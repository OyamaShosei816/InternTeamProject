using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Prototype;

public class GameOverManager : MonoBehaviour
{
    [Header("ゲーム管理")]
    [SerializeField] private PrototypeArena arena;
    [SerializeField] private DragPlayer player;
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private GameObject gameOverUICanvas;

    [Header("GameOverUIフェード")]
    [SerializeField] private CanvasGroup gameOverCanvasGroup;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;

    [Header("GameOver演出")]
    [SerializeField] private string playerDeathSEName = "PlayerDeathSE";
    [SerializeField] private string gameOverSEName = "GameOverSE";
    [SerializeField] private string stageBGMName = "Stage1BGM";
    [SerializeField] private float gameOverDelay = 1.0f;

    [Header("GameOverボタン")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button retireButton;

    [Header("共通SE")]
    [SerializeField] private string decisionSEName = "DecisionSE";
    [SerializeField] private string nonDecisionSEName = "NonDecisionSE";

    [Header("ボタン縮小演出")]
    [SerializeField, Range(0.1f, 1f)] private float decidedScale = 0.75f;
    [SerializeField] private float shrinkDuration = 0.15f;
    [SerializeField] private float shrinkWaitDuration = 0.1f;

    [Header("Scene遷移")]
    [SerializeField] private string homeSceneName = "Home";

    private bool isGameOver;
    private bool isDecided;

    // 初期化
    // 初期化
    private void Start()
    {
        Time.timeScale = 1f;

        // GameOverUIを透明にして非表示
        gameOverCanvasGroup.alpha = 0f;
        gameOverCanvasGroup.interactable = false;
        gameOverCanvasGroup.blocksRaycasts = false;
        gameOverUICanvas.SetActive(false);

        // 各ボタンにクリック処理を登録
        continueButton.onClick.AddListener(OnContinue);
        restartButton.onClick.AddListener(OnRestart);
        retireButton.onClick.AddListener(OnRetire);
    }

    // GameOver画面表示中の入力判定
    private void Update()
    {
        // GameOverUI表示完了後のみ入力を受け付ける
        if (!isGameOver || isDecided ||
            !gameOverUICanvas.activeInHierarchy ||
            !gameOverCanvasGroup.interactable)
        {
            return;
        }

        // マウス左クリックを検出
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckNonDecision(Mouse.current.position.ReadValue(), -1);
        }

        // スマートフォンのタッチ入力を検出
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    CheckNonDecision(touch.position.ReadValue(), touch.touchId.ReadValue());
                }
            }
        }
    }

    // ボタン以外を押した場合の非決定SE
    private void CheckNonDecision(Vector2 screenPosition, int pointerId)
    {
        if (EventSystem.current == null) return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = screenPosition;
        pointerData.pointerId = pointerId;

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        // クリック位置に3つのボタンが存在するか確認
        foreach (RaycastResult result in results)
        {
            Transform target = result.gameObject.transform;

            if (target == continueButton.transform || target.IsChildOf(continueButton.transform) ||
                target == restartButton.transform || target.IsChildOf(restartButton.transform) ||
                target == retireButton.transform || target.IsChildOf(retireButton.transform))
            {
                return;
            }
        }

        // ボタン以外なら非決定SEを再生
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(nonDecisionSEName);
        }
    }

    // GameOver開始
    public void StartGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;
        isDecided = false;
        Time.timeScale = 0f;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
            SoundManager.Instance.PlaySE(playerDeathSEName);
        }

        // プレイヤーを非表示
        if (playerRenderer != null)
        {
            playerRenderer.enabled = false;
        }

        StartCoroutine(GameOverRoutine());
    }

    // GameOverUIをフェードイン表示
    private IEnumerator GameOverRoutine()
    {
        // GameOver演出開始まで待機
        yield return new WaitForSecondsRealtime(gameOverDelay);

        // GameOverSE再生
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(gameOverSEName);
        }

        // UIを透明な状態で表示
        gameOverCanvasGroup.alpha = 0f;
        gameOverCanvasGroup.interactable = false;
        gameOverCanvasGroup.blocksRaycasts = false;
        gameOverUICanvas.SetActive(true);

        // フェードイン開始
        float timer = 0f;

        while (timer < fadeInDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(fadeInDuration, 0.001f));

            // 透明から徐々に表示
            gameOverCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        // フェードイン完了
        gameOverCanvasGroup.alpha = 1f;

        // ボタン操作を許可
        gameOverCanvasGroup.interactable = true;
        gameOverCanvasGroup.blocksRaycasts = true;
    }

    // Continueボタン
    private void OnContinue()
    {
        Decide(continueButton, 0);
    }

    // Restartボタン
    private void OnRestart()
    {
        Decide(restartButton, 1);
    }

    // Retireボタン
    private void OnRetire()
    {
        Decide(retireButton, 2);
    }

    // ボタン決定処理
    private void Decide(Button button, int action)
    {
        if (!isGameOver || isDecided) return;

        if (action != 0 && SceneTransitionManager.Instance == null)
        {
            Debug.LogError("SceneTransitionManagerが存在しません。");
            return;
        }

        isDecided = true;

        // 決定SEを再生
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(decisionSEName);
        }

        // ボタン演出終了後に決定処理
        StartCoroutine(DecideRoutine(button.transform, action));
    }

    // ボタン縮小 → 維持 → 復元 → 決定処理
    private IEnumerator DecideRoutine(Transform target, int action)
    {
        Vector3 originalScale = target.localScale;
        Vector3 smallScale = originalScale * decidedScale;

        // ボタンを徐々に縮小
        float timer = 0f;

        while (timer < shrinkDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(shrinkDuration, 0.001f));

            target.localScale = Vector3.Lerp(originalScale, smallScale, t);
            yield return null;
        }

        target.localScale = smallScale;

        // 縮小状態を少し維持
        yield return new WaitForSecondsRealtime(shrinkWaitDuration);

        // ボタンを元の大きさに戻す
        timer = 0f;

        while (timer < shrinkDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(shrinkDuration, 0.001f));

            target.localScale = Vector3.Lerp(smallScale, originalScale, t);
            yield return null;
        }

        target.localScale = originalScale;

        // 復元完了後に決定処理を実行
        switch (action)
        {
            case 0:
                ExecuteContinue();
                break;

            case 1:
                ExecuteRestart();
                break;

            case 2:
                ExecuteRetire();
                break;
        }
    }

    // 死亡位置から復活
    private void ExecuteContinue()
    {
        arena.ContinueGame();

        if (playerRenderer != null)
        {
            playerRenderer.enabled = true;
        }
        // GameOverUIを非表示にして初期状態へ戻す
        gameOverCanvasGroup.alpha = 0f;
        gameOverCanvasGroup.interactable = false;
        gameOverCanvasGroup.blocksRaycasts = false;
        gameOverUICanvas.SetActive(false);

        Time.timeScale = 1f;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(stageBGMName);
        }

        isGameOver = false;
        isDecided = false;
    }

    // Sceneを最初からやり直す
    private void ExecuteRestart()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        Time.timeScale = 1f;
        SceneTransitionManager.Instance.ChangeScene(sceneName);
    }

    // HomeSceneへ戻る
    private void ExecuteRetire()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.ChangeScene(homeSceneName);
    }
}