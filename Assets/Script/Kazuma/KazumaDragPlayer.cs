using UnityEngine;
using UnityEngine.InputSystem;

namespace KazumaPrototype
{
    // 指・マウスの移動量だけプレイヤーを動かす。押した位置への瞬間移動はしない。
    // Arena が ReadInput を呼び、移動速度や押す／離す瞬間を水風船の制御に渡す。
    public sealed class KazumaDragPlayer : MonoBehaviour
    {
        // ドラッグ移動量の倍率。1なら画面上の指の移動に対応した距離だけ動く。
        [SerializeField] private float dragSensitivity = 1f;
        // 水風船へ渡す速度の上限（ワールド単位／秒）。プレイヤーの移動距離自体は制限しない。
        [SerializeField] private float maximumFlickSpeed = 18f;
        [SerializeField] private Renderer body;
        // プレイヤーの当たり判定半径。見た目の拡縮とは独立している。
        public const float Radius = 0.34f;
        // 現在押しているか、今回の移動速度、投げるときに使う直近の移動速度。
        public bool IsHeld { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 FlickVelocity { get; private set; }
        // 押し始め／離した瞬間だけ true。入力更新のたびにリセットする。
        public bool PressedThisFrame { get; private set; }
        public bool ReleasedThisFrame { get; private set; }
        private Vector2 previousPointer;
        // 操作中の指を識別するID。-1はマウス、または操作していない状態。
        private int pointerId = -1;
        // アプリ復帰時などに、押しっぱなしの入力を一度離すまで無視する。
        private bool ignoreUntilRelease;
        private float lastMovementTime;
        private MaterialPropertyBlock properties;

        // falseの場合、プレイヤーの操作を受け付けない
        // GameOverなどで使用
        public bool CanMove { get; private set; } = true; // Player�̑�����

        // 端末の入力を読み取る入口。タッチを優先し、同じ処理 FeedPointer に渡す。
        // bounds は移動可能なX/Z範囲（RectのYはワールドZとして扱う）、dt は秒。
        public void ReadInput(Camera camera, Rect bounds, float dt)
        {
            // ����֎~��ԂȂ���͂��󂯕t���Ȃ�
            if (!CanMove)
            {
                Velocity = Vector3.zero;
                FlickVelocity = Vector3.zero;
                IsHeld = false;
                PressedThisFrame = false;
                ReleasedThisFrame = false;
                return;
            }

            bool held = false;
            Vector2 position = default;
            int id = -1;
            var touchscreen = Touchscreen.current;
            // 最初に触れた指を追跡し続ける。途中で別の指を追加しても位置が飛ばないようにする。
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed || (IsHeld && pointerId >= 0 && touch.touchId.ReadValue() != pointerId))
                        continue;
                    held = true;
                    position = touch.position.ReadValue();
                    id = touch.touchId.ReadValue();
                    break;
                }
            }
            if (!held && pointerId < 0 && Mouse.current != null)
            {
                held = Mouse.current.leftButton.isPressed;
                position = Mouse.current.position.ReadValue();
            }
            FeedPointer(held, position, id, camera, bounds, dt);
        }

        // ============================================================
        // Playerの操作可否を変更
        // ============================================================
        public void SetCanMove(bool canMove)
        {
            CanMove = canMove;

            // 操作禁止になった時点で、入力処理と移動速度をリセット
            if (!CanMove)
            {
                IsHeld = false;
                PressedThisFrame = false;
                ReleasedThisFrame = false;
                Velocity = Vector3.zero;
                FlickVelocity = Vector3.zero;
                pointerId = -1;
            }
        }
        // 押下状態と画面座標（ピクセル）から移動を計算する共通処理。
        // 実機入力と自動検証の両方から呼べるよう、入力デバイスの読み取りを分離している。
        public void FeedPointer(bool held, Vector2 position, int id, Camera camera, Rect bounds, float dt)
        {
            PressedThisFrame = ReleasedThisFrame = false;
            Velocity = Vector3.zero;
            if (ignoreUntilRelease)
            {
                if (!held) ignoreUntilRelease = false;
                return;
            }
            if (!held)
            {
                ReleasedThisFrame = IsHeld;
                IsHeld = false;
                pointerId = -1;
                // 止めてから離した場合は、古い移動速度で投げないようにする（猶予0.1秒）。
                if (Time.unscaledTime - lastMovementTime > 0.1f) FlickVelocity = Vector3.zero;
                return;
            }
            // 押した最初のフレームは基準座標の記録だけを行い、プレイヤーを動かさない。
            if (!IsHeld)
            {
                IsHeld = true;
                PressedThisFrame = true;
                previousPointer = position;
                pointerId = id;
                FlickVelocity = Vector3.zero;
                return;
            }
            // 前回／今回の画面座標から、プレイヤーと同じ高さの水平面へレイを飛ばす。
            // 交点の差分を使うため、カメラの拡大率や画面サイズを移動量に反映できる。
            var plane = new Plane(Vector3.up, transform.position);
            Ray before = camera.ScreenPointToRay(previousPointer);
            Ray after = camera.ScreenPointToRay(position);
            previousPointer = position;
            if (!plane.Raycast(before, out float a) || !plane.Raycast(after, out float b)) return;
            Vector3 oldPosition = transform.position;
            Vector3 target = oldPosition + (after.GetPoint(b) - before.GetPoint(a)) * dragSensitivity;
            target.x = Mathf.Clamp(target.x, bounds.xMin, bounds.xMax);
            target.z = Mathf.Clamp(target.z, bounds.yMin, bounds.yMax);
            transform.position = target;
            Velocity = Vector3.ClampMagnitude((target - oldPosition) / Mathf.Max(dt, 0.001f), maximumFlickSpeed);
            if (Velocity.sqrMagnitude > 0.01f)
            {
                FlickVelocity = Velocity;
                lastMovementTime = Time.unscaledTime;
            }
            else if (Time.unscaledTime - lastMovementTime > 0.1f) FlickVelocity = Vector3.zero;
        }

        // 位置と入力履歴を初期化する。waitForRelease=trueなら、次の押し直しまで操作を受け付けない。
        public void ResetPlayer(Vector3 position, bool waitForRelease = false)
        {
            transform.position = position;
            IsHeld = PressedThisFrame = ReleasedThisFrame = false;
            Velocity = FlickVelocity = Vector3.zero;
            pointerId = -1;
            ignoreUntilRelease = waitForRelease;
            SetColor(new Color(0.12f, 0.8f, 1f));
        }

        // 共有マテリアルを複製せず、このプレイヤーだけの表示色を変える。
        public void SetColor(Color color)
        {
            if (body == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            body.SetPropertyBlock(properties);
        }

        // 別アプリへの切り替え時に入力を解除し、復帰後の誤移動・誤投擲を防ぐ。
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ResetPlayer(transform.position, true);
        }
        // スマートフォンの一時停止でも同様に入力履歴を破棄する。
        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetPlayer(transform.position, true);
        }
    }
}
