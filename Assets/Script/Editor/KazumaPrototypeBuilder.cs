using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace KazumaPrototype.Editor
{
    // Unityエディター専用。Tools/Kazumaメニューから試作シーン・Prefab・マテリアルを再生成する。
    // 既存の生成用ルートを置き換え、カメラ設定と保存済みアセットも更新するため、手動編集後の実行には注意。
    public static class KazumaPrototypeBuilder
    {
        // 生成先の固定パス。ScenePathのシーンは事前に存在している必要がある。
        public const string ScenePath = "Assets/Scenes/Kazuma.unity";
        // 自動生成するPrefabの保存先フォルダー。
        private const string PrefabPath = "Assets/Prefab/Kazuma/";
        // 自動生成するマテリアルの保存先フォルダー。
        private const string MaterialPath = "Assets/Materials/Kazuma/";

        // 再生成の入口。通常実行では編集中シーンの保存確認を表示してから進む。
        [MenuItem("Tools/Kazuma/Rebuild gameplay prototype")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(PrefabPath);
            Directory.CreateDirectory(MaterialPath);
            AssetDatabase.Refresh();
            // 生成対象として開いたシーン。
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            // 前回このツールで作ったルートだけ削除し、同名のゲーム部分が重複しないようにする。
            // root：一覧から取り出した、今回処理する対象。
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Kazuma Gameplay Prototype") UnityEngine.Object.DestroyImmediate(root);
            // MainCameraタグのカメラを真上から見下ろす平行投影に設定する。
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0, 20, 0), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 9;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            // 役割ごとのマテリアルを作成／再利用し、識別しやすい色を割り当てる。
            Material playerMaterial = Material("Player", new Color(0.12f, 0.8f, 1f));
            // 水風船と敵弾の初期表示に使う緑色のマテリアル。
            Material ballMaterial = Material("WaterBalloon", new Color(0.25f, 1f, 0.3f));
            // ボス本体を赤色に表示するマテリアル。
            Material bossMaterial = Material("Boss", new Color(0.85f, 0.12f, 0.12f));
            // 弱点を黄色に表示するマテリアル。
            Material weakMaterial = Material("WeakPoint", new Color(1f, 0.8f, 0.1f));
            // 紐の線に使うマテリアル。
            Material lineMaterial = Material("Tether", new Color(0.5f, 0.7f, 0.75f), "Universal Render Pipeline/Particles/Unlit");
            // 円形エフェクトの線に使うマテリアル。
            Material pulseMaterial = Material("Pulse", Color.white, "Universal Render Pipeline/Particles/Unlit");

            // プレイヤーPrefab：見た目の球と、入力・移動を担当するコンポーネントを組み合わせる。
            var playerRoot = new GameObject("Player");
            // プレイヤーの表示用の球。
            var playerBody = Sphere("Body", playerRoot.transform, Vector3.zero, Vector3.one * 0.68f, playerMaterial);
            // 操作するプレイヤーの入力・移動コンポーネント。
            var player = playerRoot.AddComponent<KazumaDragPlayer>();
            Set(player, "body", playerBody);
            // 保存したプレイヤーPrefabのアセット。
            GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(playerRoot, PrefabPath + "Player.prefab");
            UnityEngine.Object.DestroyImmediate(playerRoot);

            // 水風船Prefab：球、紐の線、動作を管理するコンポーネントを組み合わせる。
            var ballRoot = new GameObject("WaterBalloon");
            // 水風船の表示用の球。
            var ballBody = Sphere("Body", ballRoot.transform, Vector3.zero, Vector3.one * 0.8f, ballMaterial);
            // 線を表示するLineRenderer。
            var line = ballRoot.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.widthMultiplier = 0.045f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.numCapVertices = 3;
            // 水風船の移動・投擲を管理するコンポーネント。
            var balloon = ballRoot.AddComponent<KazumaWaterBalloon>();
            Set(balloon, "body", ballBody);
            Set(balloon, "tether", line);
            // 保存した水風船Prefabのアセット。
            GameObject balloonPrefab = PrefabUtility.SaveAsPrefabAsset(ballRoot, PrefabPath + "WaterBalloon.prefab");
            UnityEngine.Object.DestroyImmediate(ballRoot);

            // 敵弾Prefab：出現後の位置・強さ・色はArenaから初期化される。
            var bulletRoot = new GameObject("EnemyBullet");
            // 敵弾の表示用の球。
            var bulletBody = Sphere("Body", bulletRoot.transform, Vector3.zero, Vector3.one, ballMaterial);
            // 生成または判定対象となる敵弾。
            var bullet = bulletRoot.AddComponent<KazumaBullet>();
            Set(bullet, "body", bulletBody);
            // 保存した敵弾Prefabのアセット。
            GameObject bulletPrefab = PrefabUtility.SaveAsPrefabAsset(bulletRoot, PrefabPath + "EnemyBullet.prefab");
            UnityEngine.Object.DestroyImmediate(bulletRoot);

            // 保存したPrefabをシーンへ配置し、ボスと弱点を作ってArenaに各参照を渡す。
            var gameplay = new GameObject("Kazuma Gameplay Prototype");
            // ゲーム進行と当たり判定を管理するコンポーネント。
            var arena = gameplay.AddComponent<KazumaPrototypeArena>();
            // シーン内へ配置したプレイヤーPrefabの実体。
            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, gameplay.transform);
            playerInstance.transform.position = new Vector3(0f, 0.65f, -4.5f);
            // シーン内へ配置した水風船Prefabの実体。
            var ballInstance = (GameObject)PrefabUtility.InstantiatePrefab(balloonPrefab, gameplay.transform);
            ballInstance.transform.position = playerInstance.transform.position + Vector3.back * 1.35f;
            // シーン上の水風船に付いている紐のLineRenderer。
            var instanceLine = ballInstance.GetComponent<LineRenderer>();
            instanceLine.SetPosition(0, playerInstance.transform.position);
            instanceLine.SetPosition(1, ballInstance.transform.position);
            // Prefab本体との差分を記録し、シーンを開き直しても配置や紐の位置を保持する。
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerInstance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ballInstance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceLine);
            // ボス本体の位置と表示を管理するTransform。
            var boss = new GameObject("Boss").transform;
            boss.SetParent(gameplay.transform);
            boss.position = new Vector3(0f, 0.65f, 5.6f);
            Sphere("Body", boss, Vector3.zero, new Vector3(2.3f, 0.8f, 2.3f), bossMaterial);
            // ボスのダメージ受付位置を表す弱点のTransform。
            var weakPoint = Sphere("Weak Point", boss, new Vector3(0f, 0f, -1.15f), Vector3.one * 1.1f, weakMaterial).transform;
            Set(arena, "gameCamera", camera);
            Set(arena, "player", playerInstance.GetComponent<KazumaDragPlayer>());
            Set(arena, "balloon", ballInstance.GetComponent<KazumaWaterBalloon>());
            Set(arena, "bulletPrefab", bulletPrefab.GetComponent<KazumaBullet>());
            Set(arena, "boss", boss);
            Set(arena, "weakPoint", weakPoint);
            Set(arena, "effectMaterial", pulseMaterial);
            // シーンとアセットの両方を保存して、次回起動時にも生成結果を使えるようにする。
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Kazuma prototype scene and prefabs saved.");
        }

        // 指定の親に、表示用の球を作成する。positionとscaleは親に対するローカル値。
        private static Renderer Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            // 表示用に生成した球のGameObject。
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            // 当たり判定はArenaで移動区間を調べるため、標準のColliderは外す。
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            // 生成した球のRenderer。マテリアルや影の設定を行う。
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // 同名のマテリアルを再利用し、なければ指定シェーダーで作成する。
        // 既存のマテリアルではシェーダーを変更せず、色と対応する光沢値を更新する。
        private static Material Material(string name, Color color, string shaderName = "Universal Render Pipeline/Lit")
        {
            // マテリアル名から組み立てた保存先のパス。
            string path = MaterialPath + name + ".mat";
            // 既存アセットから読み込んだ、または新規作成するマテリアル。
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            // 指定名で検索したシェーダー。見つからなければ生成を中断する。
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Required shader missing: " + shaderName);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.55f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // SerializeFieldの非公開フィールドに、Unityの保存対象としてオブジェクト参照を設定する。
        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            // 非公開フィールドの値をUnityの保存対象として操作する窓口。
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
