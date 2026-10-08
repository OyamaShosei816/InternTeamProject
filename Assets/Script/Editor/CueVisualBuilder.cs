using System;
using UnityEditor;
using UnityEngine;

namespace Prototype.Editor
{
    public static class CueVisualBuilder
    {
        public const string PrefabPath = "Assets/Prefab/Player/WaterBalloon.prefab";

        [MenuItem("Tools/Prototype/Apply MD_Q visuals %&q")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Configure(root.GetComponent<WaterBalloon>());
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("MD_Q level visuals applied to WaterBalloon prefab.");
        }

        public static void Configure(WaterBalloon balloon)
        {
            var oldVisual = balloon.GetComponentInChildren<CueLevelVisual>(true);
            if (oldVisual != null) UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
            var holder = new GameObject("Cue Visuals");
            holder.transform.SetParent(balloon.transform, false);
            var visual = holder.AddComponent<CueLevelVisual>();
            var settings = new SerializedObject(visual);
            var levels = settings.FindProperty("levels");
            levels.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/MD_Q/MD_Q_Lv{i + 1}.prefab");
                if (asset == null) throw new InvalidOperationException($"Missing MD_Q level {i + 1}");
                var wrapper = new GameObject($"Level {i + 1}");
                wrapper.transform.SetParent(holder.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, wrapper.transform);
                var mesh = model.GetComponentInChildren<MeshRenderer>(true);
                if (mesh == null) throw new InvalidOperationException($"Missing MD_Q mesh at level {i + 1}");
                var size = mesh.bounds.size;
                float scale = WaterBalloon.Radius * 2f / Mathf.Max(size.x, size.y, size.z);
                Vector3 center = wrapper.transform.InverseTransformPoint(mesh.bounds.center);
                model.transform.localPosition -= center;
                wrapper.transform.localScale = Vector3.one * scale;
                foreach (var particle in model.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(particle);
                }
                foreach (var trail in model.GetComponentsInChildren<TrailRenderer>(true))
                {
                    trail.widthMultiplier *= scale;
                    trail.autodestruct = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(trail);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
                wrapper.SetActive(i == 0);
                levels.GetArrayElementAtIndex(i).objectReferenceValue = wrapper;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            var balloonSettings = new SerializedObject(balloon);
            balloonSettings.FindProperty("levelVisual").objectReferenceValue = visual;
            var body = (Renderer)balloonSettings.FindProperty("body").objectReferenceValue;
            body.enabled = false;
            balloonSettings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
