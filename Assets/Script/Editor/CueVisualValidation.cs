using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Prototype.Editor
{
    public static class CueVisualValidation
    {
        [MenuItem("Tools/Prototype/Validate MD_Q visuals %&t")]
        public static void Validate()
        {
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CueVisualBuilder.PrefabPath);
                var colors = new[] { "Cyan", "Yellow", "Magenta" };
                for (int level = 1; level <= 3; level++)
                {
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                    var ball = root.GetComponent<WaterBalloon>();
                    var anchor = new Vector3((level - 2) * 1.6f, 0, -1.35f);
                    Charge(ball, anchor, level);
                    var visual = root.GetComponentInChildren<CueLevelVisual>();
                    var active = visual.GetComponentsInChildren<MeshRenderer>();
                    Check(active.Length == 1, "Exactly one level model active");
                    Check(active[0].sharedMaterial.mainTexture != null && active[0].sharedMaterial.mainTexture.name.Contains(colors[level - 1]), "Level texture");
                    Check(!root.transform.Find("Body").GetComponent<Renderer>().enabled, "Old sphere hidden");
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        Check(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported, "Valid renderer material");
                        if (AssetDatabase.GetAssetPath(renderer.sharedMaterial).StartsWith("Assets/MD_Q/"))
                            Check(renderer.sharedMaterial.mainTexture != null, "Effect texture reference");
                    }
                    Check(ball.Launch(Vector3.forward), "Launch");
                    Check(ball.Power == level && visual.GetComponentsInChildren<MeshRenderer>().Length == 1, "Flight retains level");
                    ball.Consume();
                    Check(visual.GetComponentsInChildren<Renderer>().Length == 0, "Recovery hides all effects");
                    foreach (var trail in visual.GetComponentsInChildren<TrailRenderer>(true))
                        Check(trail.positionCount == 0 && !trail.emitting, "Recovery clears trails");
                    ball.Simulate(anchor, Vector3.zero, false, 1.1f);
                    Check(ball.Power == 1 && ball.State == WaterBalloon.MotionState.Ready, "Recovery resets level");
                    Check(visual.GetComponentsInChildren<MeshRenderer>().Single().sharedMaterial.mainTexture.name.Contains("Cyan"), "Recovery texture");
                    Charge(ball, anchor, level);
                    root.transform.position = new Vector3((level - 2) * 1.6f, 0, 0);
                    visual.Hide();
                    visual.Apply(level, Vector3.zero, 1f);
                    root.GetComponent<LineRenderer>().enabled = false;
                    foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>()) particle.Simulate(0.3f, false, true);
                }
                var cameraObject = new GameObject("Preview Camera", typeof(Camera));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, preview);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = preview;
                camera.transform.SetPositionAndRotation(new Vector3(0, 8, 0), Quaternion.Euler(90, 0, 0));
                camera.orthographic = true;
                camera.orthographicSize = 1.25f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.055f, 0.075f);
                var lightObject = new GameObject("Preview Light", typeof(Light));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, preview);
                lightObject.GetComponent<Light>().type = LightType.Directional;
                lightObject.GetComponent<Light>().intensity = 1.5f;
                lightObject.transform.rotation = Quaternion.Euler(60, -30, 0);
                var texture = new RenderTexture(1200, 500, 24);
                var oldTarget = RenderTexture.active;
                var image = new Texture2D(1200, 500, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = texture;
                    camera.Render();
                    RenderTexture.active = texture;
                    image.ReadPixels(new Rect(0, 0, 1200, 500), 0, 0);
                    image.Apply();
                    Directory.CreateDirectory("Logs");
                    File.WriteAllBytes("Logs/MD_Q-levels.png", image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = oldTarget;
                    camera.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(image);
                    texture.Release();
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                File.WriteAllText("Logs/MD_Q-validation.txt", "PASS: level 1/2/3 textures and effects, single active model, launch, consume, cleared trails, recovery to level 1.\n");
                Debug.Log("MD_Q VALIDATION PASS");
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/MD_Q-validation.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void Charge(WaterBalloon ball, Vector3 anchor, int level)
        {
            ball.ResetBalloon(anchor);
            ball.Simulate(anchor, Vector3.right, true, 0f);
            for (int i = 1; i <= 1300 && ball.Power < level; i++)
            {
                float angle = (90f + i * 2f) * Mathf.Deg2Rad;
                ball.transform.position = anchor + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.35f;
                ball.Simulate(anchor, Vector3.right, true, 0f);
            }
            Check(ball.Power == level, "Orbit growth reaches requested level");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
