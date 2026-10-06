using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Prototype.Editor
{
    // Unityエディター専用。保存済みシーンを実際のPlay Modeで動かし、操作・衝突・勝敗を検証する。
    // Tools/Prototypeメニューまたはバッチ実行から利用できる。結果と確認画像はLogsに出力する。
    public static class PrototypeValidation
    {
        // Play Mode開始後、検証前の待機に使用する更新回数。
        private static int waitFrames;
        // 今回の検証で成功したチェックの件数。
        private static int passed;
        // Play Modeへの移行によるスクリプト再読み込みをまたいで、検証待ち状態を保持するキー。
        private const string Pending = "Prototype.Validation.Pending";

        // シーンを再生成してから検証する入口。既存の生成内容を更新する点に注意。
        public static void BuildAndValidate()
        {
            PrototypeBuilder.Build();
            Validate();
        }

        // 保存済みの試作シーンを開き、検証待ちの印を付けてPlay Modeへ移行する。
        [MenuItem("Tools/Prototype/Validate gameplay in Play Mode")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validating.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        // スクリプト読み込み後に監視処理を登録し直す。先に解除して二重登録を防ぐ。
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update += WaitForPlay;
        }

        // Play Modeへの移行と初期化を待ってから検証する。
        // バッチ実行では終了コード0が成功、1が失敗。通常実行ではPlay Modeを終了する。
        private static void WaitForPlay()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            // この監視処理が30回呼ばれるまで待ち、シーン内のStartなどが動く猶予を置く。
            if (++waitFrames < 30) return;
            SessionState.SetBool(Pending, false);
            try
            {
                RunChecks();
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) CapturePreview();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/PrototypeValidation.txt", $"PASS: {passed} gameplay checks\n");
                Debug.Log($"PROTOTYPE VALIDATION PASS: {passed} checks");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                else EditorApplication.isPlaying = false;
            }
            // exception：検証失敗の理由。ログと終了コードに反映する。
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorApplication.isPlaying = false;
            }
        }

        // 条件がfalseなら理由付きの例外で中断し、trueなら成功数を増やしてログに残す。
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            passed++;
            Debug.Log("PASS: " + description);
        }

        // 各条件を順に確認する本体。自動Updateを止め、入力や時間を直接指定して再現性を高める。
        private static void RunChecks()
        {
            // ゲーム進行と当たり判定を管理するコンポーネント。
            var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeArena>();
            // 操作するプレイヤーの入力・移動コンポーネント。
            var player = UnityEngine.Object.FindFirstObjectByType<DragPlayer>();
            // 検証対象となる水風船。
            var ball = UnityEngine.Object.FindFirstObjectByType<WaterBalloon>();
            // 画面座標の変換や描画に使うカメラ。
            Camera camera = Camera.main;
            arena.enabled = false;
            arena.ResetRound();
            Check(player != null && ball != null && camera.orthographic, "Saved scene references and top-down camera");
            Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length == 0, "No UI canvas");
            // 入力の検証：押した瞬間の位置保持、ドラッグの向き、移動範囲、離す瞬間、タッチの互換性。
            Vector3 start = player.transform.position;
            player.FeedPointer(true, new Vector2(300, 300), -1, camera, PrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position == start, "Press does not teleport player");
            player.FeedPointer(true, new Vector2(360, 350), -1, camera, PrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position.x > start.x && player.transform.position.z > start.z, "Mouse drag moves right and up");
            player.FeedPointer(true, new Vector2(100000, 100000), -1, camera, PrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position.x <= 4.1f && player.transform.position.z <= 3.7f, "Drag remains inside play area");
            player.FeedPointer(false, Vector2.zero, -1, camera, PrototypeArena.MovementBounds, 0.016f);
            Check(player.ReleasedThisFrame && !player.IsHeld, "Mouse release detected");
            player.FeedPointer(true, new Vector2(30, 30), 42, camera, PrototypeArena.MovementBounds, 0.016f);
            // タッチ移動を始める前の位置。移動方向を確認する基準。
            Vector3 touchStart = player.transform.position;
            player.FeedPointer(true, new Vector2(10, 10), 42, camera, PrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position.x < touchStart.x && player.transform.position.z < touchStart.z, "Touch uses the same drag mapping");

            // 公転の検証：静止中は強化されず、移動しながら実際に3周すると強さが上がる。
            Vector3 anchor = new Vector3(0f, 0.65f, -4f);
            ball.ResetBalloon(anchor);
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < 2400; i++) ball.Simulate(anchor, Vector3.zero, true, 1f / 120f);
            Check(ball.Power == 1, "Holding still orbits without charging");
            // 角度が増える左回りと、角度が減る右回りの両方で強さ1→2→3へ進むことを確認する。
            CheckChargeDirection(ball, anchor, 1);
            CheckChargeDirection(ball, anchor, -1);
            ball.ResetBalloon(anchor);
            // 小さな円を描いてプレイヤーを動かす条件を作り、段階アップと紐の最大長を確認する。
            int lastPower = 1;
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < 3000; i++)
            {
                // シミュレーション開始からの経過時間（秒）。
                float t = i / 120f;
                // 小さな円を描いて動かす、検証用のプレイヤー位置。
                Vector3 movingAnchor = anchor + new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t)) * 0.4f;
                // 円運動する検証用プレイヤーの速度。
                Vector3 velocity = new Vector3(Mathf.Cos(t), 0f, -Mathf.Sin(t)) * 0.4f;
                ball.Simulate(movingAnchor, velocity, true, 1f / 120f);
                if (ball.Power != lastPower)
                {
                    Check(ball.ChargedRevolutions >= 3f * (ball.Power - 1), "Strength advances after three real revolutions");
                    lastPower = ball.Power;
                }
                if ((ball.transform.position - movingAnchor).magnitude > 2.251f) throw new Exception("Tether exceeded maximum length");
            }
            Check(ball.Power == 3, "Moving charges to tier 3 and caps there");
            // 投擲の検証：公転速度の引き継ぎ、直線飛行、二重投擲の禁止、回復、フリックの加算。
            Vector3 orbitVelocity = ball.Velocity;
            Check(ball.Launch(Vector3.zero) && Vector3.Distance(ball.Velocity, orbitVelocity) < 0.001f, "Release retains world velocity");
            // 投擲直後の水風船の位置。次の移動が速度どおりかを確認する基準。
            Vector3 launched = ball.transform.position;
            ball.Simulate(anchor, Vector3.zero, false, 0.01f);
            Check(Vector3.Distance(ball.transform.position, launched + orbitVelocity * 0.01f) < 0.001f, "Ball flies along its release vector");
            Check(!ball.Launch(Vector3.forward), "Cannot launch twice while in flight");
            ball.Consume();
            ball.Simulate(anchor, Vector3.zero, false, 1f);
            Check(ball.State == WaterBalloon.MotionState.Ready && ball.Power == 1, "Ball recovers and resets strength");
            ball.Simulate(anchor, Vector3.zero, true, 0.01f);
            // フリックを加える前の水風船速度。加速量の比較に使う。
            Vector3 beforeFlick = ball.Velocity;
            ball.Launch(Vector3.forward * 10f);
            Check(ball.Velocity.z > beforeFlick.z + 7f, "Flick adds directional force");
            Check(WaterBalloon.DamageAtSpeed(20f) > WaterBalloon.DamageAtSpeed(5f), "Faster throws deal more damage");
            // 連続衝突判定の検証：1回で遠くへ動いても、通過した対象への接触を検出できる。
            Check(PrototypeArena.Sweep(new Vector3(-10, 0, 0), new Vector3(10, 0, 0), 0.5f, out _), "Fast projectile sweep cannot tunnel");
            Check(!PrototypeArena.Sweep(new Vector3(-10, 0, 2), new Vector3(10, 0, 2), 0.5f, out _), "Sweep rejects a near miss");

            arena.ResetRound();
            anchor = player.transform.position;
            ball.Simulate(anchor, Vector3.zero, true, 0.01f);
            // 弾消しの検証：同じ強さの弾は消せるが、強い弾や紐だけへの接触では消せない。
            arena.SpawnBullet(ball.transform.position, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ActiveBulletCount == 0, "Equal-tier projectile is erased by the ball");
            // 弾消しのヒットストップを終えてから、次の弾の強さを検証する。
            arena.AdvanceHitStop(1f);
            arena.SpawnBullet(ball.transform.position, Vector3.zero, 3);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ActiveBulletCount == 1, "Stronger projectile passes through a weaker ball");
            arena.ResetRound();
            ball.Simulate(anchor, Vector3.zero, true, 0.01f);
            arena.SpawnBullet(Vector3.Lerp(anchor, ball.transform.position, 0.5f), Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ActiveBulletCount == 1, "Tether does not erase projectiles");

            arena.ResetRound();
            arena.BeginReleaseProtection();
            // パリィの検証：受付中に強さ3の弾へ触れると、離れた場所にある弾も含めて消える。
            arena.SpawnBullet(anchor, Vector3.zero, 3);
            arena.SpawnBullet(anchor + Vector3.right * 2f, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ParryCount == 1 && arena.ActiveBulletCount == 0 && arena.State == PrototypeArena.RoundState.Playing,
                "Timed release against a strong attack parries and clears every projectile");
            arena.ResetRound();
            arena.BeginReleaseProtection();
            arena.SpawnBullet(anchor, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            // 弱い弾は全弾消去の条件にならないが、投擲直後の無敵で失敗は防げる。
            Check(arena.State == PrototypeArena.RoundState.Playing && arena.ParryCount == 0, "Release grants brief protection without parrying a weak attack");
            arena.ResetRound();
            arena.SpawnBullet(anchor, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.State == PrototypeArena.RoundState.Failed, "One unprotected hit fails the round");
            arena.ResetRound();
            Check(arena.State == PrototypeArena.RoundState.Playing && arena.ActiveBulletCount == 0 && arena.BossHealth == 150f,
                "Retry resets health, bullets and round state");

            // ボスの検証：胴体ではHPが減らず、弱点への投擲を繰り返すとクリアする。
            float initialHealth = arena.BossHealth;
            ThrowAt(ball, arena, new Vector3(1.2f, 0.65f, 5.6f));
            Check(arena.BossHealth == initialHealth, "Boss armor takes no damage");
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < 30 && arena.State == PrototypeArena.RoundState.Playing; i++)
                ThrowAt(ball, arena, new Vector3(0f, 0.65f, 4.45f));
            Check(arena.State == PrototypeArena.RoundState.Cleared && arena.BossHealth == 0f, "Weak-point throws deplete HP and clear the round");
            arena.ResetRound();
            RunParameterChecks(arena, player, ball);
            RunHitStopChecks(arena, player, ball, camera);
        }

        // arena/player/ball/cameraは検証シーンの対象。命中時の停止・復帰と停止中の入力を確認する。
        private static void RunHitStopChecks(PrototypeArena arena, DragPlayer player, WaterBalloon ball, Camera camera)
        {
            arena.ResetRound();
            ThrowAt(ball, arena, new Vector3(0f, 0.65f, 4.45f), finishHitStop: false);
            Check(arena.IsHitStopped && ball.State == WaterBalloon.MotionState.Flying,
                "Weak-point hit freezes gameplay and keeps cue visible until stop ends");
            // 命中後のHP。停止中にもう一度判定しても二重ダメージが入らないことを確認する。
            float healthAfterHit = arena.BossHealth;
            arena.CheckBossHit();
            Check(arena.BossHealth == healthAfterHit, "Hit stop prevents duplicate damage");
            // 停止中と復帰後の位置を確認する、命中位置から離れた敵弾。
            var bullet = arena.SpawnBullet(new Vector3(-3f, 0.65f, -1f), Vector3.forward, 1);
            // 停止開始時点の敵弾の位置。
            Vector3 bulletPosition = bullet.transform.position;
            // 停止開始時点のキューの位置。
            Vector3 ballPosition = ball.transform.position;
            // 停止開始時点のプレイヤーの位置。
            Vector3 playerPosition = player.transform.position;
            // ヒットストップが変更してはいけないゲーム全体の時間倍率。
            float timeScale = Time.timeScale;
            arena.AdvanceFrame(0.5f, 0.04f);
            Check(arena.IsHitStopped && bullet.transform.position == bulletPosition
                && ball.transform.position == ballPosition && player.transform.position == playerPosition,
                "Hit stop freezes player, cue and enemy bullets using real time");
            arena.AdvanceFrame(0.5f, 0.05f);
            Check(!arena.IsHitStopped && ball.State == WaterBalloon.MotionState.Recovering
                && bullet.transform.position == bulletPosition && Time.timeScale == timeScale,
                "Hit stop ends on real time and consumes cue without changing global time scale");
            arena.AdvanceFrame(0.01f, 0.01f);
            Check(bullet.transform.position.z > bulletPosition.z, "Enemy bullets resume after hit stop");

            arena.ResetRound();
            ThrowAt(ball, arena, new Vector3(1.2f, 0.65f, 5.6f), finishHitStop: false);
            Check(arena.IsHitStopped && arena.BossHealth == 150f, "Armor hit stops briefly without damage");
            arena.AdvanceHitStop(0.05f);
            Check(!arena.IsHitStopped, "Armor hit stop uses the shorter duration");

            arena.ResetRound();
            ball.Simulate(player.transform.position, Vector3.zero, true, 0f);
            arena.SpawnBullet(ball.transform.position, Vector3.zero, 1);
            arena.SpawnBullet(ball.transform.position, Vector3.zero, 1);
            arena.TickBullets(player.transform.position, player.transform.position, 0f);
            Check(arena.IsHitStopped && arena.ActiveBulletCount == 0, "Cue blocking bullets starts hit stop");
            arena.AdvanceHitStop(0.03f);
            Check(!arena.IsHitStopped && ball.State == WaterBalloon.MotionState.Orbiting,
                "Simultaneous blocks do not stack stop durations or consume orbiting cue");

            arena.ResetRound();
            ThrowAt(ball, arena, new Vector3(0f, 0.65f, 4.45f), finishHitStop: false);
            arena.ResetRound();
            Check(!arena.IsHitStopped && ball.State == WaterBalloon.MotionState.Ready,
                "Retry cancels pending hit stop and consumption");
            ThrowAt(ball, arena, new Vector3(0f, 0.65f, 4.45f), finishHitStop: false);
            arena.enabled = true;
            arena.enabled = false;
            Check(!arena.IsHitStopped, "Disabling arena clears hit stop");

            // Inspectorで0秒を指定した場合の挙動を確認し、設定を元に戻す。
            var serializedArena = new SerializedObject(arena);
            // 弱点命中時の停止時間の保存対象フィールド。
            var durationProperty = serializedArena.FindProperty("weakPointHitStop");
            // 検証後に復元する、元の停止時間。
            float originalDuration = durationProperty.floatValue;
            try
            {
                durationProperty.floatValue = 0f;
                serializedArena.ApplyModifiedPropertiesWithoutUndo();
                arena.ResetRound();
                ThrowAt(ball, arena, new Vector3(0f, 0.65f, 4.45f), finishHitStop: false);
                Check(!arena.IsHitStopped && ball.State == WaterBalloon.MotionState.Recovering,
                    "Zero duration disables hit stop and consumes cue immediately");
            }
            finally
            {
                durationProperty.floatValue = originalDuration;
                serializedArena.ApplyModifiedPropertiesWithoutUndo();
                arena.ResetRound();
            }

            player.FeedPointer(true, new Vector2(300, 300), -1, camera, PrototypeArena.MovementBounds, 0.016f);
            player.FeedPointer(true, new Vector2(310, 300), -1, camera, PrototypeArena.MovementBounds, 0.016f);
            // 移動停止前のプレイヤー位置。停止中に大きくドラッグしても動かないことを確認する。
            Vector3 frozenPosition = player.transform.position;
            player.FeedPointer(true, new Vector2(700, 600), -1, camera, PrototypeArena.MovementBounds, 0.016f, freezeMovement: true);
            Check(player.transform.position == frozenPosition && player.Velocity == Vector3.zero,
                "Frozen input tracks pointer without moving player");
            player.FeedPointer(true, new Vector2(700, 600), -1, camera, PrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position == frozenPosition, "Resuming input does not jump by frozen drag distance");
            player.FeedPointer(false, Vector2.zero, -1, camera, PrototypeArena.MovementBounds, 0.016f, freezeMovement: true);
            ball.ResetBalloon(player.transform.position);
            ball.Simulate(player.transform.position, Vector3.zero, true, 0f);
            arena.AdvanceFrame(0.001f, 0.001f);
            Check(ball.State == WaterBalloon.MotionState.Flying && !player.TryConsumeBufferedRelease(out _),
                "Release during hit stop launches once on resume");
            arena.ResetRound();
        }

        // ballは対象の水風船、anchorは支点、directionは角度を増やす場合1、減らす場合-1。
        // 軌道上に2度ずつ配置し、時間を進めずに強化判定だけを通して回転方向の不具合を再現する。
        private static void CheckChargeDirection(WaterBalloon ball, Vector3 anchor, int direction)
        {
            ball.ResetBalloon(anchor);
            // ログに表示する回転方向。上から見たX/Z平面で区別する。
            string label = direction > 0 ? "Counterclockwise" : "Clockwise";
            // iは2度単位の更新回数。1200回で約6.67周となり、強さ3までの閾値を越える。
            for (int i = 1; i <= 1200; i++)
            {
                // 初期位置の-90度から指定方向へ進めた角度（ラジアン）。
                float angle = (-90f + direction * i * 2f) * Mathf.Deg2Rad;
                ball.transform.position = anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // 加算前の進捗。±180度をまたぐ更新や逆回転でも減らないことを確認する。
                float previousCharge = ball.ChargedRevolutions;
                ball.Simulate(anchor, Vector3.right, true, 0f);
                if (ball.ChargedRevolutions + 0.0001f < previousCharge)
                    throw new InvalidOperationException(label + " rotation reduced charge");
                if (i == 480) Check(ball.Power == 1, label + " remains tier one before three revolutions");
                if (i == 600) Check(ball.Power == 2, label + " reaches tier two after three revolutions");
            }
            Check(ball.Power == 3, label + " reaches tier three after six revolutions");
        }

        // 6種類の補正を1つずつ変更し、実際の判定・投擲・成長・ダメージ・回復へ反映されるか確認する。
        // arena/player/ballは保存済みシーンの対象。検証後は補正を解除しラウンドを初期化する。
        private static void RunParameterChecks(PrototypeArena arena, DragPlayer player, WaterBalloon ball)
        {
            // スキル管理が後から操作するのと同じ共有パラメーター。
            var parameters = player.Parameters;
            parameters.ResetPassiveMultipliers();
            try
            {
                arena.ResetRound();
                // パリィ半径の標準外・2倍時の内側に置く検証用の弾位置。
                Vector3 parryPosition = player.transform.position + Vector3.right * 0.8f;
                arena.BeginReleaseProtection();
                arena.SpawnBullet(parryPosition, Vector3.zero, 3);
                arena.TickBullets(player.transform.position, player.transform.position, 0f);
                Check(arena.ParryCount == 0, "Default parry range excludes distant projectile");
                parameters.SetPassiveMultipliers(parryRange: 2f);
                arena.TickBullets(player.transform.position, player.transform.position, 0f);
                Check(arena.ParryCount == 1, "Expanded parry range includes distant projectile immediately");

                parameters.ResetPassiveMultipliers();
                arena.ResetRound();
                ball.Simulate(player.transform.position, Vector3.zero, true, 0f);
                ball.Launch(Vector3.forward * 10f);
                // 同じフリックで投げた場合の基準速度。
                float normalSpeed = ball.Velocity.magnitude;
                parameters.SetPassiveMultipliers(cueSpeed: 2f);
                ball.ResetBalloon(player.transform.position);
                ball.Simulate(player.transform.position, Vector3.zero, true, 0f);
                ball.Launch(Vector3.forward * 10f);
                Check(Mathf.Abs(ball.Velocity.magnitude - normalSpeed * 2f) < 0.001f, "Cue speed doubles launch speed for the same flick");

                parameters.ResetPassiveMultipliers();
                // 標準倍率で強さ2に到達するまでのシミュレーション回数。
                int normalGrowthFrames = FramesUntilTierTwo(ball, player.transform.position);
                parameters.SetPassiveMultipliers(cueGrowth: 2f);
                // 成長2倍で同じ段階に到達するまでの回数。
                int fastGrowthFrames = FramesUntilTierTwo(ball, player.transform.position);
                Check(fastGrowthFrames < normalGrowthFrames * 0.75f, "Cue growth multiplier reaches tier two earlier");

                parameters.ResetPassiveMultipliers();
                arena.ResetRound();
                // ボス弱点の位置。既存の試作シーンに合わせて指定する。
                Vector3 weakTarget = new Vector3(0f, 0.65f, 4.45f);
                ThrowAt(ball, arena, weakTarget);
                // 標準攻撃力で減ったHP。
                float normalDamage = 150f - arena.BossHealth;
                parameters.SetPassiveMultipliers(attack: 2f);
                arena.ResetRound();
                ThrowAt(ball, arena, weakTarget);
                Check(normalDamage > 0f && Mathf.Abs((150f - arena.BossHealth) - normalDamage * 2f) < 0.01f,
                    "Attack multiplier doubles actual weak-point damage");

                parameters.ResetPassiveMultipliers();
                ball.Consume();
                ball.Simulate(player.transform.position, Vector3.zero, false, 0.34f);
                Check(ball.State == WaterBalloon.MotionState.Recovering, "Default reproduction still waits after 0.34 seconds");
                parameters.SetPassiveMultipliers(cueReproduction: 2f);
                ball.Consume();
                ball.Simulate(player.transform.position, Vector3.zero, false, 0.34f);
                Check(ball.State == WaterBalloon.MotionState.Ready, "Double reproduction speed halves recovery time");

                parameters.ResetPassiveMultipliers();
                arena.ResetRound();
                ball.Simulate(player.transform.position, Vector3.zero, true, 0f);
                arena.SpawnBullet(ball.transform.position + Vector3.right * 0.75f, Vector3.zero, 1);
                arena.TickBullets(player.transform.position, player.transform.position, 0f);
                Check(arena.ActiveBulletCount == 1, "Default cue radius excludes distant projectile");
                parameters.SetPassiveMultipliers(cueHitRange: 2f);
                arena.TickBullets(player.transform.position, player.transform.position, 0f);
                Check(arena.ActiveBulletCount == 0 && Mathf.Approximately(ball.HitRadius, 0.8f),
                    "Expanded cue radius erases distant projectile");
                arena.ResetRound();
                Check(Mathf.Approximately(player.Parameters.CueHitRange, 2f), "Retry preserves equipped passive modifiers");
                parameters.ResetPassiveMultipliers();
                Check(Mathf.Approximately(parameters.ParryRange, 1f) && Mathf.Approximately(parameters.CueSpeed, 1f)
                    && Mathf.Approximately(parameters.CueGrowth, 1f) && Mathf.Approximately(parameters.Attack, 1f)
                    && Mathf.Approximately(parameters.CueReproduction, 1f) && Mathf.Approximately(parameters.CueHitRange, 1f),
                    "Removing passives restores all six defaults");
            }
            finally
            {
                parameters.ResetPassiveMultipliers();
                arena.ResetRound();
            }
        }

        // ballは検証する水風船、anchorはプレイヤーの基準位置。
        // 同じ円運動を再現し、強さ2になるまでの更新回数を返す。到達しない場合は検証失敗とする。
        private static int FramesUntilTierTwo(WaterBalloon ball, Vector3 anchor)
        {
            ball.ResetBalloon(anchor);
            // iは固定時間刻みで進めた更新回数。最大6000回まで確認する。
            for (int i = 0; i < 6000; i++)
            {
                // シミュレーションの経過時間（秒）。
                float time = i / 120f;
                // 半径0.4の円を描くプレイヤーの位置。
                Vector3 position = anchor + new Vector3(Mathf.Sin(time), 0f, Mathf.Cos(time)) * 0.4f;
                // 上記の円運動に対応するプレイヤー速度。
                Vector3 velocity = new Vector3(Mathf.Cos(time), 0f, -Mathf.Sin(time)) * 0.4f;
                ball.Simulate(position, velocity, true, 1f / 120f);
                if (ball.Power >= 2) return i + 1;
            }
            throw new InvalidOperationException("Cue did not grow to tier two");
        }

        // 対象位置に風船を配置して投擲状態を作り、ボスとの接触判定だけを実行する検証用補助。
        // finishHitStop=trueなら命中演出の完了まで進め、連続した投擲検証を可能にする。
        private static void ThrowAt(WaterBalloon ball, PrototypeArena arena, Vector3 target, bool finishHitStop = true)
        {
            arena.AdvanceHitStop(1f);
            ball.ResetBalloon(target + Vector3.forward * 1.35f);
            ball.Simulate(target + Vector3.forward * 1.35f, Vector3.zero, true, 0.001f);
            ball.Launch(Vector3.forward * 18f);
            arena.CheckBossHit();
            if (finishHitStop) arena.AdvanceHitStop(1f);
        }

        // 見た目確認用に風船と3段階の弾を配置し、540×960の縦長PNGをLogsへ保存する。
        private static void CapturePreview()
        {
            // ゲーム進行と当たり判定を管理するコンポーネント。
            var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeArena>();
            // 操作するプレイヤーの入力・移動コンポーネント。
            var player = UnityEngine.Object.FindFirstObjectByType<DragPlayer>();
            // 検証対象となる水風船。
            var ball = UnityEngine.Object.FindFirstObjectByType<WaterBalloon>();
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < 120; i++) ball.Simulate(player.transform.position, Vector3.zero, true, 1f / 120f);
            // row：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int row = 0; row < 3; row++)
                // j：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
                for (int j = -2; j <= 2; j++)
                    arena.SpawnBullet(new Vector3(j * 1.1f, 0.65f, 2.7f - row * 2f - Mathf.Abs(j) * 0.3f), Vector3.zero, row + 1);
            // 画面座標の変換や描画に使うカメラ。
            Camera camera = Camera.main;
            // 確認画像を描画する540×960の一時的な描画先。
            var texture = new RenderTexture(540, 960, 24);
            // 撮影中だけ描画先を専用テクスチャへ切り替えるため、元の状態を退避する。
            var previous = RenderTexture.active;
            // 撮影前にカメラが使っていた描画先。撮影後に戻す。
            var previousTarget = camera.targetTexture;
            // 撮影前のカメラの横幅÷高さ。撮影後に戻す。
            float previousAspect = camera.aspect;
            camera.targetTexture = texture;
            camera.aspect = 540f / 960f;
            camera.orthographicSize = 9f;
            camera.Render();
            RenderTexture.active = texture;
            // 描画結果を読み取り、PNGへ変換するための画像データ。
            var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/PrototypePreview.png", image.EncodeToPNG());
            // 描画先と縦横比を元に戻し、撮影用リソースを解放してラウンドも初期化する。
            RenderTexture.active = previous;
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            UnityEngine.Object.Destroy(image);
            texture.Release();
            UnityEngine.Object.Destroy(texture);
            arena.ResetRound();
        }
    }
}
