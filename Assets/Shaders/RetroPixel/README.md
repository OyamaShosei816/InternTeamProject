# Retro Pixel（Unity 6 / URP 17）

参考画像に近い細かめのドットを狙った、画面全体へのピクセル化シェーダーです。
PC_Renderer と Mobile_Renderer に Full Screen Pass を追加済みです。
Unity に戻ってインポートが完了すると適用されます。

`Assets/Shaders/RetroPixel/RetroPixel.mat` を選択して調整してください。

| 項目 | 用途 |
| --- | --- |
| Vertical Resolution | 仮想画面の縦ドット数。初期値288（1024×576表示で1ドット約2×2px）。小さくすると粗くなります。216で少し粗め、144で大きめ。 |
| Integer Pixel Size | 描画バッファ上でドットの辺を整数ピクセルに揃えます。OFFは指定した密度優先、ONは均一なブロック優先。端に部分的なブロックが生じることがあります。 |
| Color Levels Per Channel | 減色時のRGB各チャンネルの段階数。 |
| Color Reduction | 減色の強さ。初期値0は元の色を保持。レトロな階調を加えたい場合は1へ。 |
| Ordered Dither | 減色時の4×4ディザの強さ。Color Reductionが0の場合は無効。 |

16:9の画面では初期値が約512×288ドット相当です。参照画像からの目安であり、元ゲームの内部解像度を特定したものではありません。
アスペクト比に合わせて横のドット数を自動調整します。暗い色の階調を残すため、任意の減色は表示色空間で行います。

無効化するには、使用するRendererアセットの「Retro Pixel」チェックを外してください。
別のRendererに追加する場合は Full Screen Pass Renderer Feature を追加し、以下を指定します。

- Injection Point: After Rendering Post Processing
- Fetch Color Buffer: ON
- Requirements: None
- Pass Material: RetroPixel
- Pass Index: 0
- Bind Depth Stencil: OFF

ポストプロセス後に点サンプリングするため、Bloomもドット単位になります。
Screen Space - Overlay のUIはピクセル化対象外です。Sceneビューにも反映されます。
鮮明さ優先ならURPのRender Scaleを1、カメラの後段AAをNoneにしてください。
Mobile設定の既存Render Scaleは0.8のため、最終拡大で境界が多少柔らかくなる場合があります。
この処理は最終画像を加工するもので、描画負荷を低解像度レンダリング相当に削減するものではありません。
形状・テクスチャ・配色をピクセルアートとして描き直す機能はありません。
