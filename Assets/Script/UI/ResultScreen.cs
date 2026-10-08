using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Prototype
{
    public sealed class ResultScreen : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/ResultScene.unity";
        [SerializeField] private Text scoreText, timeText, stockText, timeBonusText, stockBonusText, hintText;
        [SerializeField] private GameObject breakdown, ranking;
        [SerializeField] private Button revealButton, nextButton;
        [SerializeField] private RectTransform rankingContent;
        [SerializeField] private GameObject rowTemplate;
        [SerializeField] private Text emptyText;
        [SerializeField] private ScrollRect rankingScroll;
        [Header("PDFのスコア表（調整用）")]
        [SerializeField] private float[] timeLimits = { 120f, 300f, 480f };
        [SerializeField] private int[] timeBonuses = { 10000000, 8000000, 500000, 100000 };
        [Tooltip("0機・1機・2機・3機の順")]
        [SerializeField] private int[] stockBonuses = { 100000, 500000, 8000000, 10000000 };
        [SerializeField, Min(0.01f)] private float countSeconds = 1.2f;
        [SerializeField] private string nextScene = "MainScene";
        [SerializeField] private string rankingKey = "Prototype.ResultRanking.v1";
        [Serializable] private sealed class Entry { public string id; public int score; }
        [Serializable] private sealed class Records { public List<Entry> entries = new List<Entry>(); }
        private static bool hasResult, cleared;
        private static float seconds;
        private static int stock, baseScore;
        private static string roundId;
        private Coroutine animation;
        private bool complete, showingRanking, loading;
        private int timeBonus, stockBonus, total;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { hasResult = false; roundId = null; }

        public static void Record(bool didClear, float elapsedSeconds, int remainingStock, string sourceScene, int earnedScore = 0)
        {
            hasResult = true; cleared = didClear;
            seconds = Mathf.Max(0, elapsedSeconds); stock = Mathf.Max(0, remainingStock);
            baseScore = Mathf.Max(0, earnedScore); roundId = Guid.NewGuid().ToString("N");
        }
        public int CalculateTimeBonus(float elapsed)
        {
            for (int i = 0; i < timeLimits.Length && i < timeBonuses.Length; i++)
                if (elapsed <= timeLimits[i]) return timeBonuses[i];
            return timeBonuses.Length > 0 ? timeBonuses[timeBonuses.Length - 1] : 0;
        }
        public int CalculateStockBonus(int remaining)
        {
            return stockBonuses.Length == 0 ? 0 : stockBonuses[Mathf.Clamp(remaining, 0, stockBonuses.Length - 1)];
        }
        private void Start()
        {
            Time.timeScale = 1;
            ranking.SetActive(false); nextButton.gameObject.SetActive(false); breakdown.SetActive(true);
            revealButton.gameObject.SetActive(true);
            revealButton.onClick.AddListener(Advance); nextButton.onClick.AddListener(Retry);
            int ticks = Mathf.FloorToInt(seconds * 100);
            timeText.text = hasResult ? $"{ticks / 6000:00}:{ticks / 100 % 60:00}.{ticks % 100:00}" : "--:--.--";
            stockText.text = hasResult ? $"残り残機  {stock}個" : "残り残機  --個";
            timeBonus = hasResult && cleared ? CalculateTimeBonus(seconds) : 0;
            stockBonus = hasResult && cleared ? CalculateStockBonus(stock) : 0;
            total = (hasResult ? baseScore : 0) + timeBonus + stockBonus;
            timeBonusText.text = $"+{timeBonus:N0}"; stockBonusText.text = $"+{stockBonus:N0}";
            scoreText.text = (hasResult ? baseScore : 0).ToString("D8");
            if (hasResult) animation = StartCoroutine(CountScore()); else FinishCount();
        }
        private IEnumerator CountScore()
        {
            hintText.text = "タップでスキップ";
            yield return Count(baseScore, baseScore + timeBonus, timeBonusText);
            yield return Count(baseScore + timeBonus, total, stockBonusText);
            FinishCount();
        }
        private IEnumerator Count(int from, int to, Text label)
        {
            Color original = label.color;
            label.color = new Color32(180, 99, 24, 255);
            float elapsed = 0;
            while (elapsed < countSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, countSeconds));
                scoreText.text = Mathf.RoundToInt(Mathf.Lerp(from, to, 1 - (1 - t) * (1 - t))).ToString("D8");
                yield return null;
            }
            label.color = original;
        }
        private void FinishCount()
        {
            complete = true; scoreText.text = total.ToString("D8");
            timeBonusText.color = stockBonusText.color = new Color32(37, 73, 85, 255);
            hintText.text = "タップでランキングを表示";
        }
        public void Advance()
        {
            if (showingRanking) return;
            if (!complete)
            {
                if (animation != null) StopCoroutine(animation);
                FinishCount(); return;
            }
            showingRanking = true;
            breakdown.SetActive(false); revealButton.gameObject.SetActive(false);
            ranking.SetActive(true); nextButton.gameObject.SetActive(true); hintText.text = "";
            ShowRanking();
        }
        private void ShowRanking()
        {
            Records records;
            try { records = JsonUtility.FromJson<Records>(PlayerPrefs.GetString(rankingKey, "")) ?? new Records(); }
            catch (ArgumentException) { records = new Records(); }
            if (records.entries == null) records.entries = new List<Entry>();
            records.entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id) || e.score < 0);
            if (hasResult && cleared && !records.entries.Exists(e => e.id == roundId))
            {
                records.entries.Add(new Entry { id = roundId, score = total });
                records.entries.Sort((a, b) => b.score.CompareTo(a.score));
                if (records.entries.Count > 100) records.entries.RemoveRange(100, records.entries.Count - 100);
                PlayerPrefs.SetString(rankingKey, JsonUtility.ToJson(records)); PlayerPrefs.Save();
            }
            emptyText.gameObject.SetActive(records.entries.Count == 0);
            rankingContent.sizeDelta = new Vector2(0, Mathf.Max(560, records.entries.Count * 126f));
            int myIndex = -1;
            for (int i = 0; i < records.entries.Count; i++)
            {
                var entry = records.entries[i]; bool mine = hasResult && entry.id == roundId;
                if (mine) myIndex = i;
                var row = Instantiate(rowTemplate, rankingContent); row.name = "Rank " + (i + 1); row.SetActive(true);
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -i * 126f);
                row.transform.Find("Name").GetComponent<Text>().text = $"{i + 1}位  {(mine ? "あなた" : "プレイヤー")}";
                row.transform.Find("Score").GetComponent<Text>().text = entry.score.ToString("D8");
                row.GetComponent<Image>().color = mine ? new Color32(239, 248, 217, 255) : new Color32(214, 237, 246, 255);
            }
            Canvas.ForceUpdateCanvases();
            float excess = rankingContent.rect.height - rankingScroll.viewport.rect.height;
            rankingScroll.verticalNormalizedPosition = excess > 0 && myIndex >= 0 ? 1 - Mathf.Clamp01(myIndex * 126f / excess) : 1;
        }
        public void Retry()
        {
            if (loading || !Application.CanStreamedLevelBeLoaded(nextScene)) return;
            loading = true; hasResult = false; nextButton.interactable = false; Time.timeScale = 1;
            SceneManager.LoadScene(nextScene);
        }
    }
}

