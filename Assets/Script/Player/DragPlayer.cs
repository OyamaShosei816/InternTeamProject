using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    // 指・マウスの移動量だけプレイヤーを動かす。押した位置への瞬間移動はしない。
    // Arena が ReadInput を呼び、移動速度や押す／離す瞬間を水風船の制御に渡す。
    public sealed class DragPlayer : MonoBehaviour
    {
        // キャラクターの基本性能。実行時のパッシブ補正もこのインスタンスに保持する。
        [Header("キャラクター性能：パリィ・キュー・攻撃力")]
        [Tooltip("項目を開いて6種類の基本倍率を調整します。すべて1が従来の性能です。")]
        [SerializeField] private PlayerParameters parameters = new PlayerParameters();

        // Arenaと水風船が参照する共通パラメーター。旧Prefabにも初期値で対応する。
        public PlayerParameters Parameters => parameters ?? (parameters = new PlayerParameters());

        // ドラッグ移動量の倍率。1なら画面上の指の移動に対応した距離だけ動く。
        [Header("操作：ドラッグへの反応倍率")]
        [Tooltip("1が標準です。大きくすると同じ指の移動でプレイヤーがより遠くへ動きます。")]
        [SerializeField] private float dragSensitivity = 1f;
        // 水風船へ渡す速度の上限（ワールド単位／秒）。プレイヤーの移動距離自体は制限しない。
        [Header("投げ操作：引き継ぐ速度の上限")]
        [Tooltip("水風船へ渡す移動速度の上限（ワールド単位／秒）。大きいほど速いフリックを反映できます。")]
        [SerializeField] private float maximumFlickSpeed = 18f;
        // 色や形を表示する本体のRenderer。
        [Header("プレイヤーの見た目：表示用Renderer")]
        [Tooltip("プレイヤーPrefab内の球のRendererを設定します。無敵や勝敗に応じた色変更に使います。")]
        [SerializeField] private Renderer body;
        // プレイヤーの当たり判定半径。見た目の拡縮とは独立している。
        public const float Radius = 0.34f;
        // 現在押しているか、今回の移動速度、投げるときに使う直近の移動速度。
        public bool IsHeld { get; private set; }
        // 1秒当たりの移動量を表す速度。
        public Vector3 Velocity { get; private set; }
        // 投げる瞬間に水風船へ加える、直近のプレイヤー移動速度。
        public Vector3 FlickVelocity { get; private set; }
        // 押し始め／離した瞬間だけ true。入力更新のたびにリセットする。
        public bool PressedThisFrame { get; private set; }
        // 今回の入力更新で、押していた指・ボタンを離した場合だけtrue。
        public bool ReleasedThisFrame { get; private set; }
        // 前回の指・マウスの画面座標（ピクセル）。
        private Vector2 previousPointer;
        // 操作中の指を識別するID。-1はマウス、または操作していない状態。
        private int pointerId = -1;
        // アプリ復帰時などに、押しっぱなしの入力を一度離すまで無視する。
        private bool ignoreUntilRelease;
        // 最後に動いた時刻（ゲームの時間倍率に影響されない秒数）。
        private float lastMovementTime;
        // 共有マテリアルを変えず、対象だけの色を指定するためのデータ。
        private MaterialPropertyBlock properties;
        // 停止中に離した入力を、ゲーム進行が再開するまで保存する。
        private bool bufferedRelease;
        // 停止中に離した瞬間のフリック速度。次の入力で上書きしないため別に持つ。
        private Vector3 bufferedFlickVelocity;

        // 端末の入力を読み取る入口。タッチを優先し、同じ処理 FeedPointer に渡す。
        // bounds は移動可能なX/Z範囲（RectのYはワールドZとして扱う）、dt は秒。
        // freezeMovement=trueでは入力だけ追跡し、位置の移動を止める。
        public void ReadInput(Camera camera, Rect bounds, float dt, bool freezeMovement = false)
        {
            // 今回、操作用の指またはマウスボタンが押されているか。
            bool held = false;
            // 今回の指・マウスの画面座標（ピクセル）。
            Vector2 position = default;
            // 入力元を識別するID。タッチは指のID、マウスは-1。
            int id = -1;
            // 現在利用できるタッチ入力デバイス。存在しない場合はnull。
            var touchscreen = Touchscreen.current;
            // 最初に触れた指を追跡し続ける。途中で別の指を追加しても位置が飛ばないようにする。
            if (touchscreen != null)
            {
                // touch：一覧から取り出した、今回処理する対象。
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
            FeedPointer(held, position, id, camera, bounds, dt, freezeMovement);
        }

        // 押下状態と画面座標（ピクセル）から移動を計算する共通処理。
        // 実機入力と自動検証の両方から呼べるよう、入力デバイスの読み取りを分離している。
        // freezeMovement=trueでは停止中の座標を基準として保存し、離した操作を予約する。
        public void FeedPointer(bool held, Vector2 position, int id, Camera camera, Rect bounds, float dt, bool freezeMovement = false)
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
                if (!freezeMovement && Time.unscaledTime - lastMovementTime > 0.1f) FlickVelocity = Vector3.zero;
                if (freezeMovement && ReleasedThisFrame)
                {
                    bufferedRelease = true;
                    bufferedFlickVelocity = FlickVelocity;
                }
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
            if (freezeMovement)
            {
                // 停止中のドラッグ量は移動に加算せず、復帰後の移動基準だけ追従させる。
                previousPointer = position;
                lastMovementTime = Time.unscaledTime;
                return;
            }
            // 前回／今回の画面座標から、プレイヤーと同じ高さの水平面へレイを飛ばす。
            // 交点の差分を使うため、カメラの拡大率や画面サイズを移動量に反映できる。
            var plane = new Plane(Vector3.up, transform.position);
            // 前回の画面座標から水平面へ飛ばすレイ。
            Ray before = camera.ScreenPointToRay(previousPointer);
            // 今回の画面座標から水平面へ飛ばすレイ。
            Ray after = camera.ScreenPointToRay(position);
            previousPointer = position;
            // a/bは前回／今回のレイが水平面に届くまでの距離。交点を求めるために使う。
            if (!plane.Raycast(before, out float a) || !plane.Raycast(after, out float b)) return;
            // 移動処理を始める前のプレイヤー位置。
            Vector3 oldPosition = transform.position;
            // ドラッグ量を加えた移動先。後で移動可能範囲に収める。
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
            bufferedRelease = false;
            bufferedFlickVelocity = Vector3.zero;
            transform.position = position;
            IsHeld = PressedThisFrame = ReleasedThisFrame = false;
            Velocity = FlickVelocity = Vector3.zero;
            pointerId = -1;
            ignoreUntilRelease = waitForRelease;
            SetColor(new Color(0.12f, 0.8f, 1f));
        }

        // flickVelocityに停止中の投擲速度を渡す。予約があればtrueを返し、一度だけ消費する。
        public bool TryConsumeBufferedRelease(out Vector3 flickVelocity)
        {
            flickVelocity = bufferedFlickVelocity;
            // この呼び出し前に投擲予約があったかを、消去する前に保存する。
            bool hadRelease = bufferedRelease;
            bufferedRelease = false;
            return hadRelease;
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
