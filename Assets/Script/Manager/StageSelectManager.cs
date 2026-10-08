
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class StageSelectManager : MonoBehaviour
{
    [Header("ステージ一覧")]
    [SerializeField] private ScrollRect stageScrollRect;
    [SerializeField] private RectTransform[] stageButtons;

    [Header("Scene遷移")]
    [SerializeField] private string gameSceneName = "GameLoop";

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

    private bool isDecided;
    private bool isPointerDown;
    private bool isSwiping;
    private bool isTouchInput;

    private Vector2 startPosition;
    private Vector2 currentPosition;

    private RectTransform pressedButton;

    private void Start()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(stageSelectBGMName);
        }
    }

    private void Update()
    {
        if (isDecided) return;

        if (SceneTransitionManager.Instance != null &&
            SceneTransitionManager.Instance.IsTransitioning)
        {
            return;
        }

        UpdatePointerInput();
    }

    private void UpdatePointerInput()
    {
        bool down = false;
        bool held = false;
        bool up = false;

        Vector2 position = Vector2.zero;

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

    private void BeginPointer(Vector2 position)
    {
        isPointerDown = true;
        isSwiping = false;

        startPosition = position;
        currentPosition = position;

        pressedButton = GetStageButton(position);

        // スワイプの場合も最初の接触で一度だけ鳴らす。
        // ボタン以外から触れた場合は非決定SE。
        if (pressedButton == null)
        {
            PlayNonDecisionSE();
        }
    }

    private void MovePointer(Vector2 position)
    {
        currentPosition = position;

        float distance =
            Vector2.Distance(startPosition, currentPosition);

        if (distance >= swipeThreshold)
        {
            if (!isSwiping)
            {
                isSwiping = true;

                // ボタン上からスワイプした場合も
                // 非決定SEは一度だけ鳴らす。
                if (pressedButton != null)
                {
                    PlayNonDecisionSE();
                }
            }
        }
    }

    private void EndPointer(Vector2 position)
    {
        isPointerDown = false;

        float distance =
            Vector2.Distance(startPosition, position);

        if (isSwiping || distance >= swipeThreshold)
        {
            return;
        }

        // 押した場所と離した場所が
        // 同じステージボタンなら決定
        RectTransform releasedButton =
            GetStageButton(position);

        if (pressedButton != null &&
            pressedButton == releasedButton)
        {
            DecideStage(pressedButton);
        }
    }

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

    private void DecideStage(RectTransform selectedButton)
    {
        if (isDecided) return;

        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogError("SceneTransitionManagerが存在しません。");
            return;
        }

        isDecided = true;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(decisionSEName);
        }

        StartCoroutine(DecideRoutine(selectedButton));
    }

    private IEnumerator DecideRoutine(RectTransform selectedButton)
    {
        Vector3 originalScale = selectedButton.localScale;
        Vector3 targetScale = originalScale * decidedScale;

        yield return ScaleRoutine(
            selectedButton, originalScale, targetScale);

        yield return ScaleRoutine(
            selectedButton, targetScale, originalScale);

        selectedButton.localScale = originalScale;

        SceneTransitionManager.Instance.ChangeScene(
            gameSceneName,
            StopStageSelectBGM);
    }

    private IEnumerator ScaleRoutine(
        RectTransform target,
        Vector3 from,
        Vector3 to)
    {
        float timer = 0f;

        while (timer < shrinkDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / shrinkDuration);

            target.localScale = Vector3.Lerp(from, to, t);

            yield return null;
        }

        target.localScale = to;
    }

    private void StopStageSelectBGM()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
        }
    }

    private void PlayNonDecisionSE()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(nonDecisionSEName);
        }
    }
}
