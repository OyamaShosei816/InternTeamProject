using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Prototype.Editor
{
    // 指定されたfix版のエフェクトを、Effectシーンの弾消し処理へ登録する。
    public static class BulletEraseEffectSetup
    {
        // ユーザー指定のレベル別Prefabのフォルダー。
        private const string Folder = "Assets/EF_Bullet_Dest/fix/";

        // 未保存の作業を保護してからEffectシーンの登録を更新する。
        [MenuItem("Tools/Prototype/Setup Bullet Erase Effects")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // バッチ検証と通常の設定で共通利用する、Prefab登録処理。
        public static void Build()
        {
            // 既存のEffectシーンを開き、ゲーム進行の管理元を取得する。
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Effect.unity");
            // 他のシーンやPrefabへ影響させず、この管理元だけに追加する。
            var arena = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PrototypeArena>(true)).Single();
            // 再設定しても同じコンポーネントを重複して追加しない。
            var player = arena.GetComponent<BulletEraseEffectPlayer>();
            if (player == null) player = arena.gameObject.AddComponent<BulletEraseEffectPlayer>();
            // Inspectorに保存するレベル別Prefabの参照。
            var settings = new SerializedObject(player);
            // レベル1～3のPrefabを順番に登録する。
            for (int level = 1; level <= 3; level++)
            {
                // 元版ではなく、指定されたfix版のパス。
                string path = Folder + $"Bullet_Dest_Lv{level}_Null 1.prefab";
                PrepareParticles(path);
                settings.FindProperty($"level{level}Prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // 提供Prefabの形状・色・放射速度を保ち、旧標準マテリアルだけをURPへ対応させる。
        private static void PrepareParticles(string path)
        {
            // Prefab編集用の一時的なルート。終了時に必ずアンロードする。
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // 非表示の子も含め、描画するパーティクルを確認する。
                foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    renderer.gameObject.layer = LayerMask.NameToLayer(EffectLayerScope.LayerName);
                    // Emitterは粒子生成専用で、描画用マテリアルを必要としない。
                    if (renderer.renderMode == ParticleSystemRenderMode.None) continue;
                    // 元からURP対応しているFlareなどのマテリアルは保持する。
                    Material original = renderer.sharedMaterial;
                    if (original != null && original.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                    // 旧Unity標準の粒子マテリアルは、同じテクスチャを使うURP版へ差し替える。
                    string materialPath = Folder + (renderer.name.Contains("Emissive") ? "BulletErase_Glow_URP.mat" : "BulletErase_Spark_URP.mat");
                    // レベル間で同じ描画設定を共有する。
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                        material.SetTexture("_BaseMap", original != null ? original.mainTexture : Texture2D.whiteTexture);
                        material.SetColor("_BaseColor", Color.white);
                        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 2);
                        material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 1);
                        material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0);
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    renderer.sharedMaterial = material;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
