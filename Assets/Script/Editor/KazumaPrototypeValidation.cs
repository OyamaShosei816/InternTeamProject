using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KazumaPrototype.Editor
{
    // Unityエディター専用。保存済みシーンを実際のPlay Modeで動かし、操作・衝突・勝敗を検証する。
    // Tools/Kazumaメニューまたはバッチ実行から利用できる。結果と確認画像はLogsに出力する。
    public static class KazumaPrototypeValidation
    {
        // Play Mode開始後、検証前の待機に使用する更新回数。
        private static int waitFrames;
        // 今回の検証で成功したチェックの件数。
        private static int passed;
        // Play Modeへの移行によるスクリプト再読み込みをまたいで、検証待ち状態を保持するキー。
        private const string Pending = "Kazuma.Validation.Pending";

        // シーンを再生成してから検証する入口。既存の生成内容を更新する点に注意。
        public static void BuildAndValidate()
        {
            KazumaPrototypeBuilder.Build();
            Validate();
        }

        // 保存済みの試作シーンを開き、検証待ちの印を付けてPlay Modeへ移行する。
        [MenuItem("Tools/Kazuma/Validate gameplay in Play Mode")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validating.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(KazumaPrototypeBuilder.ScenePath);
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
                File.WriteAllText("Logs/KazumaValidation.txt", $"PASS: {passed} gameplay checks\n");
                Debug.Log($"KAZUMA VALIDATION PASS: {passed} checks");
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
            var arena = UnityEngine.Object.FindFirstObjectByType<KazumaPrototypeArena>();
            // 操作するプレイヤーの入力・移動コンポーネント。
            var player = UnityEngine.Object.FindFirstObjectByType<KazumaDragPlayer>();
            // 検証対象となる水風船。
            var ball = UnityEngine.Object.FindFirstObjectByType<KazumaWaterBalloon>();
            // 画面座標の変換や描画に使うカメラ。
            Camera camera = Camera.main;
            arena.enabled = false;
            arena.ResetRound();
            Check(player != null && ball != null && camera.orthographic, "Saved scene references and top-down camera");
            Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length == 0, "No UI canvas");
            // 入力の検証：押した瞬間の位置保持、ドラッグの向き、移動範囲、離す瞬間、タッチの互換性。
            Vector3 start = player.transform.position;
            player.FeedPointer(true, new Vector2(300, 300), -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position == start, "Press does not teleport player");
            player.FeedPointer(true, new Vector2(360, 350), -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position.x > start.x && player.transform.position.z > start.z, "Mouse drag moves right and up");
            player.FeedPointer(true, new Vector2(100000, 100000), -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position.x <= 4.1f && player.transform.position.z <= 3.7f, "Drag remains inside play area");
            player.FeedPointer(false, Vector2.zero, -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            Check(player.ReleasedThisFrame && !player.IsHeld, "Mouse release detected");
            player.FeedPointer(true, new Vector2(30, 30), 42, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            // タッチ移動を始める前の位置。移動方向を確認する基準。
            Vector3 touchStart = player.transform.position;
            player.FeedPointer(true, new Vector2(10, 10), 42, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
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
            Check(ball.State == KazumaWaterBalloon.MotionState.Ready && ball.Power == 1, "Ball recovers and resets strength");
            ball.Simulate(anchor, Vector3.zero, true, 0.01f);
            // フリックを加える前の水風船速度。加速量の比較に使う。
            Vector3 beforeFlick = ball.Velocity;
            ball.Launch(Vector3.forward * 10f);
            Check(ball.Velocity.z > beforeFlick.z + 7f, "Flick adds directional force");
            Check(KazumaWaterBalloon.DamageAtSpeed(20f) > KazumaWaterBalloon.DamageAtSpeed(5f), "Faster throws deal more damage");
            // 連続衝突判定の検証：1回で遠くへ動いても、通過した対象への接触を検出できる。
            Check(KazumaPrototypeArena.Sweep(new Vector3(-10, 0, 0), new Vector3(10, 0, 0), 0.5f, out _), "Fast projectile sweep cannot tunnel");
            Check(!KazumaPrototypeArena.Sweep(new Vector3(-10, 0, 2), new Vector3(10, 0, 2), 0.5f, out _), "Sweep rejects a near miss");

            arena.ResetRound();
            anchor = player.transform.position;
            ball.Simulate(anchor, Vector3.zero, true, 0.01f);
            // 弾消しの検証：同じ強さの弾は消せるが、強い弾や紐だけへの接触では消せない。
            arena.SpawnBullet(ball.transform.position, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ActiveBulletCount == 0, "Equal-tier projectile is erased by the ball");
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
            Check(arena.ParryCount == 1 && arena.ActiveBulletCount == 0 && arena.State == KazumaPrototypeArena.RoundState.Playing,
                "Timed release against a strong attack parries and clears every projectile");
            arena.ResetRound();
            arena.BeginReleaseProtection();
            arena.SpawnBullet(anchor, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            // 弱い弾は全弾消去の条件にならないが、投擲直後の無敵で失敗は防げる。
            Check(arena.State == KazumaPrototypeArena.RoundState.Playing && arena.ParryCount == 0, "Release grants brief protection without parrying a weak attack");
            arena.ResetRound();
            arena.SpawnBullet(anchor, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.State == KazumaPrototypeArena.RoundState.Failed, "One unprotected hit fails the round");
            arena.ResetRound();
            Check(arena.State == KazumaPrototypeArena.RoundState.Playing && arena.ActiveBulletCount == 0 && arena.BossHealth == 150f,
                "Retry resets health, bullets and round state");

            // ボスの検証：胴体ではHPが減らず、弱点への投擲を繰り返すとクリアする。
            float initialHealth = arena.BossHealth;
            ThrowAt(ball, arena, new Vector3(1.2f, 0.65f, 5.6f));
            Check(arena.BossHealth == initialHealth, "Boss armor takes no damage");
            // i：この繰り返しで処理する対象の番号。条件を満たす間、順番に更新する。
            for (int i = 0; i < 30 && arena.State == KazumaPrototypeArena.RoundState.Playing; i++)
                ThrowAt(ball, arena, new Vector3(0f, 0.65f, 4.45f));
            Check(arena.State == KazumaPrototypeArena.RoundState.Cleared && arena.BossHealth == 0f, "Weak-point throws deplete HP and clear the round");
            arena.ResetRound();
            RunParameterChecks(arena, player, ball);
        }

        // ballは対象の水風船、anchorは支点、directionは角度を増やす場合1、減らす場合-1。
        // 軌道上に2度ずつ配置し、時間を進めずに強化判定だけを通して回転方向の不具合を再現する。
        private static void CheckChargeDirection(KazumaWaterBalloon ball, Vector3 anchor, int direction)
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
        private static void RunParameterChecks(KazumaPrototypeArena arena, KazumaDragPlayer player, KazumaWaterBalloon ball)
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
                Check(ball.State == KazumaWaterBalloon.MotionState.Recovering, "Default reproduction still waits after 0.34 seconds");
                parameters.SetPassiveMultipliers(cueReproduction: 2f);
                ball.Consume();
                ball.Simulate(player.transform.position, Vector3.zero, false, 0.34f);
                Check(ball.State == KazumaWaterBalloon.MotionState.Ready, "Double reproduction speed halves recovery time");

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
        private static int FramesUntilTierTwo(KazumaWaterBalloon ball, Vector3 anchor)
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
        private static void ThrowAt(KazumaWaterBalloon ball, KazumaPrototypeArena arena, Vector3 target)
        {
            ball.ResetBalloon(target + Vector3.forward * 1.35f);
            ball.Simulate(target + Vector3.forward * 1.35f, Vector3.zero, true, 0.001f);
            ball.Launch(Vector3.forward * 18f);
            arena.CheckBossHit();
        }

        // 見た目確認用に風船と3段階の弾を配置し、540×960の縦長PNGをLogsへ保存する。
        private static void CapturePreview()
        {
            // ゲーム進行と当たり判定を管理するコンポーネント。
            var arena = UnityEngine.Object.FindFirstObjectByType<KazumaPrototypeArena>();
            // 操作するプレイヤーの入力・移動コンポーネント。
            var player = UnityEngine.Object.FindFirstObjectByType<KazumaDragPlayer>();
            // 検証対象となる水風船。
            var ball = UnityEngine.Object.FindFirstObjectByType<KazumaWaterBalloon>();
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
            File.WriteAllBytes("Logs/KazumaPreview.png", image.EncodeToPNG());
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
