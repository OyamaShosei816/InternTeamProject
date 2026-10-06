using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    // ゲーム進行の司令塔。入力→水風船の移動→弾の衝突→ボス判定の順に更新する。
    // 弾幕、投擲直後の無敵／パリィ、勝敗、リトライ、円形エフェクトもここで管理する。
    public sealed class PrototypeArena : MonoBehaviour
    {
        // プレイ中・クリア・失敗の3状態。終了後は一定時間を置いて押し直すと再開できる。
        public enum RoundState { Playing, Cleared, Failed }
        // シーン／Prefabの参照。PrototypeBuilderが生成時に設定する。
        [Header("画面：ゲーム用カメラ")]
        [Tooltip("プレイ画面のCameraを設定します。画面の広さとタッチ位置の計算に使います。")]
        [SerializeField] private Camera gameCamera;
        // 操作するプレイヤーの入力・移動コンポーネント。
        [Header("操作対象：プレイヤー")]
        [Tooltip("シーン内のプレイヤーに付いたDragPlayerを設定します。")]
        [SerializeField] private DragPlayer player;
        // 水風船の移動・投擲を管理するコンポーネント。
        [Header("攻撃対象：水風船")]
        [Tooltip("シーン内の水風船に付いたWaterBalloonを設定します。")]
        [SerializeField] private WaterBalloon balloon;
        // 発射時に複製する敵弾Prefabのコンポーネント。
        [Header("敵の攻撃：発射する弾Prefab")]
        [Tooltip("Project内のEnemyBullet Prefabに付いたBulletを設定します。発射ごとに複製します。")]
        [SerializeField] private Bullet bulletPrefab;
        // ボス本体の位置と表示を管理するTransform。
        [Header("敵の配置：ボス本体")]
        [Tooltip("シーン内のボス本体のTransformを設定します。胴体の接触判定とクリア時の非表示に使います。")]
        [SerializeField] private Transform boss;
        // ボスのダメージ受付位置を表す弱点のTransform。
        [Header("敵の弱点：ダメージが入る位置")]
        [Tooltip("シーン内の弱点のTransformを設定します。投げた水風船がここに当たるとHPが減ります。")]
        [SerializeField] private Transform weakPoint;
        // 命中・パリィ・勝敗の円形エフェクトに使用するマテリアル。
        [Header("演出：円形エフェクトの素材")]
        [Tooltip("命中・パリィ・勝敗を知らせる線に使うマテリアルを設定します。")]
        [SerializeField] private Material effectMaterial;
        // ボスの初期HP。弱点に投げた水風船が当たると減る。
        [Header("難易度：ボスの最大HP")]
        [Tooltip("ラウンド開始時のHP。大きいほど倒すまでに多くの攻撃が必要です。")]
        [SerializeField] private float bossMaxHealth = 150f;
        // 弾幕を発射する間隔（秒）。ラウンド開始直後だけ2.5秒待つ。
        [Header("難易度：敵の発射間隔（秒）")]
        [Tooltip("小さいほど弾幕が頻繁になります。最初の発射だけ開始から2.5秒後です。")]
        [SerializeField] private float shotInterval = 1.5f;
        // 水風船を投げた直後に被弾しても失敗しない時間（秒）。
        [Header("救済：投げた直後の無敵時間（秒）")]
        [Tooltip("指を離して投擲した後、被弾しても失敗しない時間。大きいほど回避しやすくなります。")]
        [SerializeField] private float releaseInvulnerability = 0.22f;
        // 投擲直後に強さ3の弾へ触れると全弾消去できる受付時間（秒）。
        [Header("パリィ：投擲後の受付時間（秒）")]
        [Tooltip("投擲後のこの時間内に強さ3の弾へ触れると全弾を消します。大きいほど成功しやすくなります。")]
        [SerializeField] private float parryWindow = 0.12f;
        // キューが弱点に当たった瞬間にゲーム進行を止める時間。実時間の秒数で指定する。
        [Header("キュー演出：弱点命中時の停止時間（秒）")]
        [Tooltip("仕様値0.15秒。長いほど命中の重さが強調されます。0で無効です。")]
        [SerializeField, Range(0f, 0.3f)] private float weakPointHitStop = 0.15f;
        // 弱点命中時に、カメラを画面の横・縦方向へ揺らす最大距離。
        [Header("弱点命中：カメラの揺れ幅")]
        [Tooltip("ワールド単位。初期値0.12。0でカメラシェイクを無効にします。")]
        [SerializeField, Min(0f)] private float cameraShakeStrength = 0.12f;
        // ヒットストップ中も実時間で進む、カメラシェイクの表示時間。
        [Header("弱点命中：カメラが揺れる時間（秒）")]
        [Tooltip("初期値0.15秒。弱点命中と同時に揺れ始め、次第に収まります。")]
        [SerializeField, Min(0f)] private float cameraShakeDuration = 0.15f;
        // カメラシェイクの1秒あたりの振動回数。
        [Header("弱点命中：カメラの揺れる速さ")]
        [Tooltip("初期値35回／秒。大きいほど細かく素早く揺れます。")]
        [SerializeField, Min(1f)] private float cameraShakeFrequency = 35f;
        // カメラシェイクを開始してからの実経過秒数。
        private float cameraShakeElapsed;
        // 前回の描画用にカメラへ加えた位置のずれ。入力判定前と演出終了時に取り除く。
        private Vector3 cameraShakeOffset;
        // 弱点命中のシェイクが進行中か。ゲームの停止中も揺れの更新は継続する。
        public bool IsCameraShaking { get; private set; }
        // ダメージが入らない胴体への命中に使う、短めの停止時間。
        [Header("キュー演出：胴体命中時の停止時間（秒）")]
        [Tooltip("初期値0.04秒。ボスの胴体にキューが当たったときに停止します。0で無効です。")]
        [SerializeField, Range(0f, 0.3f)] private float bodyHitStop = 0.04f;
        // 公転中・投擲中のキューが敵弾を消したときの停止時間。
        [Header("キュー演出：敵弾を消したときの停止時間（秒）")]
        [Tooltip("初期値0.025秒。連続命中では時間を足さず、長い方を採用します。0で無効です。")]
        [SerializeField, Range(0f, 0.3f)] private float bulletHitStop = 0.025f;
        // 敵弾をキューで消した際、キューの外周から広がる輪の色。
        [Header("弾消し演出：外円の色")]
        [Tooltip("キューで敵弾を消した瞬間に表示する光の輪の色です。")]
        [SerializeField] private Color bulletEraseColor = new Color(0.4f, 1f, 1f, 1f);
        // 当たり判定の外周から、輪の半径を追加で広げる距離。
        [Header("弾消し演出：外円が広がる距離")]
        [Tooltip("ワールド単位。キューの当たり判定の外側へ、この距離だけ輪が広がります。")]
        [SerializeField, Min(0f)] private float bulletEraseExpansion = 0.8f;
        // 弾消しの輪が出現してから消えるまでの時間。ヒットストップ中は進めない。
        [Header("弾消し演出：外円の表示時間（秒）")]
        [Tooltip("初期値0.3秒。大きいほどゆっくり広がって消えます。0で演出を無効にします。")]
        [SerializeField, Min(0f)] private float bulletEraseDuration = 0.3f;
        // 弾消しの輪を描く線の太さ。
        [Header("弾消し演出：外円の線の太さ")]
        [Tooltip("ワールド単位。初期値0.09。大きくすると光の輪が太くなります。")]
        [SerializeField, Min(0.001f)] private float bulletEraseWidth = 0.09f;
        // 現在ヒットストップ中か。停止中はゲーム進行と追加の命中判定を行わない。
        public bool IsHitStopped => hitStopRemaining > 0f;
        // ヒットストップの残り時間（実時間の秒数）。Time.timeScaleには影響されない。
        private float hitStopRemaining;
        // ボス命中時のキューを停止中は表示し、停止終了後に消費するための予約。
        private bool consumeBalloonAfterHitStop;
        // 現在の進行状態。
        public RoundState State { get; private set; }
        // ボスの残りHP。0になるとクリア。
        public float BossHealth { get; private set; }
        // このラウンドで成功したパリィの回数。
        public int ParryCount { get; private set; }
        // 現在管理している敵弾の数。
        public int ActiveBulletCount => bullets.Count;
        // プレイヤー中心の移動範囲。Rectの横軸はワールドX、縦軸はワールドZに対応する。
        public static Rect MovementBounds => Rect.MinMaxRect(-4.1f, -7.5f, 4.1f, 3.7f);
        // 画面内で更新・判定する敵弾の一覧。
        private readonly List<Bullet> bullets = new List<Bullet>();
        // 表示中の円形エフェクトの一覧。
        private readonly List<Pulse> pulses = new List<Pulse>();
        // 発射までの残り秒数、無敵の残り秒数、パリィの残り秒数、終了後の経過秒数。
        private float shotTimer, // 次の発射までの残り秒数。
            invincible, // 無敵の残り秒数。
            parryRemaining, // パリィ受付の残り秒数。
            endTimer; // ラウンド終了後の経過秒数。
        // 発射済み弾幕の通し番号。強さ1→2→3の切り替えに使う。
        private int wave;
        // 前フレームのプレイヤー位置。衝突計算用の移動区間の始点。
        private Vector3 previousPlayerPosition;
        // 一時的な円形エフェクトの線、経過時間、色、最終半径をまとめたデータ。
        private sealed class Pulse
        {
            // 円を描画する線コンポーネント。
            public LineRenderer line;
            // 出現からの経過時間（秒）。
            public float age;
            // 出現時の基本色。時間経過で暗くする基準。
            public Color color;
            // 拡大が終わったときの半径（ワールド単位）。
            public float radius;
            // 出現時の半径。弾消しではキューの当たり判定の外周から描く。
            public float startRadius;
            // 円が完全に消えるまでの秒数。
            public float duration;
        }

        // Androidでは縦画面に固定し、最初のラウンドを開始する。
        private void Start()
        {
            if (Application.platform == RuntimePlatform.Android) Screen.orientation = ScreenOrientation.Portrait;
            ResetRound();
        }

        // 残った弾・演出を片付け、HP、タイマー、プレイヤーと風船を開始状態に戻す。
        public void ResetRound()
        {
            StopCameraShake();
            CompleteHitStop();
            ClearBullets();
            // pulse：一覧から取り出した、今回処理する対象。
            foreach (var pulse in pulses)
            {
                if (pulse.line == null) continue;
                pulse.line.gameObject.SetActive(false);
                Destroy(pulse.line.gameObject);
            }
            pulses.Clear();
            State = RoundState.Playing;
            BossHealth = bossMaxHealth;
            ParryCount = wave = 0;
            shotTimer = 2.5f;
            invincible = parryRemaining = endTimer = 0f;
            boss.gameObject.SetActive(true);
            weakPoint.gameObject.SetActive(true);
            player.ResetPlayer(new Vector3(0f, 0.65f, -4.5f));
            previousPlayerPosition = player.transform.position;
            // プレイヤーと風船で性能を共有する。リトライしても装備中のスキル補正は保持する。
            balloon.SetPlayerParameters(player.Parameters);
            balloon.ResetBalloon(player.transform.position);
        }

        // 毎フレームの進行処理。プレイヤー入力は1回読み、物理的な移動と衝突だけ細分化する。
        private void Update()
        {
            AdvanceFrame(Time.deltaTime, Time.unscaledDeltaTime);
        }

        // 入力・移動の処理後に描画用の揺れを加える。Time.timeScaleには依存しない。
        private void LateUpdate()
        {
            AdvanceCameraShake(Time.unscaledDeltaTime);
        }

        // deltaTimeはゲーム内の経過秒数、unscaledDeltaTimeは時間倍率に依存しない実経過秒数。
        // 実際のUpdateと検証で同じ進行処理を使い、停止中の移動や復帰を確認できるようにする。
        public void AdvanceFrame(float deltaTime, float unscaledDeltaTime)
        {
            // 揺れをタッチ座標の変換に混ぜない。静止した指でプレイヤーが動くのを防ぐ。
            RestoreCameraOffset();
            // 停止中もRキーで即座にリトライできる。
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetRound();
                return;
            }
            if (IsHitStopped)
            {
                // 座標の基準だけ更新して復帰時のジャンプを防ぎ、離した入力はプレイヤー側に保存する。
                player.ReadInput(gameCamera, MovementBounds, unscaledDeltaTime, freezeMovement: true);
                AdvanceHitStop(unscaledDeltaTime);
                return;
            }
            // 画面が縦長でも左右のプレイ領域を確保するよう、カメラの表示範囲を広げる。
            gameCamera.orthographicSize = Mathf.Max(9f, 5f / Mathf.Max(gameCamera.aspect, 0.1f));
            // 処理落ち後に一度に大きく動くのを避けるため、1フレームで進める時間を0.1秒までにする。
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            UpdatePulses(dt);
            player.ReadInput(gameCamera, MovementBounds, dt);
            if (State != RoundState.Playing)
            {
                endTimer += dt;
                // 終了から0.75秒後、新しくタップ／クリックすると再開する。メニュー操作は不要。
                if (endTimer > 0.75f && player.PressedThisFrame) ResetRound();
                else player.transform.position = previousPlayerPosition;
                return;
            }
            // bufferedReleaseは停止中の投擲予約の有無、bufferedFlickはその瞬間の速度。
            // 予約があれば最新の入力より優先し、復帰時に1回だけ投げる。
            bool bufferedRelease = player.TryConsumeBufferedRelease(out Vector3 bufferedFlick);
            if ((bufferedRelease || player.ReleasedThisFrame) && balloon.Launch(bufferedRelease ? bufferedFlick : player.FlickVelocity))
                BeginReleaseProtection();
            // 今回の入力を反映したプレイヤーの到達位置。
            Vector3 currentPlayerPosition = player.transform.position;
            // 1回の更新を最大1/120秒に分割してばねの計算を安定させる。
            // プレイヤーの移動も区間ごとに補間して、移動中の衝突を判定する。
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
            // 細分化した1回分のシミュレーション時間（秒）。
            float step = dt / steps;
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < steps && State == RoundState.Playing; i++)
            {
                // 今回の細分化区間の始点となるプレイヤー位置。
                Vector3 from = Vector3.Lerp(previousPlayerPosition, currentPlayerPosition, i / (float)steps);
                // 今回の細分化区間の終点となるプレイヤー位置。
                Vector3 to = Vector3.Lerp(previousPlayerPosition, currentPlayerPosition, (i + 1f) / steps);
                balloon.Simulate(to, player.Velocity, player.IsHeld, step);
                TickBullets(from, to, step);
                if (State != RoundState.Playing || IsHitStopped) break;
                CheckBossHit();
                // 命中したフレームの残りの細分化処理も止め、キューが進み続けるのを防ぐ。
                if (IsHitStopped) break;
                invincible = Mathf.Max(0f, invincible - step);
                parryRemaining = Mathf.Max(0f, parryRemaining - step);
            }
            previousPlayerPosition = currentPlayerPosition;
            if (State != RoundState.Playing || IsHitStopped) return;
            player.SetColor(invincible > 0f ? Color.white : new Color(0.12f, 0.8f, 1f));
            shotTimer -= dt;
            if (shotTimer <= 0f)
            {
                FireWave();
                shotTimer = shotInterval;
            }
        }

        // 投擲成功時に無敵とパリィの受付を同時に開始し、白い円で合図する。
        public void BeginReleaseProtection()
        {
            invincible = releaseInvulnerability;
            parryRemaining = parryWindow;
            EmitPulse(player.transform.position, Color.white, 0.8f);
        }

        // 強さ1→2→3を繰り返す扇状弾幕。発射時点のプレイヤー方向を狙う。
        private void FireWave()
        {
            // 今回の弾幕の強さ。発射のたびに1→2→3を繰り返す。
            int power = 1 + wave++ % 3;
            // ボスの弱点より少し手前に置く敵弾の発射位置。
            Vector3 origin = weakPoint.position + Vector3.back * 0.5f;
            // 発射位置から現在のプレイヤーへ向かう長さ1の方向。
            Vector3 direction = (player.transform.position - origin).normalized;
            // 一度に発射する弾の数。強さ3なら3発、それ以外は5発。
            int count = power == 3 ? 3 : 5;
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < count; i++)
            {
                // 扇状に14度ずつ方向をずらし、強さに応じた速さを与えた弾の速度。
                Vector3 velocity = Quaternion.AngleAxis((i - (count - 1) * 0.5f) * 14f, Vector3.up) * direction * (2.7f + power * 0.35f);
                SpawnBullet(origin, velocity, power);
            }
        }

        // 弾を生成して管理リストへ登録する。負荷対策として96個を上限とし、超えたらnullを返す。
        public Bullet SpawnBullet(Vector3 position, Vector3 velocity, int power)
        {
            if (bullets.Count >= 96) return null;
            // 生成または判定対象となる敵弾。
            var bullet = Instantiate(bulletPrefab, transform);
            bullet.Initialize(position, velocity, power);
            bullets.Add(bullet);
            return bullet;
        }

        // 弾を進めて衝突を解決する。playerFrom/Toはこの時間区間のプレイヤー移動前／後の位置。
        public void TickBullets(Vector3 playerFrom, Vector3 playerTo, float dt)
        {
            if (IsHitStopped) return;
            // 通常の被弾より先にパリィを判定する。受付中に強さ3の弾へ接触すると全弾を消す。
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < bullets.Count; i++) bullets[i].Simulate(dt);
            if (parryRemaining > 0f)
            {
                // bullet：一覧から取り出した、今回処理する対象。
                foreach (var bullet in bullets)
                {
                    if (bullet.Power == 3 && Sweep(bullet.PreviousPosition - playerFrom,
                        bullet.transform.position - playerTo, bullet.Radius + DragPlayer.Radius * player.Parameters.ParryRange, out _))
                    {
                        ResolveParry(playerTo);
                        return;
                    }
                }
            }
            // 高速投擲が複数の弾を横切る場合も接触順に処理する。
            // 格上の弾でキューが消滅した後、その奥の弾まで消してしまうことを防ぐ。
            if (balloon.CanHit && bullets.Count > 1) bullets.Sort(CompareCueContactOrder);
            // 削除でリストの添字がずれても未処理の弾を飛ばさないよう、後ろから調べる。
            // 同じ判定区間で複数の弾を消しても、外円を重ね描きしないための印。
            bool emittedEraseEffect = false;
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                // 生成または判定対象となる敵弾。
                var bullet = bullets[i];
                // 敵弾がこの移動区間でプレイヤーに接触するか。playerTimeは接触時点（0～1）。
                bool hitsPlayer = Sweep(bullet.PreviousPosition - playerFrom,
                    bullet.transform.position - playerTo, bullet.Radius + DragPlayer.Radius, out float playerTime);
                // 敵弾が水風船へ触れる時点（0～1）。未接触なら無限大のままにする。
                float ballTime = float.PositiveInfinity;
                // 強さに関係なくキューへの接触を調べる。接触後にレベルの大小で結果を分ける。
                bool hitsBall = balloon.CanHit && Sweep(
                    bullet.PreviousPosition - balloon.PreviousPosition,
                    bullet.transform.position - balloon.transform.position,
                    bullet.Radius + balloon.HitRadius, out ballTime);
                // 紐は弾を消さない。風船本体がプレイヤーより先に弾へ当たったときだけ防ぐ。
                if (hitsBall && (!hitsPlayer || ballTime <= playerTime))
                {
                    if (balloon.Power >= bullet.Power)
                    {
                        // 同レベル以下なら敵弾を消し、キューは強さと飛行／公転を維持して貫通する。
                        if (!emittedEraseEffect)
                        {
                            EmitBulletEraseEffect();
                            emittedEraseEffect = true;
                        }
                        RemoveBullet(i);
                        BeginHitStop(bulletHitStop);
                        continue;
                    }
                    // 格上の敵弾は残り、キューだけが消える。敵弾のプレイヤー接触判定は継続する。
                    balloon.Consume();
                }
                if (hitsPlayer)
                {
                    RemoveBullet(i);
                    if (invincible <= 0f) EndRound(false);
                    if (State != RoundState.Playing) return;
                    continue;
                }
                if (Mathf.Abs(bullet.transform.position.x) > 6f || Mathf.Abs(bullet.transform.position.z) > 10f)
                    RemoveBullet(i);
            }
        }

        // 後ろから削除するループに合わせ、接触が遅い弾から早い弾の順へ並べる。
        private int CompareCueContactOrder(Bullet left, Bullet right)
        {
            return CueContactTime(right).CompareTo(CueContactTime(left));
        }

        // キューとbulletが移動区間で接触する時点。接触しない場合は最後に処理するため無限大を返す。
        private float CueContactTime(Bullet bullet)
        {
            // 接触が起きる時点（区間の開始0～終了1）。強さでは絞り込まず格上も含める。
            bool hits = Sweep(bullet.PreviousPosition - balloon.PreviousPosition,
                bullet.transform.position - balloon.transform.position, bullet.Radius + balloon.HitRadius, out float time);
            return hits ? time : float.PositiveInfinity;
        }

        // パリィ成功を確定する。距離や弾の強さを問わず、管理中の敵弾を即座に全消去する。
        // 受付も終了させ、同じ投擲でパリィ回数や演出が重複しないようにする。
        private void ResolveParry(Vector3 position)
        {
            ParryCount++;
            parryRemaining = 0f;
            ClearBullets();
            EmitPulse(position, Color.cyan, 5f, DragPlayer.Radius * player.Parameters.ParryRange);
        }

        // 弾を打ち消した位置のキュー外周に輪を作る。強化やパッシブによる判定半径も反映する。
        private void EmitBulletEraseEffect()
        {
            if (bulletEraseDuration <= 0f) return;
            EmitPulse(balloon.transform.position, bulletEraseColor,
                balloon.HitRadius + Mathf.Max(0f, bulletEraseExpansion), balloon.HitRadius,
                bulletEraseDuration, Mathf.Max(0.001f, bulletEraseWidth), "Bullet erase outer ring");
        }

        // 投擲中の風船だけボスに当たる。弱点と胴体のうち先に接触した方を採用する。
        // 弱点ならキャラ攻撃力とレベルに応じたダメージ、胴体ならダメージなしで風船を消費する。
        public void CheckBossHit()
        {
            if (IsHitStopped) return;
            if (balloon.State != WaterBalloon.MotionState.Flying) return;
            // 水風船が弱点に接触するか。weakTimeは移動区間内の最初の接触時点（0～1）。
            bool weakHit = Sweep(balloon.PreviousPosition - weakPoint.position,
                balloon.transform.position - weakPoint.position, balloon.HitRadius + 0.55f, out float weakTime);
            // 水風船が胴体に接触するか。bodyTimeは移動区間内の最初の接触時点（0～1）。
            bool bodyHit = Sweep(balloon.PreviousPosition - boss.position,
                balloon.transform.position - boss.position, balloon.HitRadius + 1.15f, out float bodyTime);
            if (weakHit && (!bodyHit || weakTime <= bodyTime))
            {
                BossHealth = Mathf.Max(0f, BossHealth - balloon.CurrentDamage);
                EmitPulse(weakPoint.position, Color.yellow, 1.6f);
                StopOnBalloonImpact(weakPointHitStop);
                BeginCameraShake();
                Debug.Log($"Prototype: weak point hit. Boss HP {BossHealth:0}/{bossMaxHealth:0}", this);
                if (BossHealth <= 0f) EndRound(true);
            }
            else if (bodyHit)
            {
                EmitPulse(balloon.transform.position, Color.gray, 0.7f);
                StopOnBalloonImpact(bodyHitStop);
            }
        }

        // durationは停止させる実時間の秒数。同時命中で停止時間が積み上がらないよう長い方を使う。
        private void BeginHitStop(float duration)
        {
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            hitStopRemaining = Mathf.Max(hitStopRemaining, duration);
        }

        // duration秒の命中演出を開始する。停止中はキューを残し、停止終了時に再生産待ちへ移す。
        private void StopOnBalloonImpact(float duration)
        {
            BeginHitStop(duration);
            if (IsHitStopped) consumeBalloonAfterHitStop = true;
            else balloon.Consume();
        }

        // unscaledDeltaTime秒だけ停止時間を進める。ゲーム本体の時間倍率や一時停止を変更しない。
        public void AdvanceHitStop(float unscaledDeltaTime)
        {
            if (!IsHitStopped) return;
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - Mathf.Max(0f, unscaledDeltaTime));
            if (!IsHitStopped) CompleteHitStop();
        }

        // 停止を終了し、保留していた命中済みキューの消費を1回だけ行う。
        private void CompleteHitStop()
        {
            hitStopRemaining = 0f;
            if (consumeBalloonAfterHitStop && balloon != null) balloon.Consume();
            consumeBalloonAfterHitStop = false;
        }

        // 無効化・シーン移動で停止や投擲予約を持ち越さないようにする。
        private void OnDisable()
        {
            StopCameraShake();
            CompleteHitStop();
            if (player != null) player.TryConsumeBufferedRelease(out _);
        }

        // 移動区間と球の衝突を調べ、高速な弾やフリックのすり抜けを防ぐ。
        // from/toは相手から見た相対位置、radiusは双方の半径の合計。
        // timeは最初に接触する時点（0=区間の開始、1=終了）。戻り値がtrueのときに使う。
        public static bool Sweep(Vector3 from, Vector3 to, float radius, out float time)
        {
            time = 0f;
            // 開始時点ですでに重なっていれば、時刻0で接触している。
            float c = from.sqrMagnitude - radius * radius;
            if (c <= 0f) return true;
            // 相手から見た、移動区間の始点から終点への変化量。
            Vector3 delta = to - from;
            // 衝突の二次方程式の係数。移動量の長さの2乗。
            float a = delta.sqrMagnitude;
            if (a < 0.000001f) return false;
            // 衝突の二次方程式の係数。始点と移動方向の内積。
            float b = Vector3.Dot(from, delta);
            // 移動する点が球面へ到達する二次方程式を解く。判別式が負なら接触しない。
            float discriminant = b * b - a * c;
            if (discriminant < 0f) return false;
            time = (-b - Mathf.Sqrt(discriminant)) / a;
            return time >= 0f && time <= 1f;
        }

        // 勝敗を確定し、色と円形エフェクトで結果を示して残りの弾を片付ける。
        private void EndRound(bool cleared)
        {
            State = cleared ? RoundState.Cleared : RoundState.Failed;
            player.SetColor(cleared ? Color.green : Color.red);
            EmitPulse(cleared ? boss.position : player.transform.position, cleared ? Color.green : Color.red, 3f);
            if (cleared) boss.gameObject.SetActive(false);
            ClearBullets();
            Debug.Log(cleared ? "Prototype: CLEAR. Tap/click to restart." : "Prototype: HIT. Tap/click to retry.", this);
        }

        // Destroyはフレーム末尾まで遅延するため、先に非表示にして管理リストから除く。
        private void RemoveBullet(int index)
        {
            bullets[index].gameObject.SetActive(false);
            Destroy(bullets[index].gameObject);
            bullets.RemoveAt(index);
        }
        // リスト末尾から全弾を削除する。リトライ、パリィ、ラウンド終了時に使用する。
        private void ClearBullets()
        {
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = bullets.Count - 1; i >= 0; i--) RemoveBullet(i);
        }

        // positionを中心にstartRadiusからradiusへ広がる円を、duration秒間表示する。
        // widthは線の太さ、effectNameはHierarchyで演出を識別する名前。同時表示は8個まで。
        private void EmitPulse(Vector3 position, Color color, float radius, float startRadius = 0.2f,
            float duration = 0.45f, float width = 0.07f, string effectName = "Gameplay pulse")
        {
            // 上限時は古い輪を終了し、新しく起きた弾消しやパリィの演出を必ず表示する。
            if (pulses.Count >= 8)
            {
                pulses[0].line.gameObject.SetActive(false);
                Destroy(pulses[0].line.gameObject);
                pulses.RemoveAt(0);
            }
            // 円形エフェクト用に作る一時的なGameObject。
            var go = new GameObject(effectName);
            go.transform.SetParent(transform);
            go.transform.position = position;
            // 線を表示するLineRenderer。
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = effectMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < 40; i++)
            {
                // 円周上の配置に使用する角度（ラジアン）。
                float angle = i * Mathf.PI * 2f / 40f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * startRadius);
            }
            pulses.Add(new Pulse { line = line, color = color, radius = radius,
                startRadius = startRadius, duration = Mathf.Max(0.001f, duration) });
        }

        // 弱点命中の瞬間に揺れを開始する。古い位置のずれを残さず、連続命中では演出を更新する。
        private void BeginCameraShake()
        {
            StopCameraShake();
            if (gameCamera == null || cameraShakeStrength <= 0f || cameraShakeDuration <= 0f) return;
            IsCameraShaking = true;
            cameraShakeElapsed = 0f;
            AdvanceCameraShake(0f);
        }

        // unscaledDeltaTime秒だけ揺れを進める。ヒットストップ中も減衰し、終了時は元の位置へ戻す。
        public void AdvanceCameraShake(float unscaledDeltaTime)
        {
            RestoreCameraOffset();
            if (!IsCameraShaking || gameCamera == null) return;
            cameraShakeElapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (cameraShakeDuration <= 0f || cameraShakeElapsed >= cameraShakeDuration)
            {
                StopCameraShake();
                return;
            }
            // 演出終盤ほど小さくする揺れ幅。ゲーム内の判定位置は動かさない。
            float amplitude = cameraShakeStrength * (1f - cameraShakeElapsed / cameraShakeDuration);
            // 時間から決まる振動の位相。乱数を使わず、検証時も同じ揺れを再現できる。
            float phase = cameraShakeElapsed * Mathf.PI * 2f * cameraShakeFrequency;
            cameraShakeOffset = (gameCamera.transform.right * Mathf.Cos(phase)
                + gameCamera.transform.up * Mathf.Cos(phase * 0.73f + 1f)) * amplitude;
            gameCamera.transform.position += cameraShakeOffset;
        }

        // 最後に加えた描画用のずれだけを取り除き、カメラ本来の位置を保持する。
        private void RestoreCameraOffset()
        {
            if (gameCamera != null) gameCamera.transform.position -= cameraShakeOffset;
            cameraShakeOffset = Vector3.zero;
        }

        // リトライ、無効化、揺れ終了で呼ぶ後片付け。カメラのずれを次のラウンドに持ち越さない。
        private void StopCameraShake()
        {
            RestoreCameraOffset();
            IsCameraShaking = false;
            cameraShakeElapsed = 0f;
        }
        // 円を指定した表示時間で拡大・暗くし、寿命を迎えたら即座に非表示にして削除する。
        private void UpdatePulses(float dt)
        {
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                // 更新または削除対象の円形エフェクト1個分のデータ。
                var pulse = pulses[i];
                pulse.age += dt;
                // エフェクトの寿命に対する経過割合。1になると削除する。
                float t = pulse.age / pulse.duration;
                if (t >= 1f)
                {
                    pulse.line.gameObject.SetActive(false);
                    Destroy(pulse.line.gameObject);
                    pulses.RemoveAt(i);
                    continue;
                }
                // 円形エフェクトの表示色。
                Color color = pulse.color * (1f - t);
                color.a = 1f;
                pulse.line.startColor = pulse.line.endColor = color;
                // j：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
                for (int j = 0; j < 40; j++)
                {
                    // 円周上の配置に使用する角度（ラジアン）。
                    float angle = j * Mathf.PI * 2f / 40;
                    pulse.line.SetPosition(j, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Lerp(pulse.startRadius, pulse.radius, t));
                }
            }
        }
    }
}
