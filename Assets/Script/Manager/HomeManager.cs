using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================
// HomeScene専用の管理クラス。
//
// 【Homeの仕様】
// ・SelectImageの範囲内をタップ
//     → 決定SE
//     → FadeOut
//     → StageSelectへ遷移
//
// ・SelectImage以外をタップ
//     → 非決定SE
//
// ・Scene遷移中は入力を受け付けない
//
// PC確認用として左クリックにも対応する。
// ===========================================
public class HomeManager : MonoBehaviour
{
    // ============================================================
    // Inspector設定
    // ============================================================
    [Header("StageSelectへの決定範囲")]
    [Tooltip("StageSelectへ遷移するSelectImageを設定")]
    [SerializeField]
    private RectTransform selectImage;

    [Header("Scene遷移")]
    [Tooltip("StageSelectのScene名")]
    [SerializeField]
    private string stageSelectSceneName = "StageSelect";

    [Header("BGM")]
    [Tooltip("SoundManagerのBGM Dataに登録したBGM名")]
    [SerializeField]
    private string homeBGMName = "HomeBGM";

    [Header("SE")]
    [Tooltip("決定したときに鳴らすSE名")]
    [SerializeField]
    private string decisionSEName = "DecisionSE";

    [Header("決定時のUI演出")]
    [Tooltip("決定したSelectImageを何倍まで縮小するか")]
    [SerializeField, Range(0.1f, 1.0f)]
    private float decidedScale = 0.75f;

    [Tooltip("決定UIが縮小するまでの時間")]
    [SerializeField, Min(0.01f)]
    private float shrinkDuration = 0.1f;

    [Tooltip("決定できない場所を押したときに鳴らすSE名")]
    [SerializeField]
    private string nonDecisionSEName = "NonDecisionSE";

    // ============================================================
    // 内部状態
    // ============================================================
    // StageSelectへの遷移を決定した後、
    // 連打によって何度も処理されるのを防ぐ。
    private bool isDecided = false;

    // ============================================================
    // Start
    // HomeSceneに入ったときの初期処理
    // ============================================================
    private void Start()
    {
        // --------------------------------------------------------
        // HomeBGM再生
        // --------------------------------------------------------
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(
                homeBGMName);
        }
        else
        {
            Debug.LogWarning(
                "HomeManager : SoundManagerが存在しない",
                this);
        }
    }

    // ============================================================
    // Update
    // ============================================================
    private void Update()
    {
        // --------------------------------------------------------
        // 既にStageSelectへの遷移を決定している場合
        // --------------------------------------------------------

        if (isDecided)
        {
            return;
        }


        // --------------------------------------------------------
        // Scene遷移中は操作禁止
        // --------------------------------------------------------

        if (SceneTransitionManager.Instance != null &&
            SceneTransitionManager.Instance.IsTransitioning)
        {
            return;
        }


        // --------------------------------------------------------
        // タップされた位置を取得
        // --------------------------------------------------------

        Vector2 pointerPosition;

        // GetPointerDownPosition()で
        // 今フレームに押されたか確認する。
        if (!GetPointerDownPosition(out pointerPosition))
        {
            return;
        }


        // --------------------------------------------------------
        // SelectImageの範囲内か確認
        // --------------------------------------------------------

        bool isInsideSelectImage =
            IsPointerInsideSelectImage(pointerPosition);


        if (isInsideSelectImage)
        {
            // SelectImageの中
            // ↓
            // 決定処理
            DecideStageSelect();
        }
        else
        {
            // SelectImage以外
            // ↓
            // 非決定SE
            PlayNonDecisionSE();
        }
    }


    // ============================================================
    // 入力取得
    // ============================================================

    // 今フレームで画面が押された場合、
    // 押された画面座標を返す。
    //
    // true
    //     → 今フレームで押された
    //
    // false
    //     → 押されていない
    private bool GetPointerDownPosition(
        out Vector2 pointerPosition)
    {
        pointerPosition = Vector2.zero;

        // --------------------------------------------------------
        // スマートフォン
        // --------------------------------------------------------
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pointerPosition =
                Touchscreen.current.primaryTouch.position.ReadValue();

            return true;
        }

        // --------------------------------------------------------
        // PC確認用
        // --------------------------------------------------------
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerPosition =
                Mouse.current.position.ReadValue();

            return true;
        }

        // 今フレームでは押されていない。
        return false;
    }

    // ============================================================
    // SelectImage判定
    // ============================================================
    // タップした位置が
    // SelectImageのRectTransform内にあるか確認する。
    private bool IsPointerInsideSelectImage(
        Vector2 pointerPosition)
    {
        // SelectImageが設定されていなければ
        // 決定判定はできない。
        if (selectImage == null)
        {
            Debug.LogWarning(
                "HomeManager : SelectImageが設定されていません。",
                this);

            return false;
        }


        // --------------------------------------------------------
        // RectTransform内判定
        // --------------------------------------------------------
        //
        // Screen Space - OverlayのCanvasなら
        // Cameraにはnullを渡して判定できる。
        //
        // SelectTextはSelectImageの子なので、
        // 文字の上を押してもSelectImageの範囲内として判定される。
        //
        return RectTransformUtility.RectangleContainsScreenPoint(
            selectImage,
            pointerPosition,
            null);
    }

    // ============================================================
    // StageSelect決定
    // ============================================================
    private void DecideStageSelect()
    {
        // --------------------------------------------------------
        // 二重実行防止
        // --------------------------------------------------------
        if (isDecided)
        {
            return;
        }

        // --------------------------------------------------------
        // SceneTransitionManager確認
        // --------------------------------------------------------
        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogError(
                "HomeManager : SceneTransitionManagerが存在しません。",
                this);

            return;
        }

        // --------------------------------------------------------
        // 決定状態にする
        // --------------------------------------------------------
        // ここから先は他のタップを受け付けない。
        isDecided = true;

        // --------------------------------------------------------
        // 決定SE
        // --------------------------------------------------------
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(
                decisionSEName);
        }
        else
        {
            Debug.LogWarning(
                "HomeManager : SoundManagerが存在しません。",
                this);
        }

        // --------------------------------------------------------
        // 決定UI縮小
        // ↓
        // FadeOut
        // ↓
        // StageSelect
        // --------------------------------------------------------
        StartCoroutine(
            DecideStageSelectRoutine());
    }

    // ============================================================
    // 決定時のUI演出 → Scene遷移
    // ============================================================
    private IEnumerator DecideStageSelectRoutine()
    {
        // --------------------------------------------------------
        // SelectImageが設定されている場合だけ
        // 決定UIの縮小 → 元サイズへ戻す演出を行う
        // --------------------------------------------------------
        if (selectImage != null)
        {
            // ----------------------------------------------------
            // 最初の大きさを保存
            // ----------------------------------------------------
            // InspectorでScaleを変更していても対応できるように
            // Vector3.oneではなく現在のScaleを保存する。
            Vector3 startScale =
                selectImage.localScale;

            // ----------------------------------------------------
            // 縮小後の大きさを計算
            // ----------------------------------------------------
            // decidedScale = 0.75 の場合
            //
            // 元サイズ
            // (1.0, 1.0, 1.0)
            //
            // ↓
            //
            // 縮小サイズ
            // (0.75, 0.75, 0.75)
            //
            Vector3 targetScale =
                startScale * decidedScale;

            // ====================================================
            // ① 元サイズ → 0.75倍まで縮小
            // ====================================================
            float timer = 0.0f;

            while (timer < shrinkDuration)
            {
                // Time.timeScaleの影響を受けない時間を使用する。
                timer += Time.unscaledDeltaTime;

                // 0～1の進行度
                float t =
                    Mathf.Clamp01(
                        timer / shrinkDuration);

                // 元サイズから縮小サイズへ
                // 徐々に変化させる。
                selectImage.localScale =
                    Vector3.Lerp(
                        startScale,
                        targetScale,
                        t);

                yield return null;
            }

            // 誤差が残らないように
            // 最後は確実に0.75倍にする。
            selectImage.localScale =
                targetScale;

            // ====================================================
            // ② 0.75倍 → 元サイズまで戻す
            // ====================================================
            timer = 0.0f;

            while (timer < shrinkDuration)
            {
                timer += Time.unscaledDeltaTime;


                // 0～1の進行度
                float t =
                    Mathf.Clamp01(
                        timer / shrinkDuration);


                // 縮小サイズから元サイズへ
                // 徐々に戻していく。
                selectImage.localScale =
                    Vector3.Lerp(
                        targetScale,
                        startScale,
                        t);

                yield return null;
            }

            // ----------------------------------------------------
            // 最後は必ず元サイズに戻す
            // ----------------------------------------------------
            selectImage.localScale =
                startScale;
        }

        // ========================================================
        // ③ UI演出が完全に終了してからScene遷移
        // ========================================================
        //
        // 決定
        // ↓
        // 1.0 → 0.75
        // ↓
        // 0.75 → 1.0
        // ↓
        // FadeOut
        // ↓
        // StageSelect
        //
        SceneTransitionManager.Instance.ChangeScene(
            stageSelectSceneName,
            StopHomeBGM);
    }

    // ============================================================
    // HomeBGM停止
    // FadeOutが完全に終了して
    // 画面が真っ黒になったタイミングで呼ばれる。
    // TitleSceneと同じ仕様で、
    // 暗転が完了するまではHomeBGMを流し続ける。
    // ============================================================
    private void StopHomeBGM()
    {
        // SoundManagerが存在しない場合は
        // 停止処理を行えないので終了する。
        if (SoundManager.Instance == null)
        {
            return;
        }

        // 現在再生しているHomeBGMを停止する。
        SoundManager.Instance.StopBGM();
    }

    // ============================================================
    // 非決定SE
    // SelectImage以外をタップしたときに
    // 非決定SEを再生する。
    // ============================================================
    private void PlayNonDecisionSE()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning(
                "HomeManager : SoundManagerが存在しません。",
                this);

            return;
        }


        SoundManager.Instance.PlaySE(
            nonDecisionSEName);
    }
}