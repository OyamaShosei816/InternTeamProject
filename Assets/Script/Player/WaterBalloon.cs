using UnityEngine;

namespace Prototype
{
    // 水風船の公転、回転による強化、投擲、回復と見た目を管理する。
    // Arena が細かい時間刻みで Simulate を呼び、衝突時は Consume で回復待ちにする。
    public sealed class WaterBalloon : MonoBehaviour
    {
        // Ready: 待機／Orbiting: 押している間の公転／Flying: 投擲中／Recovering: 再出現待ち。
        public enum MotionState { Ready, Orbiting, Flying, Recovering }
        // 公転の調整値。距離はワールド単位、時間は秒、角速度はラジアン／秒。
        // プレイヤーから公転目標までの距離。
        [Header("公転：プレイヤーからの基本距離")]
        [Tooltip("水風船が回る軌道の半径（ワールド単位）。大きいほど遠くを回ります。")]
        [SerializeField] private float orbitRadius = 1.35f;
        // 目標へ引き戻すばねの強さ。大きいほど素早く追従する。
        [Header("公転：軌道へ戻る力")]
        [Tooltip("大きいほど水風船が目標位置へ素早く戻ります。高すぎると揺れやすくなります。")]
        [SerializeField] private float spring = 65f;
        // 速度を減衰させる強さ。大きいほど揺れが収まりやすい。
        [Header("公転：揺れの収まりやすさ")]
        [Tooltip("大きいほど勢いを抑えて揺れを収めます。小さいほど慣性が残ります。")]
        [SerializeField] private float damping = 6f;
        // 基本の公転角速度。強さとプレイヤーの移動速度に応じて加速する。
        [Header("公転：基本の回転速度")]
        [Tooltip("ラジアン／秒。約6.28で毎秒1周です。大きいほど速く回ります。")]
        [SerializeField] private float baseAngularSpeed = 3.8f;
        // 紐の最大長。超えた場合は位置と外向きの速度を補正する。
        [Header("紐：伸びる長さの上限")]
        [Tooltip("プレイヤーから水風船までの最大距離（ワールド単位）。基本距離以上を目安にします。")]
        [SerializeField] private float maximumTetherLength = 2.25f;
        // 強さが1段階上がるまでの実回転数。初期値は3周、強さの上限は3。
        [Header("強化：1段階上がるまでの周回数")]
        [Tooltip("移動しながら指定回数だけ回ると強さが上がります。小さいほど早く強化されます。")]
        [SerializeField, Min(1)] private int revolutionsPerLevel = 3;
        // 投擲時にプレイヤーのフリック速度を加える倍率。
        [Header("投擲：フリックの反映倍率")]
        [Tooltip("指を離す直前のプレイヤー移動速度を加える倍率。0ではフリックの加速を加えません。")]
        [SerializeField] private float flickInfluence = 0.8f;
        // 投擲直後の速度上限（ワールド単位／秒）。
        [Header("投擲：飛び出す速さの上限")]
        [Tooltip("投げた直後の速度上限（ワールド単位／秒）。大きいほど速く投げられます。")]
        [SerializeField] private float maximumLaunchSpeed = 30f;
        // 命中・寿命切れから再出現までの待ち時間（秒）。
        [Header("再使用：水風船が戻るまでの秒数")]
        [Tooltip("初期値1秒。消滅してからプレイヤーの上方へ再出現するまでの時間。再生産速度の倍率で短縮されます。")]
        [SerializeField, Min(0f)] private float recoveryDelay = 1f;
        // 色や形を表示する本体のRenderer。
        [Header("水風船の見た目：表示用Renderer")]
        [Tooltip("水風船Prefab内の球のRendererを設定します。色変更と伸び縮みに使います。")]
        [SerializeField] private Renderer body;
        // プレイヤーと水風船を結ぶ紐を描画するコンポーネント。
        [Header("紐の見た目：LineRenderer")]
        [Tooltip("水風船PrefabのLineRendererを設定します。紐の線を描くための参照です。")]
        [SerializeField] private LineRenderer tether;
        // 倍率1の基準となる当たり判定半径。速度による見た目の伸び縮みでは変化しない。
        public const float Radius = 0.4f;
        // キャラクター性能を反映した現在の当たり判定半径（ワールド単位）。
        public float HitRadius => Radius * Parameters.CueHitRange;
        // 所有者の性能。Arenaからプレイヤーと同じインスタンスを渡す。
        private PlayerParameters parameters = new PlayerParameters();
        // 単体で配置された風船でも初期性能で動作するための参照。
        private PlayerParameters Parameters => parameters;

        // ownerParametersは所有者の共通性能。参照を共有し、スキル変更をすぐ反映する。
        public void SetPlayerParameters(PlayerParameters ownerParameters)
        {
            parameters = ownerParameters ?? new PlayerParameters();
        }
        // 待機・公転・飛行・回復待ちのうち、現在の水風船の状態。
        public MotionState State { get; private set; }
        // 弾の強さ（1～3）。
        public int Power { get; private set; } = 1;
        // 成長倍率を反映した強化進捗を「周」に換算する。成長倍率1では実際の回転数に対応する。
        public float ChargedRevolutions => chargedRadians / (Mathf.PI * 2f);
        // 1秒当たりの移動量を表す速度。
        public Vector3 Velocity { get; private set; }
        // 高速移動時の衝突判定に使う、直前のシミュレーション位置。
        public Vector3 PreviousPosition { get; private set; }
        // 待機中と回復待ちでは敵弾を消せない。公転中と投擲中だけ有効。
        public bool CanHit => State == MotionState.Orbiting || State == MotionState.Flying;
        // 順に目標の公転角度、強化用の累積角度、飛行／回復の残り秒数、前回の実角度。
        // phaseとchargedRadiansはラジアン、previousAngleは度で保持する。
        private float phase, // 公転目標の角度（ラジアン）。
            chargedRadians, // 強化用に蓄積した実回転角度（ラジアン）。
            remaining, // 飛行・回復待ちの残り時間（秒）。
            previousAngle; // 前回の実際の角度（度）。
        // 共有マテリアルを変えず、対象だけの色を指定するためのデータ。
        private MaterialPropertyBlock properties;

        // プレイヤー位置anchorの画面上方（ワールド+Z）へ戻し、強さ1の待機状態からやり直す。
        public void ResetBalloon(Vector3 anchor)
        {
            State = MotionState.Ready;
            Power = 1;
            phase = Mathf.PI / 2f;
            chargedRadians = 0f;
            Velocity = Vector3.zero;
            transform.position = anchor + Vector3.forward * orbitRadius;
            PreviousPosition = transform.position;
            previousAngle = 90f;
            body.enabled = true;
            UpdateAppearance(anchor);
        }

        // dt秒だけ進める。anchorはプレイヤー位置、heldは押下状態。
        // 回復待ち→再出現、飛行→直進、それ以外→ばね付き公転の順に分岐する。
        public void Simulate(Vector3 anchor, Vector3 playerVelocity, bool held, float dt)
        {
            PreviousPosition = transform.position;
            if (State == MotionState.Recovering)
            {
                // 再生産速度2倍なら、待ち時間のタイマーが2倍の速さで進む。
                remaining -= dt * Parameters.CueReproduction;
                if (remaining <= 0f) ResetBalloon(anchor);
            }
            else if (State == MotionState.Flying)
            {
                transform.position += Velocity * dt;
                remaining -= dt;
                if (remaining <= 0f || Mathf.Abs(transform.position.x) > 7f || Mathf.Abs(transform.position.z) > 11f) Consume();
            }
            else
            {
                State = held ? MotionState.Orbiting : MotionState.Ready;
                // 強化判定と回転加速に使うプレイヤー速度。12を上限とする。
                float movement = Mathf.Min(playerVelocity.magnitude, 12f);
                if (held) phase += (baseAngularSpeed + (Power - 1) * 1.4f + movement * 0.22f) * Parameters.CueSpeed * dt;
                // 現在の公転角度から求めた、プレイヤーから外側への単位方向。
                Vector3 radial = new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase));
                // ばねの力で追いかける公転軌道上の目標位置。
                Vector3 target = anchor + radial * orbitRadius;
                // 目標へ引くばねと速度の減衰で動かす。プレイヤーが動いても風船の慣性を残す。
                Velocity += ((target - transform.position) * spring - Velocity * damping) * dt;
                transform.position += Velocity * dt;
                // プレイヤーから水風船までの距離と方向を表すベクトル。
                Vector3 offset = transform.position - anchor;
                // 紐が伸び切ったら最大長に戻し、さらに外へ伸ばそうとする相対速度だけを取り除く。
                if (offset.magnitude > maximumTetherLength)
                {
                    // 紐が伸びる方向を長さ1にしたベクトル。
                    Vector3 normal = offset.normalized;
                    transform.position = anchor + normal * maximumTetherLength;
                    // プレイヤーに対して、さらに紐を伸ばす方向へ進む速度。
                    float outwardSpeed = Vector3.Dot(Velocity - playerVelocity, normal);
                    if (outwardSpeed > 0f) Velocity -= normal * outwardSpeed;
                }
                offset = transform.position - anchor;
                // プレイヤーから見た水風船の現在の角度（度）。
                float angle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
                // 前回から実際に回った角度の大きさ（ラジアン）。
                // 右回りは角度差が負になるため、絶対値を使って左右どちらも強化に加える。
                float turned = Mathf.Abs(Mathf.DeltaAngle(previousAngle, angle)) * Mathf.Deg2Rad;
                // 押しながらプレイヤーが動いているときだけ、風船の実際の回転角度を強化に加える。
                // 途中で回転方向を変えても、それまでの強化進捗は減らさない。
                if (held && movement > 0.15f && offset.sqrMagnitude > 0.25f)
                    chargedRadians = Mathf.Max((Power - 1) * Mathf.PI * 2f * revolutionsPerLevel, chargedRadians + turned * Parameters.CueGrowth);
                previousAngle = angle;
                Power = Mathf.Clamp(1 + Mathf.FloorToInt(chargedRadians / (Mathf.PI * 2f * revolutionsPerLevel)), 1, 3);
            }
            UpdateAppearance(anchor);
        }

        // 公転中だけ投げられる。現在の風船速度にフリックを加算し、成功時はtrueを返す。
        public bool Launch(Vector3 flick)
        {
            if (State != MotionState.Orbiting) return false;
            // 公転速度とフリック速度を合成した、投げ始めの速度。
            // 公転の実速度には速度倍率が反映済みなので、フリックの加速側へ倍率を掛ける。
            Vector3 direction = Velocity + flick * flickInfluence * Parameters.CueSpeed;
            // 速度がほぼゼロなら、公転軌道の接線方向を使って飛び出す。
            if (direction.sqrMagnitude < 0.1f)
                direction = new Vector3(-Mathf.Sin(phase), 0f, Mathf.Cos(phase)) * baseAngularSpeed * orbitRadius * Parameters.CueSpeed;
            Velocity = Vector3.ClampMagnitude(direction, maximumLaunchSpeed * Parameters.CueSpeed);
            State = MotionState.Flying;
            // 飛行の寿命は最大3秒。範囲外へ出た場合も回復待ちになる。
            remaining = 3f;
            tether.enabled = false;
            return true;
        }

        // 命中などで使用済みにする。風船と紐を隠し、回復待ちへ移す。
        public void Consume()
        {
            // 同じ消滅への重複通知で復活までの時間を延ばさない。
            if (State == MotionState.Recovering) return;
            State = MotionState.Recovering;
            remaining = recoveryDelay;
            Velocity = Vector3.zero;
            body.enabled = false;
            tether.enabled = false;
        }

        // 仕様確定値：Lv.1はキャラの攻撃力、Lv.2とLv.3は2倍。速度による追加補正は行わない。
        public float CurrentDamage => Parameters.AttackPower * (Power == 1 ? 1f : 2f);

        // 強さに応じた色、速度に応じた伸び縮み、紐の表示位置を更新する。
        private void UpdateAppearance(Vector3 anchor)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            // 仕様書の色分け：Lv.1は青、Lv.2はオレンジ、Lv.3は紫。
            Color color = Power == 1 ? new Color(0.1f, 0.35f, 1f) : Power == 2 ? new Color(1f, 0.5f, 0f) : new Color(0.65f, 0f, 1f);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            body.SetPropertyBlock(properties);
            // 伸び縮みは見た目だけに適用する。当たり判定半径は一定に保つ。
            float stretch = Mathf.Clamp(Velocity.magnitude * 0.01f, 0f, 0.2f);
            body.transform.localScale = new Vector3(0.8f - stretch * 0.4f, 0.8f - stretch * 0.4f, 0.8f + stretch) * Parameters.CueHitRange;
            if (Velocity.sqrMagnitude > 0.05f) body.transform.rotation = Quaternion.LookRotation(Velocity, Vector3.up);
            tether.enabled = State == MotionState.Ready || State == MotionState.Orbiting;
            if (!tether.enabled) return;
            // 紐は9点で描き、中央を少し下げてたるみを表現する。紐自体に当たり判定はない。
            tether.positionCount = 9;
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < 9; i++)
            {
                // 紐の始点から終点までの割合（0～1）。
                float t = i / 8f;
                // 紐を構成する1点のワールド座標。後でたるみを加える。
                Vector3 point = Vector3.Lerp(anchor, transform.position, t);
                point += Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.2f);
                tether.SetPosition(i, point);
            }
        }
    }
}
