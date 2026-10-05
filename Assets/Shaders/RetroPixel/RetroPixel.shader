// URPのFull Screen Passで画面全体をドット化するシェーダー。
// ポストプロセス後の画像を、仮想ドットの中心から1回ずつ読み取る。
// RetroPixel.matで粗さを調整する。減色とディザは任意で、初期状態では無効。
Shader "InternTeam/Retro Pixel"
{
    Properties
    {
        // 仮想画面の縦ドット数。小さいほど粗い。288なら高さ576pxの画面で約2px角になる。
        _VerticalResolution ("Vertical Resolution (lower = coarser)", Range(64, 720)) = 288
        // ONなら描画バッファ上のドットの辺を整数pxに丸め、ブロックの大きさを揃える。
        [Toggle] _IntegerScale ("Integer Pixel Size", Float) = 0
        // 減色時のRGB各チャンネルの段階数。パレット全体の色数ではない。
        _ColorLevels ("Color Levels Per Channel", Range(2, 64)) = 32
        // 減色結果との混ぜ具合。0は元の色、1は完全に減色した色。
        _ColorReduction ("Color Reduction", Range(0, 1)) = 0
        // 減色の境界に規則的な模様を加える強さ。Color Reductionが0なら影響しない。
        _DitherStrength ("Ordered Dither", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "RetroPixel"
            // 画面全体への加工なので、奥行き判定・奥行き書き込み・裏面除去を使わない。
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // マテリアルから受け取る設定値。Propertiesと同じ名前で対応付ける。
            CBUFFER_START(UnityPerMaterial)
                // マテリアルで指定した縦方向の仮想ドット数。
                float _VerticalResolution;
                // 1ならドットの辺の長さを整数ピクセルへ丸める。
                float _IntegerScale;
                // 減色後のRGB各チャンネルの段階数。
                float _ColorLevels;
                // 元の色から減色結果へ混ぜる割合（0～1）。
                float _ColorReduction;
                // 減色の丸めに加えるディザ模様の強さ（0～1）。
                float _DitherStrength;
            CBUFFER_END

            // 4×4のベイヤー配列に相当する値を計算する。cellは仮想ドットの整数座標。
            // 約-0.5～+0.5の値で丸め方を少しずつ変え、減色時の階調の境界を目立ちにくくする。
            float Bayer4(uint2 cell)
            {
                // 模様は元画像のピクセルではなく、ドット化後のマス目に固定する。
                uint2 low = cell & 1u;
                // ドット座標の下から2番目のビット。4×4内のブロック位置を求める。
                uint2 high = (cell >> 1u) & 1u;
                // 2×2内の位置に対応するディザの基本番号（0～3）。
                uint a = ((low.x ^ low.y) << 1u) | low.y;
                // 4×4内のブロックに対応するディザの補助番号（0～3）。
                uint b = ((high.x ^ high.y) << 1u) | high.y;
                return (float(4u * a + b) + 0.5) / 16.0 - 0.5;
            }

            // 画面の各ピクセルの色を決める処理。同じ仮想ドットに属するピクセルは同じ色を読む。
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // 実際の描画バッファのサイズを使い、縦ドット数から1ドットの辺の長さを求める。
                float2 size = max(_ScaledScreenParams.xy, 1.0);
                // 実際の画面の高さを超えないように制限した縦ドット数。
                float height = clamp(round(_VerticalResolution), 1.0, size.y);
                // 仮想ドット1個の辺の長さ（描画バッファ上のピクセル数）。
                float blockSize = size.y / height;
                if (_IntegerScale > 0.5)
                    blockSize = max(1.0, round(blockSize));

                // UV（画像の位置を0～1で表す座標）を通常の画面範囲へ戻してから、マス目に区切る。
                // 横・縦に同じ辺の長さを使うため、画面比率が変わってもドットは正方形になる。
                float2 uv = DYNAMIC_SCALING_REMOVE_SCALEBIAS(input.texcoord);
                // 今回の画面ピクセルが属する仮想ドットの番号（横・縦）。
                float2 cell = floor(uv * size / blockSize);
                // 各マス目の中心を読む。0.5を加えることで境界上のサンプリングを避ける。
                float2 sampleUV = (cell + 0.5) * blockSize / size;
                // 画面端の半端なマス目でも画像の外を読まないよう、端のピクセル中心に制限する。
                sampleUV = clamp(sampleUV, 0.5 / size, 1.0 - 0.5 / size);
                sampleUV = DYNAMIC_SCALING_APPLY_SCALEBIAS(sampleUV);
                // Pointサンプリングで隣の色を混ぜずに読む。ドットの境界がぼやけるのを防ぐ。
                half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, sampleUV, 0);

                if (_ColorReduction > 0.0)
                {
                    // 暗部の色の差を残しやすいよう、表示用の色空間（sRGB）に変換してから減色する。
                    float3 displayColor = max(float3(color.rgb), 0.0);
                    #if !defined(UNITY_COLORSPACE_GAMMA)
                        displayColor = LinearToSRGB(displayColor);
                    #endif
                    // 0～1の色を指定段階数に丸める。ディザは丸める前に加えて模様を作る。
                    float steps = max(2.0, round(_ColorLevels)) - 1.0;
                    // 減色前に加える模様の値。ディザ強度が0なら0。
                    float noise = Bayer4((uint2)cell) * _DitherStrength;
                    // 指定した色の段階数へ丸めた表示用のRGB値。
                    float3 reduced = saturate(floor(saturate(displayColor) * steps + 0.5 + noise) / steps);
                    // HDRの白（1.0）を超える明るさは、切り捨てずに減色後へ戻す。
                    reduced += max(displayColor - 1.0, 0.0);
                    #if !defined(UNITY_COLORSPACE_GAMMA)
                        reduced = SRGBToLinear(reduced);
                    #endif
                    // 元の色と減色結果を指定割合で混ぜる。アルファ（透明度）は変更しない。
                    color.rgb = lerp(color.rgb, reduced, _ColorReduction);
                }
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
