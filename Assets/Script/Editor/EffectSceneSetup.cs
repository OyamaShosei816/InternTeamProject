using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Prototype.Editor
{
    // Effectシーン専用の描画設定を生成する。既存シーンの既定Rendererは維持する。
    public static class EffectSceneSetup
    {
        // エフェクトのレイヤー分離を適用するシーンの保存先。
        public const string ScenePath = "Assets/Scenes/Effect.unity";

        // メニューから実行する。現在の未保存作業を確認してから設定する。
        [MenuItem("Tools/Prototype/Setup Effect Layers %&e")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // 保存済みのEffectシーンに、専用Rendererとレイヤー分類を設定する。
        public static void Build()
        {
            // 空いているユーザーレイヤーを確保する。
            int layer = EnsureLayer();
            // 品質を切り替えてもカメラが同じ番号を参照できるよう両方に登録する。
            int pcIndex = ConfigureRenderer("PC", layer);
            // Mobile側の専用Renderer番号。PCとの一致を確認する。
            int mobileIndex = ConfigureRenderer("Mobile", layer);
            if (pcIndex != mobileIndex) throw new InvalidOperationException("Effect renderer indices differ between quality levels.");
            // 既存シーンのゲームオブジェクトを保ちながら設定を追加する。
            var scene = EditorSceneManager.OpenScene(ScenePath);
            // Effectシーンのメインカメラだけに専用Rendererを指定する。
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
            camera.GetUniversalAdditionalCameraData().SetRenderer(pcIndex);
            camera.cullingMask |= 1 << layer;
            // 繰り返し実行しても設定コンポーネントを重複させない。
            var scope = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EffectLayerScope>(true)).FirstOrDefault();
            if (scope == null) scope = new GameObject("Effect Layer Setup").AddComponent<EffectLayerScope>();
            // 強攻撃はメッシュ演出なので、明示的な追加対象として登録する。
            var strongAttack = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "StrongAttackObject");
            // Inspectorの追加対象を保持しながら、強攻撃だけを不足時に追加する。
            var scopeSettings = new SerializedObject(scope);
            // デザイナーが追加した既存の対象は削除しない。
            var extras = scopeSettings.FindProperty("additionalEffects");
            // 強攻撃がすでに登録済みかどうか。
            bool registered = false;
            // 登録済みの要素を順に調べる。
            for (int i = 0; i < extras.arraySize; i++) registered |= extras.GetArrayElementAtIndex(i).objectReferenceValue == strongAttack;
            if (strongAttack != null && !registered)
            {
                extras.arraySize++;
                extras.GetArrayElementAtIndex(extras.arraySize - 1).objectReferenceValue = strongAttack;
            }
            scopeSettings.ApplyModifiedPropertiesWithoutUndo();
            scope.RefreshLayers();
            // Prefab由来の紐などにも、シーン側のLayer上書きを保存する。
            foreach (var root in scene.GetRootGameObjects())
                // 描画コンポーネントのあるGameObjectだけを記録する。
                foreach (var visual in root.GetComponentsInChildren<Renderer>(true))
                    if (visual.gameObject.layer == layer) PrefabUtility.RecordPrefabInstancePropertyModifications(visual.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("EFFECT SETUP PASS");
        }

        // Effectレイヤーを再利用する。未登録なら空きスロットへ追加する。
        private static int EnsureLayer()
        {
            // 同名レイヤーが存在する場合は、その番号を維持する。
            int existing = LayerMask.NameToLayer(EffectLayerScope.LayerName);
            if (existing >= 0) return existing;
            // UnityのTags and Layers設定を編集する。
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            // 標準レイヤーを避け、8番以降の空きだけを使う。
            var layers = tags.FindProperty("layers");
            // 既存の名前を上書きしないよう、最初の空き番号を探す。
            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = EffectLayerScope.LayerName;
                    tags.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            throw new InvalidOperationException("No free Effect layer.");
        }

        // 品質別の既存Rendererをコピーし、通常描画→ドット化→演出描画の順番にする。
        private static int ConfigureRenderer(string quality, int layer)
        {
            // 既存Rendererは変更せず、Effect専用アセットを作成する。
            string path = $"Assets/Settings/{quality}_Effect_Renderer.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path) == null && !AssetDatabase.CopyAsset($"Assets/Settings/{quality}_Renderer.asset", path))
                throw new InvalidOperationException("Renderer copy failed: " + path);
            // 不透明・透明の通常描画からEffectレイヤーを取り除く。
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            // Unityのシリアライズ済みLayer Maskを変更する。
            var settings = new SerializedObject(data);
            settings.FindProperty("m_OpaqueLayerMask").intValue &= ~(1 << layer);
            settings.FindProperty("m_TransparentLayerMask").intValue &= ~(1 << layer);
            settings.ApplyModifiedPropertiesWithoutUndo();
            // 深度とカラーの解像度が揃うポスト処理前で、リスト順に3パスを実行する。
            var pixel = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().First(f => f.passMaterial != null && f.passMaterial.shader.name == "InternTeam/Retro Pixel");
            pixel.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            EditorUtility.SetDirty(pixel);
            AddPass(data, "Effect Opaque", RenderQueueType.Opaque, layer);
            AddPass(data, "Effect Transparent", RenderQueueType.Transparent, layer);
            data.SetDirty();
            EditorUtility.SetDirty(data);
            // 品質ごとのRenderer一覧に追加する。既定番号は変更しない。
            var pipeline = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"Assets/Settings/{quality}_RPAsset.asset"));
            // カメラが選択できるRenderer一覧。
            var list = pipeline.FindProperty("m_RendererDataList");
            // すでに登録されている場合の番号。未登録は-1。
            int index = -1;
            // 再実行時に同じアセットを二重登録しない。
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) index = i;
            if (index < 0)
            {
                index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = data;
            }
            pipeline.ApplyModifiedPropertiesWithoutUndo();
            return index;
        }

        // 元のマテリアルと深度判定を維持して、Effectレイヤーだけを後から描く。
        private static void AddPass(UniversalRendererData data, string name, RenderQueueType queue, int layer)
        {
            // 既存パスを再利用して設定を更新する。
            var pass = data.rendererFeatures.OfType<RenderObjects>().FirstOrDefault(f => f.name == name);
            if (pass == null)
            {
                pass = ScriptableObject.CreateInstance<RenderObjects>();
                pass.name = name;
                AssetDatabase.AddObjectToAsset(pass, data);
                data.rendererFeatures.Add(pass);
            }
            pass.settings.passTag = name;
            pass.settings.Event = RenderPassEvent.BeforeRenderingPostProcessing;
            pass.settings.filterSettings.RenderQueueType = queue;
            pass.settings.filterSettings.LayerMask = 1 << layer;
            pass.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            pass.settings.overrideDepthState = false;
            pass.Create();
            EditorUtility.SetDirty(pass);
        }
    }
}
