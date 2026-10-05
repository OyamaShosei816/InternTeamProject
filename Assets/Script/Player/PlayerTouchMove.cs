using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
// タッチ操作でPlayerを移動させるクラス
// Android端末 : 指をドラッグして移動
// Unity Editor: マウス左ボタンを押しながらドラッグして移動
// 画面解像度によって操作感が変わりにくいように、
// 指・マウスの移動量を画面サイズで正規化して使用する。
// また、Playerがカメラの表示範囲外へ出ないように制限する。
// ============================================================
public class PlayerTouchMove : MonoBehaviour
{
    // =========================================================
    // 移動設定
    // =========================================================
    [Header("移動設定")]
    [Tooltip("Playerの移動速度。Inspectorから調整可能")]
    [SerializeField] private float moveSpeed = 20.0f;

    [Header("移動範囲設定")]
    [Tooltip("画面端からどれだけ内側でPlayerを止めるか")]
    [SerializeField] private float screenMargin = 0.5f;

    // =========================================================
    // 内部変数
    // =========================================================
    // 1フレーム前の指 / マウスの画面座標
    private Vector2 previousPointerPosition;

    // 現在ドラッグ操作中か
    private bool isDragging;

    // Playerを映しているMain Camera
    private Camera mainCamera;

    // =========================================================
    // 初期化
    // =========================================================
    private void Awake()
    {
        // MainCameraタグが付いているCameraを取得
        mainCamera = Camera.main;
    }

    // =========================================================
    // Update
    // =========================================================
    private void Update()
    {
        // Androidなどのタッチ操作
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            HandleTouch();
            return;
        }

        // Unity Editor / PCでのマウス操作
        HandleMouse();
    }

    // =========================================================
    // タッチ操作
    // =========================================================
    private void HandleTouch()
    {
        // 現在の指の画面座標を取得
        Vector2 currentPosition =
            Touchscreen.current.primaryTouch.position.ReadValue();

        // タッチ開始直後
        if (!isDragging)
        {
            // 最初の位置を記録するだけ。
            // ここでPlayerを動かすとタッチした瞬間に飛ぶため移動しない。
            previousPointerPosition = currentPosition;
            isDragging = true;
            return;
        }

        // 前フレームから指がどれだけ動いたか取得
        Vector2 delta = currentPosition - previousPointerPosition;

        // Playerを移動
        MovePlayer(delta);

        // 現在位置を次フレーム用に保存
        previousPointerPosition = currentPosition;
    }

    // =========================================================
    // マウス操作
    // =========================================================
    private void HandleMouse()
    {
        // マウスが存在しない環境なら何もしない
        if (Mouse.current == null)
        {
            return;
        }

        // 左クリックしている間
        if (Mouse.current.leftButton.isPressed)
        {
            // 現在のマウス画面座標
            Vector2 currentPosition = Mouse.current.position.ReadValue();

            // クリック開始直後
            if (!isDragging)
            {
                previousPointerPosition = currentPosition;
                isDragging = true;
                return;
            }

            // 前フレームからマウスがどれだけ動いたか
            Vector2 delta = currentPosition - previousPointerPosition;

            // Playerを移動
            MovePlayer(delta);

            // 現在位置を次フレーム用に保存
            previousPointerPosition = currentPosition;
        }
        else
        {
            // 左クリックを離したらドラッグ終了
            isDragging = false;
        }
    }

    // =========================================================
    // Player移動
    // =========================================================
    private void MovePlayer(Vector2 screenDelta)
    {
        // 画面上の移動量を0～1基準に正規化する。
        // 端末によって解像度や比率が変わるため。
        Vector2 normalizedDelta = new Vector2(
            screenDelta.x / Screen.width,
            screenDelta.y / Screen.height
        );

        // 画面上の横移動 → WorldのX
        // 画面上の縦移動 → WorldのZ
        Vector3 moveDirection = new Vector3(
            normalizedDelta.x,
            0.0f,
            normalizedDelta.y
        );

        // 移動後の位置を先に計算
        Vector3 nextPosition =
            transform.position + moveDirection * moveSpeed;

        // 移動後の位置が画面外にならないよう制限
        nextPosition = ClampPositionToScreen(nextPosition);

        // 最終的な位置をPlayerに反映
        transform.position = nextPosition;
    }

    // =========================================================
    // Playerを画面内に制限
    // =========================================================
    private Vector3 ClampPositionToScreen(Vector3 position)
    {
        // Cameraが取得できていない場合は何もしない
        if (mainCamera == null)
        {
            return position;
        }

        // Playerの高さまでのカメラからの距離を取得
        float distanceFromCamera =
            Mathf.Abs(mainCamera.transform.position.y - position.y);

        // 画面左下をWorld座標に変換
        Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(
            new Vector3(0.0f, 0.0f, distanceFromCamera)
        );

        // 画面右上をWorld座標に変換
        Vector3 topRight = mainCamera.ViewportToWorldPoint(
            new Vector3(1.0f, 1.0f, distanceFromCamera)
        );

        // 左右の画面外へ出ないようX座標を制限
        position.x = Mathf.Clamp(
            position.x,
            bottomLeft.x + screenMargin,
            topRight.x - screenMargin
        );

        // 上下の画面外へ出ないようZ座標を制限
        position.z = Mathf.Clamp(
            position.z,
            bottomLeft.z + screenMargin,
            topRight.z - screenMargin
        );

        return position;
    }
}