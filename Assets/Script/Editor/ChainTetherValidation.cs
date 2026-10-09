using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Prototype.Editor
{
    public static class ChainTetherValidation
    {
        [MenuItem("Tools/Prototype/Validate Player_Q chain")]
        public static void Validate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Player_Q.unity");
            var ball = UnityEngine.Object.FindFirstObjectByType<WaterBalloon>();
            var settings = new SerializedObject(ball);
            var prefab = settings.FindProperty("chainTetherPrefab").objectReferenceValue;
            Check(prefab != null, "Scene chain prefab reference");
            Check(AssetDatabase.GetAssetPath(prefab) ==
                "Assets/EF_Chain_Plazma/EF_Chain_Q.prefab", "EF_Chain_Q prefab");
            Check(ball.GetComponent<LineRenderer>() == null, "Scene LineRenderer removed");
            Vector3 anchor = new Vector3(1, 0.65f, -3);
            ball.ResetBalloon(anchor);
            settings.Update();
            var chain = (Transform)settings.FindProperty("chainTether").objectReferenceValue;
            Check(chain != null, "Chain instantiated");
            Check(!chain.GetComponent<Collider>().enabled, "Visual has no collision");
            var mesh = chain.GetComponent<MeshFilter>().sharedMesh;
            Check((mesh.bounds.size - new Vector3(1, 1, 0)).sqrMagnitude < 0.0001f, "Unit quad dimensions");
            for (int i = 0; i < 120; i++)
            {
                anchor.x += 0.01f;
                ball.Simulate(anchor, Vector3.right, true, 1f / 60f);
                Check(chain.gameObject.activeSelf && ball.GetComponent<LineRenderer>() == null, "Only chain visible");
                Check(Vector3.Distance(chain.TransformPoint(new Vector3(0, -0.5f, 0)), anchor) < 0.001f, "Player endpoint");
                Check(Vector3.Distance(chain.TransformPoint(new Vector3(0, 0.5f, 0)), ball.transform.position) < 0.001f, "Cue endpoint");
            }
            Check(ball.Launch(Vector3.forward) && !chain.gameObject.activeSelf, "Launch hides chain");
            ball.Consume();
            Check(!chain.gameObject.activeSelf, "Recovery hides chain");
            ball.Simulate(anchor, Vector3.zero, false, 1.1f);
            Check(chain.gameObject.activeSelf, "Respawn restores chain");
            // Restore the saved scene after exercising the simulation in edit mode.
            EditorSceneManager.OpenScene("Assets/Scenes/Player_Q.unity");
            Debug.Log("CHAIN_TETHER_VALIDATION_PASSED");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }
    }
}
