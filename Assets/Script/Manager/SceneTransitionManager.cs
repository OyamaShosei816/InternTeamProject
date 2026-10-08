using System;
using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =================================================
// 仕様：
// 全Scene共通のScene遷移とフェードを管理する。
//
// 【Scene開始時】
// 黒画面
// ↓
// 徐々に透明（Fade In）
//
// 【Scene遷移時】
// 現在画面
// ↓
// 徐々に黒（Fade Out）
// ↓
// 完全暗転
// ↓
// 必要なら完全暗転時の処理を実行
// 例：現在のSceneのBGM停止
// ↓
// Scene切り替え
// ↓
// 黒画面
// ↓
// 徐々に透明（Fade In）
//
// このGameObjectはDontDestroyOnLoadでSceneを跨いで保持する
//===========================================================
public class SceneTransitionManager : MonoBehaviour
{
    // ============================================================
    // Singleton
    // ============================================================
    public static SceneTransitionManager Instance { get; private set; }

    // ============================================================
    // Inspector設定
    // ============================================================
    [Header("フェードUI")]
    [Tooltip("画面全体を覆う黒いImageを設定します。")]
    [SerializeField]
    private Image fadePanel;

    [Header("フェード時間")]
    [Tooltip("Scene遷移時に画面が完全に黒くなるまでの時間")]
    [SerializeField, Min(0.01f)]
    private float fadeOutDuration = 0.5f;

    [Tooltip("新しいSceneに入った後、画面が完全に表示されるまでの時間")]
    [SerializeField, Min(0.01f)]
    private float fadeInDuration = 0.5f;

    // ============================================================
    // 状態
    // フェード中やSceneロード中に
    // ボタンを連打してScene遷移が複数回発生するのを防止する
    // ============================================================
    public bool IsTransitioning { get; private set; }

    // ============================================================
    // 初期化
    // ============================================================
    private void Awake()
    {
        // --------------------------------------------------------
        // Singleton
        // --------------------------------------------------------
        // 既にSceneTransitionManagerが存在している場合は
        // 後から生成されたManagerを削除する。
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // --------------------------------------------------------
        // Sceneを跨いで保持
        // --------------------------------------------------------
        // Title → Home → SelectStage → GameStage → Result
        // とSceneが変更されても、このManagerは破棄しない。
        DontDestroyOnLoad(gameObject);

        // --------------------------------------------------------
        // FadePanel確認
        // --------------------------------------------------------
        if (fadePanel == null)
        {
            Debug.LogError(
                "SceneTransitionManager : FadePanelが設定されていません。",
                this);

            return;
        }

        // --------------------------------------------------------
        // 初期状態
        // --------------------------------------------------------
        // ゲーム開始直後は完全な黒画面にする。
        //
        // Start()からFadeInすることで
        // TitleSceneがいきなり表示されるのを防止する。
        SetFadeAlpha(1.0f);

        // FadeInが始まるまでは
        // 後ろのUIを触れないようにする。
        fadePanel.raycastTarget = true;
    }

    private void Start()
    {
        // --------------------------------------------------------
        // 最初のSceneのFadeIn
        // --------------------------------------------------------
        if (fadePanel != null)
        {
            StartCoroutine(
                FadeIn());
        }
    }

    // ============================================================
    // Scene遷移
    // ============================================================
    // 指定されたSceneへフェード付きで遷移する。
    //
    // 第2引数を省略した場合：
    //
    // ChangeScene("Home");
    //
    // 通常の
    // FadeOut → Scene変更 → FadeIn
    // になる。
    // 
    // 第2引数を指定した場合：
    //
    // ChangeScene("Home", StopTitleBGM);
    //
    // FadeOut
    // ↓
    // 完全暗転
    // ↓
    // StopTitleBGM()
    // ↓
    // Scene変更
    // ↓
    // FadeIn
    public void ChangeScene(
        string sceneName,
        Action onFadeOutComplete = null)
    {
        // --------------------------------------------------------
        // 二重Scene遷移防止
        // --------------------------------------------------------

        if (IsTransitioning)
        {
            return;
        }

        // --------------------------------------------------------
        // Scene名確認
        // --------------------------------------------------------
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                "SceneTransitionManager : Scene名が空です。",
                this);

            return;
        }

        // --------------------------------------------------------
        // Sceneがロード可能か確認
        // --------------------------------------------------------
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"SceneTransitionManager : Scene「{sceneName}」を読み込めません。" +
                " Build Profiles / Scene Listへの登録を確認してください。",
                this);

            return;
        }

        // --------------------------------------------------------
        // Scene遷移開始
        // --------------------------------------------------------
        StartCoroutine(
            ChangeSceneRoutine(
                sceneName,
                onFadeOutComplete));
    }


    // ============================================================
    // 実際のScene遷移処理
    // ============================================================
    private IEnumerator ChangeSceneRoutine(
        string sceneName,
        Action onFadeOutComplete)
    {
        // --------------------------------------------------------
        // Scene遷移開始
        // --------------------------------------------------------

        IsTransitioning = true;

        // --------------------------------------------------------
        // ① FadeOut
        // --------------------------------------------------------

        // 現在の画面を徐々に黒くする。
        yield return FadeOut();

        // ========================================================
        // この時点で画面は完全に真っ黒
        // ========================================================

        // --------------------------------------------------------
        // ② 完全暗転時の処理
        // --------------------------------------------------------
        // 呼び出し側から処理が渡されている場合だけ実行する。
        // BGMが停止
        // nullの場合は何も実行しない。
        onFadeOutComplete?.Invoke();

        // --------------------------------------------------------
        // ③ Sceneロード
        // --------------------------------------------------------
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName);

        // Sceneロード完了まで待つ。
        while (!operation.isDone)
        {
            yield return null;
        }

        // --------------------------------------------------------
        // ④ FadeIn
        // --------------------------------------------------------
        // 新しいSceneを
        // 黒画面から徐々に表示する。
        yield return FadeIn();

        // --------------------------------------------------------
        // ⑤ Scene遷移終了
        // --------------------------------------------------------
        IsTransitioning = false;
    }

    // ============================================================
    // Fade Out
    // ============================================================
    private IEnumerator FadeOut()
    {
        if (fadePanel == null)
        {
            yield break;
        }

        // --------------------------------------------------------
        // フェード中の入力を禁止
        // --------------------------------------------------------

        // FadePanel自身にRaycastを受けさせることで、
        // 後ろに存在するButtonなどが押されるのを防止する。
        fadePanel.raycastTarget = true;


        // --------------------------------------------------------
        // FadeOut開始
        // --------------------------------------------------------
        float timer = 0.0f;

        while (timer < fadeOutDuration)
        {
            // Time.timeScaleが0でもフェードできるように
            // unscaledDeltaTimeを使用する。
            timer += Time.unscaledDeltaTime;


            // 0 → 1へ変化する。
            float alpha =
                Mathf.Clamp01(
                    timer / fadeOutDuration);

            // FadePanelへAlphaを反映する。
            SetFadeAlpha(alpha);

            // 次のフレームまで待つ。
            yield return null;
        }

        // --------------------------------------------------------
        // 完全暗転
        // --------------------------------------------------------
        // 誤差が残らないよう
        // 最後は必ずAlpha = 1にする。
        SetFadeAlpha(1.0f);
    }

    // ============================================================
    // Fade In
    // ============================================================
    private IEnumerator FadeIn()
    {
        if (fadePanel == null)
        {
            yield break;
        }

        // --------------------------------------------------------
        // FadeIn中の入力を禁止
        // --------------------------------------------------------
        fadePanel.raycastTarget = true;

        // --------------------------------------------------------
        // FadeIn開始
        // --------------------------------------------------------
        float timer = 0.0f;

        while (timer < fadeInDuration)
        {
            timer += Time.unscaledDeltaTime;

            // FadeOutとは逆に
            // Alphaを1 → 0へ変化させる。
            float alpha =
                1.0f -
                Mathf.Clamp01(
                    timer / fadeInDuration);

            SetFadeAlpha(alpha);

            yield return null;
        }

        // --------------------------------------------------------
        // FadeIn完了
        // --------------------------------------------------------
        // 最後は確実に透明にする。
        SetFadeAlpha(0.0f);

        // FadeIn終了後は
        // 後ろのUIを操作できるようにする。
        fadePanel.raycastTarget = false;
    }

    // ============================================================
    // FadePanel Alpha変更
    // ============================================================
    private void SetFadeAlpha(float alpha)
    {
        if (fadePanel == null)
        {
            return;
        }

        // 現在の色を取得。
        Color color =
            fadePanel.color;

        // RGBは変更せず
        // Alphaだけ変更する。
        color.a =
            Mathf.Clamp01(alpha);

        // FadePanelへ反映。
        fadePanel.color =
            color;
    }
}