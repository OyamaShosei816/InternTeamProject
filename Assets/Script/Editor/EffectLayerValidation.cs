using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Prototype.Editor
{
    // 独立した検証用プロジェクトから実行し、実際のGPU描画でレイヤー分離を調べる。
    public static class EffectLayerValidation
    {
        // ドメイン再読み込みをまたいで検証の開始状態を保持するキー。
        private const string Pending = "EffectLayerValidation.Pending";
        // 品質変更後、描画パイプラインが更新されるまで待つ時刻。
        private static double nextCheck;
        // PC、Mobile、完了の順に進む検証段階。
        private static int stage;

        // バッチ実行の入口。設定生成後、Play Modeで動的な演出を検証する。
        public static void BuildAndValidateBatch()
        {
            EffectSceneSetup.Build();
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        // Play Mode開始による再読み込み後に検証を再開する。
        [InitializeOnLoadMethod]
        private static void Register() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        // 品質を順番に切り替え、生成された画像と判定結果を保存する。
        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (nextCheck == 0) nextCheck = EditorApplication.timeSinceStartup + 5;
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            try
            {
                if (stage == 0) QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "PC"), true);
                if (stage == 1) { CheckRendering("PC"); QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "Mobile"), true); }
                if (stage == 2) { CheckRendering("Mobile"); Finish("PASS: PC/Mobile pixel exclusion, depth occlusion, runtime line/particle/trail classification.", 0); return; }
                stage++;
                nextCheck = EditorApplication.timeSinceStartup + 3;
            }
            catch (Exception error) { Finish("FAIL: " + error, 1); }
        }

        // 結果をファイルへ書き出して検証用Unityを終了する。
        private static void Finish(string result, int code)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/EffectLayer-validation.txt", result);
            SessionState.SetBool(Pending, false);
            Debug.Log(result);
            EditorApplication.Exit(code);
        }

        // 左の通常レイヤーと右のEffectレイヤーに同じ斜線を描き、輪郭の細かさと遮蔽を比べる。
        private static void CheckRendering(string quality)
        {
            // Windows検証時は、検証用コピーだけでMobile品質のStandalone除外を解除しておく。
            Check(GraphicsSettings.currentRenderPipeline.name == quality + "_RPAsset", "Requested quality is excluded on this platform: " + quality);
            // 検証対象の専用Rendererと、そのドット化パス。
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/Settings/{quality}_Effect_Renderer.asset");
            // Full Screen Passはドット化用の1つだけであることを確認する。
            var pixel = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().Single();
            // 実アセットを変更せず、検証中だけ粗いドットの複製へ差し替える。
            var originalMaterial = pixel.passMaterial;
            // 分離を判定しやすい64ドットの一時マテリアル。
            var coarse = new Material(originalMaterial);
            coarse.SetFloat("_VerticalResolution", 64); pixel.passMaterial = coarse;
            // 品質変更で作られたRendererにも、一時マテリアルを確実に反映する。
            data.SetDirty();
            Debug.Log($"Effect check {quality}: active asset={GraphicsSettings.currentRenderPipeline.name}, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            // 既存のシーンから十分離れた場所に置く、一時的な検証オブジェクトの親。
            var root = new GameObject("Effect rendering validation");
            // 読み取り用の描画先。深度も確保し、前後関係を検証する。
            var target = new RenderTexture(512, 512, 24);
            // 照明に依存しない白色の不透明マテリアル。
            var white = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            white.SetColor("_BaseColor", Color.white); white.SetFloat("_Cull", 0);
            // 不透明物と透明物の両方が分離されることを調べる。
            var transparent = new Material(white);
            transparent.SetFloat("_Surface", 1); transparent.SetFloat("_SrcBlend", 5); transparent.SetFloat("_DstBlend", 10);
            transparent.SetFloat("_ZWrite", 0); transparent.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); transparent.renderQueue = 3000;
            // エフェクトの手前に置く遮蔽物。緑が残れば深度判定が正常。
            var green = new Material(white); green.SetColor("_BaseColor", Color.green);
            // テスト前の描画先を最後に復元する。
            var previous = RenderTexture.active;
            // GPUから読み取った比較画像。最後に破棄する。
            Texture2D image = null;
            try
            {
                // Effectレイヤー番号を取得する。
                int layer = LayerMask.NameToLayer(EffectLayerScope.LayerName);
                // 実行中に生成した演出の分類を実際のコンポーネントで確認する。
                var scope = UnityEngine.Object.FindFirstObjectByType<EffectLayerScope>();
                // 3種類の演出を、起動後に新規生成する。
                foreach (Type type in new[] { typeof(LineRenderer), typeof(TrailRenderer), typeof(ParticleSystem) })
                {
                    // 一時オブジェクトを既存シーンに追加し、自動分類の対象にする。
                    var dynamicEffect = new GameObject("Runtime effect check", type);
                    dynamicEffect.transform.SetParent(root.transform);
                    scope.RefreshLayers();
                    Check(dynamicEffect.layer == layer, "Dynamic classification: " + type.Name);
                    dynamicEffect.SetActive(false);
                }
                // 比較専用カメラ。ゲーム本体のカメラ設定は触らない。
                var camera = root.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.position = new Vector3(0, 1000, -10); camera.orthographic = true; camera.orthographicSize = 4;
                camera.aspect = 1; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = false; camera.allowMSAA = false; camera.farClipPlane = 30;
                camera.GetUniversalAdditionalCameraData().SetRenderer(1);
                // 左右の配置位置。左は通常、右はEffectレイヤー。
                foreach (float x in new[] { -1.5f, 1.5f })
                    // 上段は不透明、下段は透明の演出。
                    foreach (float y in new[] { -1.5f, 1.5f })
                    {
                        // 同じ斜線を両レイヤーへ配置し、画面上の輪郭を比較する。
                        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        quad.transform.SetParent(root.transform, true); quad.transform.position = new Vector3(x, 1000 + y, 0);
                        quad.transform.rotation = Quaternion.Euler(0, 0, 25); quad.transform.localScale = new Vector3(0.12f, 2.5f, 1);
                        quad.layer = x > 0 ? layer : 0; quad.GetComponent<Renderer>().sharedMaterial = y > 0 ? white : transparent;
                    }
                // 右上の斜線を部分的に隠す通常レイヤーの物体。
                var block = GameObject.CreatePrimitive(PrimitiveType.Quad);
                block.transform.SetParent(root.transform, true); block.transform.position = new Vector3(1.5f, 1001.5f, -1);
                block.transform.localScale = new Vector3(0.6f, 0.6f, 1); block.GetComponent<Renderer>().sharedMaterial = green;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(512, 512, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/EffectLayer-{quality}.png", image.EncodeToPNG());
                Check(Edges(image, 275, 485, 275, 485) > Edges(image, 30, 240, 275, 485) + 8, quality + " opaque exclusion");
                Check(Edges(image, 275, 485, 30, 240) > Edges(image, 30, 240, 30, 240) + 8, quality + " transparent exclusion");
                // 遮蔽物の中心。エフェクトが無条件で最前面に出ていないことを確認する。
                Color center = image.GetPixel(352, 352);
                Check(center.g > 0.5f && center.r < 0.2f, quality + " depth occlusion");
            }
            finally
            {
                pixel.passMaterial = originalMaterial; RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root); target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(coarse); UnityEngine.Object.DestroyImmediate(white);
                UnityEngine.Object.DestroyImmediate(transparent); UnityEngine.Object.DestroyImmediate(green);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
        }

        // 白い斜線の左端が何種類のX座標を通るか数える。ドット化されると種類が減る。
        private static int Edges(Texture2D image, int xMin, int xMax, int yMin, int yMax)
        {
            // 重複するX座標を除いた輪郭位置の集合。
            var positions = new HashSet<int>();
            // 指定範囲の行を上から調べる。
            for (int y = yMin; y < yMax; y++)
                // 行の左から白いピクセルを探す。
                for (int x = xMin; x < xMax; x++)
                {
                    // 緑の遮蔽物を除外して、白い斜線だけを判定する。
                    Color color = image.GetPixel(x, y);
                    if (color.r > 0.5f && color.g > 0.5f && color.b > 0.5f) { positions.Add(x); break; }
                }
            return positions.Count;
        }

        // 条件が成立しなければ、失敗した検証項目を例外として報告する。
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
