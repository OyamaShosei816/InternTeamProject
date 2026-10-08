using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TitleScene専用の処理。
///
/// 【Titleの仕様】
/// ・Title開始時にTitleBGMを再生
/// ・画面のどこをタップしても決定扱い
/// ・タップ時に決定SEを再生
/// ・フェードアウト開始
/// ・画面が完全暗転したらTitleBGMを停止
/// ・その後HomeへScene遷移
/// </summary>
public class TitleController : MonoBehaviour
{
    // ============================================================
    // Inspector設定
    // ============================================================

    [Header("Scene遷移")]
    [Tooltip("Titleから遷移するScene名")]
    [SerializeField]
    private string nextSceneName = "Home";


    [Header("SE")]
    [Tooltip("SoundManagerのSE Dataに登録した決定SEの名前")]
    [SerializeField]
    private string decisionSEName = "DecisionSE";


    [Header("BGM")]
    [Tooltip("SoundManagerのBGM Dataに登録したTitleBGMの名前")]
    [SerializeField]
    private string titleBGMName = "TitleBGM";


    // ============================================================
    // 内部状態
    // ============================================================

    // 一度決定した後の連打防止
    private bool isDecided = false;


    // ============================================================
    // Start
    // ============================================================

    private void Start()
    {
        // --------------------------------------------------------
        // TitleBGM再生
        // --------------------------------------------------------

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(
                titleBGMName);
        }
        else
        {
            Debug.LogWarning(
                "TitleController : SoundManagerが存在しません。",
                this);
        }
    }


    // ============================================================
    // Update
    // ============================================================

    private void Update()
    {
        // --------------------------------------------------------
        // 既に決定済みなら入力を受け付けない
        // --------------------------------------------------------

        if (isDecided)
        {
            return;
        }


        // --------------------------------------------------------
        // Scene遷移中なら入力を受け付けない
        // --------------------------------------------------------

        if (SceneTransitionManager.Instance != null &&
            SceneTransitionManager.Instance.IsTransitioning)
        {
            return;
        }


        // --------------------------------------------------------
        // 今フレームで画面が押されたか確認
        // --------------------------------------------------------

        bool pressed = false;


        // PC確認用
        // マウス左クリック
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressed = true;
        }


        // Android / スマートフォン用
        // 画面タップ
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pressed = true;
        }


        // 入力されていなければ何もしない
        if (!pressed)
        {
            return;
        }


        // --------------------------------------------------------
        // Title決定
        // --------------------------------------------------------

        Decide();
    }


    // ============================================================
    // Title決定
    // ============================================================

    /// <summary>
    /// Title画面をタップしたときに一度だけ実行する。
    /// </summary>
    private void Decide()
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
                "TitleController : SceneTransitionManagerが存在しません。",
                this);

            return;
        }


        // --------------------------------------------------------
        // 決定状態
        // --------------------------------------------------------

        // ここから先は入力を禁止する
        isDecided = true;


        // --------------------------------------------------------
        // 決定SE再生
        // --------------------------------------------------------

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(
                decisionSEName);
        }


        // --------------------------------------------------------
        // FadeOut → BGM停止 → Scene変更
        // --------------------------------------------------------

        SceneTransitionManager.Instance.ChangeScene(
            nextSceneName,
            StopTitleBGM);
    }

    // ============================================================
    // TitleBGM停止
    // ============================================================
    /// <summary>
    /// FadeOutが完全に終了して
    /// 画面が真っ黒になったタイミングで呼ばれる。
    /// </summary>
    private void StopTitleBGM()
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.StopBGM();
    }
}