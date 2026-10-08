using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Prototype.Editor
{
    public static class ResultSceneValidation
    {
        private const string Pending = "Prototype.ResultValidation";
        private const string TestKey = "Prototype.ResultRanking.ValidationOnly";
        private static int stage;
        private static double nextCheck, deadline;
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
            if (deadline == 0) { deadline = EditorApplication.timeSinceStartup + 60; nextCheck = EditorApplication.timeSinceStartup + 2; }
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Result flow timed out.");
                var screen = UnityEngine.Object.FindFirstObjectByType<ResultScreen>();
                switch (stage)
                {
                    case 0:
                        Check(GameObject.Find("Time").GetComponent<Text>().text == "--:--.--", "Standalone preview");
                        Check(GameObject.Find("Reveal Ranking").activeInHierarchy, "Tap target enabled");
                        Check(screen.CalculateTimeBonus(120) == 10000000 && screen.CalculateTimeBonus(120.01f) == 8000000, "2 minute boundary");
                        Check(screen.CalculateTimeBonus(300) == 8000000 && screen.CalculateTimeBonus(300.01f) == 500000, "5 minute boundary");
                        Check(screen.CalculateTimeBonus(480) == 500000 && screen.CalculateTimeBonus(480.01f) == 100000, "8 minute boundary");
                        Check(screen.CalculateTimeBonus(600) == 100000, "10 minute bonus");
                        Check(screen.CalculateStockBonus(0) == 100000 && screen.CalculateStockBonus(1) == 500000 && screen.CalculateStockBonus(2) == 8000000 && screen.CalculateStockBonus(3) == 10000000, "Stock bonuses");
                        screen.Retry();
                        break;
                    case 1:
                        Check(SceneManager.GetActiveScene().name == "MainScene", "Next scene");
                        var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeArena>();
                        typeof(PrototypeArena).GetField("roundElapsed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena, 83.25f);
                        typeof(PrototypeArena).GetField("currentStock", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena, 2);
                        typeof(PrototypeArena).GetMethod("EndRound", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { true });
                        break;
                    case 2:
                        Check(SceneManager.GetActiveScene().path == ResultScreen.ScenePath, "Clear transition");
                        Check(GameObject.Find("Time").GetComponent<Text>().text == "01:23.25", "Time transfer");
                        Check(GameObject.Find("Stock").GetComponent<Text>().text.Contains("2個"), "Stock transfer");
                        Check(GameObject.Find("Score").GetComponent<Text>().text == "18000000", "Count total");
                        Directory.CreateDirectory("Logs");
                        ScreenCapture.CaptureScreenshot("Logs/ResultScene-score.png");
                        break;
                    case 3:
                        typeof(ResultScreen).GetField("rankingKey", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(screen, TestKey);
                        PlayerPrefs.SetString(TestKey, "{\"entries\":[{\"id\":\"a\",\"score\":25000000},{\"id\":\"b\",\"score\":22000000},{\"id\":\"c\",\"score\":19000000},{\"id\":\"d\",\"score\":17000000},{\"id\":\"e\",\"score\":15000000},{\"id\":\"f\",\"score\":10000000}]}");
                        GameObject.Find("Reveal Ranking").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 4:
                        Check(GameObject.Find("Rank 4/Name").GetComponent<Text>().text == "4位  あなた", "Own rank");
                        Check(GameObject.Find("Rank 4/Score").GetComponent<Text>().text == "18000000", "Own score");
                        Check(GameObject.Find("Rows").transform.childCount == 8, "7 records plus template");
                        Check(GameObject.Find("Scroll View").GetComponent<ScrollRect>().content.rect.height > 544, "Scrollable content");
                        string saved = PlayerPrefs.GetString(TestKey);
                        screen.Advance();
                        Check(saved == PlayerPrefs.GetString(TestKey), "No duplicate record");
                        ScreenCapture.CaptureScreenshot("Logs/ResultScene-ranking.png");
                        break;
                    case 5:
                        GameObject.Find("Next").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 6:
                        Check(SceneManager.GetActiveScene().name == "MainScene", "Next button");
                        Check(UnityEngine.Object.FindFirstObjectByType<PrototypeArena>().State == PrototypeArena.RoundState.Playing, "Fresh round");
                        File.WriteAllText("Logs/ResultScene-validation.txt", "PASS: preview, tap target, score boundaries, stock bonuses, clear transition, time/stock transfer, count-up total, own rank, scrolling content, duplicate prevention, next scene, fresh round.");
                        Debug.Log("RESULT VALIDATION PASS");
                        Finish();
                        return;
                }
                stage++;
                nextCheck = EditorApplication.timeSinceStartup + 4;
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ResultScene-validation.txt", "FAIL: " + error);
                Debug.LogException(error);
                Finish();
            }
        }
        private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish()
        {
            PlayerPrefs.DeleteKey(TestKey); PlayerPrefs.Save();
            SessionState.SetBool(Pending, false); stage = 0; deadline = 0;
            EditorApplication.isPlaying = false;
        }
    }
}
