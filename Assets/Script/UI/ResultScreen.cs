using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Prototype
{
    public sealed class ResultScreen : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/ResultScene.unity";
        [SerializeField] private Text resultText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text stockText;
        [SerializeField] private Button retryButton;
        [SerializeField] private string defaultRetryScene = "MainScene";

        // 直前の一戦だけを保持。シーン単体のプレビューでは記録を空欄にする。
        private static bool hasResult;
        private static bool cleared;
        private static float seconds;
        private static int stock;
        private static string retryScene;
        private bool loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            hasResult = false;
            retryScene = null;
        }

        public static void Record(bool didClear, float elapsedSeconds, int remainingStock, string sourceScene)
        {
            hasResult = true;
            cleared = didClear;
            seconds = Mathf.Max(0f, elapsedSeconds);
            stock = Mathf.Max(0, remainingStock);
            retryScene = sourceScene;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            resultText.text = !hasResult || cleared ? "CLEAR!" : "GAME OVER";
            resultText.color = !hasResult || cleared
                ? new Color32(241, 227, 152, 255) : new Color32(237, 137, 125, 255);
            int hundredths = Mathf.FloorToInt(seconds * 100f);
            timeText.text = hasResult
                ? $"{hundredths / 6000:00}:{hundredths / 100 % 60:00}.{hundredths % 100:00}"
                : "--:--.--";
            stockText.text = hasResult ? stock.ToString() : "--";
            retryButton.onClick.AddListener(Retry);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
        }

        public void Retry()
        {
            if (loading) return;
            string destination = hasResult && !string.IsNullOrEmpty(retryScene) ? retryScene : defaultRetryScene;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError($"Retry scene is not in Build Settings: {destination}", this);
                return;
            }
            loading = true;
            retryButton.interactable = false;
            hasResult = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(destination);
        }

        private void OnDestroy()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
        }
    }
}
