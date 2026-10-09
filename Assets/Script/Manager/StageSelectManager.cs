
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class StageSelectManager : MonoBehaviour
{
    [Header("ステージ一覧")]
    [SerializeField] private ScrollRect stageScrollRect;
    [SerializeField] private RectTransform[] stageButtons;

    [Header("Canvas切り替え")]
    [SerializeField] private GameObject stageSelectCanvas;
    [SerializeField] private GameObject stageDetailCanvas;

    [Header("ステージ詳細画面のボタン")]
    [SerializeField] private Button goButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button playerIconImageButton;

    [Header("キャラクター選択")]
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private Button csPlayerIcon;

    [Header("Scene遷移")]
    [SerializeField] private string gameSceneName = "MainScene";

    [Header("BGM")]
    [SerializeField] private string stageSelectBGMName = "StageSelectBGM";

    [Header("SE")]
    [SerializeField] private string decisionSEName = "DecisionSE";
    [SerializeField] private string nonDecisionSEName = "NonDecisionSE";

    [Header("決定演出")]
    [SerializeField, Range(0.1f, 1f)]
    private float decidedScale = 0.75f;

    [SerializeField, Min(0.01f)]
    private float shrinkDuration = 0.1f;

    [Header("スワイプ判定")]
    [SerializeField, Min(1f)]
    private float swipeThreshold = 20f;

    // 現在の画面状態
    private enum ScreenState
    {
        StageSelect,
        StageDetail,
        CharacterSelect
    }

    private ScreenState currentScreen = ScreenState.StageSelect;

    // ボタン演出中の多重操作防止
    private bool isDecided;

    // スワイプ判定用
    private bool isPointerDown;
    private bool isSwiping;
    private bool isTouchInput;
    private Vector2 startPosition;
    private Vector2 currentPosition;
    private RectTransform pressedButton;

    // 現在選択しているステージ番号
    private int selectedStageIndex = -1;

    private void Start()
    {
        // 最初はステージ一覧だけ表示
        stageSelectCanvas.SetActive(true);
        stageDetailCanvas.SetActive(false);
        characterSelectPanel.SetActive(false);

        currentScreen = ScreenState.StageSelect;
        isDecided = false;

        // 詳細画面のボタン登録
        goButton.onClick.AddListener(OnGoButton);
        backButton.onClick.AddListener(OnBackButton);
        playerIconImageButton.onClick.AddListener(OnPlayerIconButton);
        csPlayerIcon.onClick.AddListener(OnCSPlayerIconButton);

        // StageSelectBGM再生
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(stageSelectBGMName);
        }
    }

    private void OnDestroy()
    {
        // イベント登録を解除
        if (goButton != null)
            goButton.onClick.RemoveListener(OnGoButton);

        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackButton);

        if (playerIconImageButton != null)
            playerIconImageButton.onClick.RemoveListener(OnPlayerIconButton);

        if (csPlayerIcon != null)
            csPlayerIcon.onClick.RemoveListener(OnCSPlayerIconButton);
    }

    private void Update()
    {
        if (isDecided) return;

        if (SceneTransitionManager.Instance != null &&
            SceneTransitionManager.Instance.IsTransitioning)
        {
            return;
        }

        // 画面ごとに入力処理を切り替える
        if (currentScreen == ScreenState.StageSelect)
        {
            UpdateStageSelectInput();
        }
        else
        {
            UpdateDetailInput();
        }
    }

    // ============================================================
    // ステージ一覧の入力
    // ============================================================
    private void UpdateStageSelectInput()
    {
        ReadPointerInput(
            out bool down,
            out bool held,
            out bool up,
            out Vector2 position);

        if (down)
        {
            BeginPointer(position);
        }

        if (held && isPointerDown)
        {
            MovePointer(position);
        }

        if (up && isPointerDown)
        {
            EndPointer(position);
        }
    }

    // ============================================================
    // ステージ詳細画面の入力
    // ============================================================
    private void UpdateDetailInput()
    {
        ReadPointerInput(
            out bool down,
            out bool held,
            out bool up,
            out Vector2 position);

        if (!down) return;

        // ボタン以外なら非決定SEを再生
        if (!IsPointerOverDetailButton(position))
        {
            PlayNonDecisionSE();
        }
    }

    // ============================================================
    // マウス・タッチ入力の共通取得
    // ============================================================
    private void ReadPointerInput(
        out bool down,
        out bool held,
        out bool up,
        out Vector2 position)
    {
        down = false;
        held = false;
        up = false;
        position = Vector2.zero;

        // スマートフォンのタッチを優先
        if (Touchscreen.current != null &&
            (Touchscreen.current.primaryTouch.press.isPressed ||
             Touchscreen.current.primaryTouch.press.wasReleasedThisFrame ||
             Touchscreen.current.primaryTouch.press.wasPressedThisFrame))
        {
            var touch = Touchscreen.current.primaryTouch;

            down = touch.press.wasPressedThisFrame;
            held = touch.press.isPressed;
            up = touch.press.wasReleasedThisFrame;

            position = touch.position.ReadValue();
            isTouchInput = true;
        }
        else if (!isTouchInput && Mouse.current != null)
        {
            var mouse = Mouse.current;

            down = mouse.leftButton.wasPressedThisFrame;
            held = mouse.leftButton.isPressed;
            up = mouse.leftButton.wasReleasedThisFrame;

            position = mouse.position.ReadValue();
        }
        else if (isTouchInput)
        {
            isTouchInput = false;
        }
    }

    // ============================================================
    // ステージ一覧：タッチ開始
    // ============================================================
    private void BeginPointer(Vector2 position)
    {
        isPointerDown = true;
        isSwiping = false;

        startPosition = position;
        currentPosition = position;

        pressedButton = GetStageButton(position);

        // ボタン以外に触れた場合は非決定SE
        if (pressedButton == null)
        {
            PlayNonDecisionSE();
        }
    }

    // ============================================================
    // ステージ一覧：スワイプ判定
    // ============================================================
    private void MovePointer(Vector2 position)
    {
        currentPosition = position;

        float distance =
            Vector2.Distance(startPosition, currentPosition);

        if (distance >= swipeThreshold && !isSwiping)
        {
            isSwiping = true;

            // ボタン上からスワイプした場合は
            // このタイミングで非決定SEを1回だけ鳴らす
            if (pressedButton != null)
            {
                PlayNonDecisionSE();
            }
        }
    }

    // ============================================================
    // ステージ一覧：タッチ終了
    // ============================================================
    private void EndPointer(Vector2 position)
    {
        isPointerDown = false;

        float distance =
            Vector2.Distance(startPosition, position);

        // スワイプならステージ決定しない
        if (isSwiping || distance >= swipeThreshold)
        {
            return;
        }

        RectTransform releasedButton = GetStageButton(position);

        // 同じボタン上で指を離したらステージ決定
        if (pressedButton != null &&
            pressedButton == releasedButton)
        {
            DecideStage(pressedButton);
        }
    }

    // ============================================================
    // タップしたステージボタンを取得
    // ============================================================
    private RectTransform GetStageButton(Vector2 position)
    {
        if (stageButtons == null) return null;

        Canvas canvas = stageScrollRect != null
            ? stageScrollRect.GetComponentInParent<Canvas>()
            : null;

        Camera uiCamera = null;

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        foreach (RectTransform button in stageButtons)
        {
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;

            // Viewportの外側は選択不可
            if (stageScrollRect != null &&
                stageScrollRect.viewport != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    stageScrollRect.viewport, position, uiCamera))
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(
                button, position, uiCamera))
            {
                return button;
            }
        }

        return null;
    }

    // ============================================================
    // ステージ選択 → 詳細画面
    // ============================================================
    private void DecideStage(RectTransform selectedButton)
    {
        if (isDecided) return;

        isDecided = true;

        // 選択されたステージ番号を保存
        selectedStageIndex =
            System.Array.IndexOf(stageButtons, selectedButton);

        PlayDecisionSE();

        StartCoroutine(OpenStageDetailRoutine(selectedButton));
    }

    private IEnumerator OpenStageDetailRoutine(RectTransform button)
    {
        // ボタン縮小 → 復元
        yield return PlayButtonAnimation(button);

        // ステージ一覧を非表示
        stageSelectCanvas.SetActive(false);

        // ステージ詳細を表示
        stageDetailCanvas.SetActive(true);

        // キャラクター選択画面は閉じておく
        characterSelectPanel.SetActive(false);

        currentScreen = ScreenState.StageDetail;
        isPointerDown = false;
        isDecided = false;

        Debug.Log("選択ステージ : " + (selectedStageIndex + 1));
    }

    // ============================================================
    // GOボタン → MainSceneへ遷移
    // ============================================================
    private void OnGoButton()
    {
        if (isDecided ||
            currentScreen != ScreenState.StageDetail)
        {
            return;
        }

        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogError("SceneTransitionManagerが存在しません。");
            return;
        }

        isDecided = true;
        PlayDecisionSE();

        StartCoroutine(GoRoutine());
    }

    private IEnumerator GoRoutine()
    {
        yield return PlayButtonAnimation(goButton.transform);

        // フェードアウトしてMainSceneへ
        SceneTransitionManager.Instance.ChangeScene(
            gameSceneName,
            StopStageSelectBGM);
    }

    // ============================================================
    // Backボタン → ステージ一覧へ戻る
    // ============================================================
    private void OnBackButton()
    {
        if (isDecided ||
            currentScreen != ScreenState.StageDetail)
        {
            return;
        }

        isDecided = true;
        PlayDecisionSE();

        StartCoroutine(BackRoutine());
    }

    private IEnumerator BackRoutine()
    {
        yield return PlayButtonAnimation(backButton.transform);

        // 詳細画面を非表示
        stageDetailCanvas.SetActive(false);

        // ステージ一覧を再表示
        stageSelectCanvas.SetActive(true);

        currentScreen = ScreenState.StageSelect;
        isPointerDown = false;
        isDecided = false;
    }

    // ============================================================
    // PlayerIcon → CharacterSelectPanelを表示
    // ※ このボタンだけ縮小演出なし
    // ============================================================
    private void OnPlayerIconButton()
    {
        if (isDecided ||
            currentScreen != ScreenState.StageDetail)
        {
            return;
        }

        PlayDecisionSE();

        characterSelectPanel.SetActive(true);
        currentScreen = ScreenState.CharacterSelect;
    }

    // ============================================================
    // CSPlayerIcon → CharacterSelectPanelを閉じる
    // ※ キャラクターデータ変更は未実装
    // ============================================================
    private void OnCSPlayerIconButton()
    {
        if (isDecided ||
            currentScreen != ScreenState.CharacterSelect)
        {
            return;
        }

        PlayDecisionSE();

        characterSelectPanel.SetActive(false);
        currentScreen = ScreenState.StageDetail;
    }

    // ============================================================
    // 詳細画面：ボタン上かどうか確認
    // ============================================================
    private bool IsPointerOverDetailButton(Vector2 position)
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current);

        pointerData.position = position;

        var results =
            new System.Collections.Generic.List<RaycastResult>();

        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            Transform target = result.gameObject.transform;

            // キャラクター選択画面表示中
            if (currentScreen == ScreenState.CharacterSelect)
            {
                if (IsChildOrSelf(target, csPlayerIcon.transform))
                    return true;
            }
            else
            {
                if (IsChildOrSelf(target, goButton.transform) ||
                    IsChildOrSelf(target, backButton.transform) ||
                    IsChildOrSelf(target, playerIconImageButton.transform))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsChildOrSelf(Transform target, Transform parent)
    {
        return target == parent || target.IsChildOf(parent);
    }

    // ============================================================
    // 共通ボタン縮小・復元演出
    // ============================================================
    private IEnumerator PlayButtonAnimation(Transform target)
    {
        Vector3 originalScale = target.localScale;
        Vector3 smallScale = originalScale * decidedScale;

        yield return ScaleRoutine(
            target, originalScale, smallScale);

        yield return ScaleRoutine(
            target, smallScale, originalScale);

        target.localScale = originalScale;
    }

    private IEnumerator ScaleRoutine(
        Transform target,
        Vector3 from,
        Vector3 to)
    {
        float timer = 0f;

        while (timer < shrinkDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(timer / shrinkDuration);

            target.localScale =
                Vector3.Lerp(from, to, t);

            yield return null;
        }

        target.localScale = to;
    }

    // ============================================================
    // SE再生
    // ============================================================
    private void PlayDecisionSE()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(decisionSEName);
        }
    }

    private void PlayNonDecisionSE()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(nonDecisionSEName);
        }
    }

    // ============================================================
    // シーン遷移時にBGM停止
    // ============================================================
    private void StopStageSelectBGM()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
        }
    }
}
