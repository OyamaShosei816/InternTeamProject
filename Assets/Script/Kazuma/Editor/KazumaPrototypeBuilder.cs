using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace KazumaPrototype.Editor
{
    public static class KazumaPrototypeBuilder
    {
        public const string ScenePath = "Assets/Scenes/Kazuma.unity";
        private const string PrefabPath = "Assets/Prefab/Kazuma/";
        private const string MaterialPath = "Assets/Materials/Kazuma/";

        [MenuItem("Tools/Kazuma/Rebuild gameplay prototype")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(PrefabPath);
            Directory.CreateDirectory(MaterialPath);
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Kazuma Gameplay Prototype") UnityEngine.Object.DestroyImmediate(root);
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0, 20, 0), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 9;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            Material playerMaterial = Material("Player", new Color(0.12f, 0.8f, 1f));
            Material ballMaterial = Material("WaterBalloon", new Color(0.25f, 1f, 0.3f));
            Material bossMaterial = Material("Boss", new Color(0.85f, 0.12f, 0.12f));
            Material weakMaterial = Material("WeakPoint", new Color(1f, 0.8f, 0.1f));
            Material lineMaterial = Material("Tether", new Color(0.5f, 0.7f, 0.75f), "Universal Render Pipeline/Particles/Unlit");
            Material pulseMaterial = Material("Pulse", Color.white, "Universal Render Pipeline/Particles/Unlit");

            var playerRoot = new GameObject("Player");
            var playerBody = Sphere("Body", playerRoot.transform, Vector3.zero, Vector3.one * 0.68f, playerMaterial);
            var player = playerRoot.AddComponent<KazumaDragPlayer>();
            Set(player, "body", playerBody);
            GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(playerRoot, PrefabPath + "Player.prefab");
            UnityEngine.Object.DestroyImmediate(playerRoot);

            var ballRoot = new GameObject("WaterBalloon");
            var ballBody = Sphere("Body", ballRoot.transform, Vector3.zero, Vector3.one * 0.8f, ballMaterial);
            var line = ballRoot.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.widthMultiplier = 0.045f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.numCapVertices = 3;
            var balloon = ballRoot.AddComponent<KazumaWaterBalloon>();
            Set(balloon, "body", ballBody);
            Set(balloon, "tether", line);
            GameObject balloonPrefab = PrefabUtility.SaveAsPrefabAsset(ballRoot, PrefabPath + "WaterBalloon.prefab");
            UnityEngine.Object.DestroyImmediate(ballRoot);

            var bulletRoot = new GameObject("EnemyBullet");
            var bulletBody = Sphere("Body", bulletRoot.transform, Vector3.zero, Vector3.one, ballMaterial);
            var bullet = bulletRoot.AddComponent<KazumaBullet>();
            Set(bullet, "body", bulletBody);
            GameObject bulletPrefab = PrefabUtility.SaveAsPrefabAsset(bulletRoot, PrefabPath + "EnemyBullet.prefab");
            UnityEngine.Object.DestroyImmediate(bulletRoot);

            var gameplay = new GameObject("Kazuma Gameplay Prototype");
            var arena = gameplay.AddComponent<KazumaPrototypeArena>();
            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, gameplay.transform);
            playerInstance.transform.position = new Vector3(0f, 0.65f, -4.5f);
            var ballInstance = (GameObject)PrefabUtility.InstantiatePrefab(balloonPrefab, gameplay.transform);
            ballInstance.transform.position = playerInstance.transform.position + Vector3.back * 1.35f;
            var instanceLine = ballInstance.GetComponent<LineRenderer>();
            instanceLine.SetPosition(0, playerInstance.transform.position);
            instanceLine.SetPosition(1, ballInstance.transform.position);
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerInstance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ballInstance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceLine);
            var boss = new GameObject("Boss").transform;
            boss.SetParent(gameplay.transform);
            boss.position = new Vector3(0f, 0.65f, 5.6f);
            Sphere("Body", boss, Vector3.zero, new Vector3(2.3f, 0.8f, 2.3f), bossMaterial);
            var weakPoint = Sphere("Weak Point", boss, new Vector3(0f, 0f, -1.15f), Vector3.one * 1.1f, weakMaterial).transform;
            Set(arena, "gameCamera", camera);
            Set(arena, "player", playerInstance.GetComponent<KazumaDragPlayer>());
            Set(arena, "balloon", ballInstance.GetComponent<KazumaWaterBalloon>());
            Set(arena, "bulletPrefab", bulletPrefab.GetComponent<KazumaBullet>());
            Set(arena, "boss", boss);
            Set(arena, "weakPoint", weakPoint);
            Set(arena, "effectMaterial", pulseMaterial);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Kazuma prototype scene and prefabs saved.");
        }

        private static Renderer Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            // Gameplay uses swept sphere tests rather than frame-dependent trigger callbacks.
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Material Material(string name, Color color, string shaderName = "Universal Render Pipeline/Lit")
        {
            string path = MaterialPath + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
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

        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
