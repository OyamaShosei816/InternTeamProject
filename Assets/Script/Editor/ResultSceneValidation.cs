using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Prototype.Editor
{
    // Play Modeで、実際の終了処理からリトライまでを通して確認する。
    public static class ResultSceneValidation
    {
        private const string Pending = "Prototype.ResultValidation";
        private static int stage;
        private static double nextCheck;
        private static double deadline;

        [MenuItem("Tools/Prototype/Validate Result Flow %&v")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying) return;
            if (SceneManager.GetActiveScene().path != ResultScreen.ScenePath)
                throw new InvalidOperationException("Open ResultScene before validation.");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (deadline == 0) { deadline = EditorApplication.timeSinceStartup + 45; nextCheck = EditorApplication.timeSinceStartup + 2; }
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Result flow timed out.");
                switch (stage)
                {
                    case 0:
                        Check(GameObject.Find("Time").GetComponent<Text>().text == "--:--.--", "Standalone preview");
                        GameObject.Find("Retry").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 1:
                        Check(SceneManager.GetActiveScene().name == "MainScene", "Standalone retry");
                        var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeArena>();
                        Check(arena != null, "Arena loaded");
                        typeof(PrototypeArena).GetField("roundElapsed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena, 83.25f);
                        typeof(PrototypeArena).GetField("currentStock", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena, 2);
                        typeof(PrototypeArena).GetMethod("EndRound", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { true });
                        break;
                    case 2:
                        Check(SceneManager.GetActiveScene().path == ResultScreen.ScenePath, "Clear transition");
                        Check(GameObject.Find("Result").GetComponent<Text>().text == "CLEAR!", "Clear label");
                        Check(GameObject.Find("Time").GetComponent<Text>().text == "01:23.25", "Time transfer");
                        Check(GameObject.Find("Stock").GetComponent<Text>().text == "2", "Stock transfer");
                        Directory.CreateDirectory("Logs");
                        ScreenCapture.CaptureScreenshot("Logs/ResultScene-preview.png");
                        break;
                    case 3:
                        GameObject.Find("Retry").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 4:
                        Check(SceneManager.GetActiveScene().name == "MainScene", "Result retry");
                        Check(UnityEngine.Object.FindFirstObjectByType<PrototypeArena>().State == PrototypeArena.RoundState.Playing, "Fresh round");
                        ResultScreen.Record(false, 12.5f, 0, "MainScene");
                        SceneManager.LoadScene(ResultScreen.ScenePath);
                        break;
                    case 5:
                        Check(GameObject.Find("Result").GetComponent<Text>().text == "GAME OVER", "Failure label");
                        Check(GameObject.Find("Stock").GetComponent<Text>().text == "0", "Zero stock");
                        File.WriteAllText("Logs/ResultScene-validation.txt", "PASS: standalone preview, clear transition, time/stock transfer, retry, fresh round, failure display.\n");
                        Debug.Log("RESULT VALIDATION PASS");
                        Finish();
                        return;
                }
                stage++;
                nextCheck = EditorApplication.timeSinceStartup + 2;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ResultScene-validation.txt", "FAIL: " + error);
                Debug.LogException(error);
                Finish();
            }
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new Exception(label);
        }

        private static void Finish()
        {
            SessionState.SetBool(Pending, false);
            stage = 0;
            deadline = 0;
            EditorApplication.isPlaying = false;
        }
    }
}
