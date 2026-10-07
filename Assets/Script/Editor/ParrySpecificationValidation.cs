using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KazumaPrototype.Editor
{
    // 新しい残機・SE連携を持つ実装で、パリィ仕様だけを独立して検証する。
    public static class ParrySpecificationValidation
    {
        // Play Modeへの移行をまたいで検証待ちを保持するキー。
        private const string Pending = "ParrySpecificationValidation.Pending";
        // 空の検証シーンではなく、保存済みPlayerParryシーンで検証する場合の印。
        private const string UsePlayerParryScene = "ParrySpecificationValidation.PlayerParry";
        // 起動処理の完了を待つフレーム数。
        private static int frames;
        // 今回成功した検証の数。
        private static int passed;
        // 空の検証用シーンを作り、保存済みシーンを変更せず検証を開始する。
        [MenuItem("Tools/Prototype/Validate parry specification")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validation.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(UsePlayerParryScene, false);
            frames = passed = 0;
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
        // PlayerParryを実際に開き、シーン参照・パリィ・左右のキュー強化をPlay Modeで検証する。
        [MenuItem("Tools/Prototype/Validate PlayerParry scene")]
        public static void ValidatePlayerParry()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validation.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/PlayerParry.unity");
            SessionState.SetBool(UsePlayerParryScene, true);
            frames = passed = 0;
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
        // ドメイン再読み込み後も検証待ちを監視する。
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            EditorApplication.update -= Wait;
            EditorApplication.update += Wait;
        }
        // Play Modeでフィクスチャを作り、通常のパリィ処理を通して検証する。
        private static void Wait()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (++frames < 20) return;
            SessionState.SetBool(Pending, false);
            try
            {
                if (SessionState.GetBool(UsePlayerParryScene, false)) RunPlayerParryScene();
                else Run();
                Debug.Log($"PARRY SPECIFICATION PASS: {passed} checks");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                else EditorApplication.isPlaying = false;
            }
            // errorは失敗の理由。バッチ検証では失敗コードを返す。
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorApplication.isPlaying = false;
            }
        }
        // 保存されたシーンの参照をそのまま使い、UIやSEを含む構成で仕様を確認する。
        private static void RunPlayerParryScene()
        {
            // シーンに配置されているゲーム管理コンポーネント。
            var arena = UnityEngine.Object.FindFirstObjectByType<KazumaPrototypeArena>();
            // シーン内の入力とキャラクターパラメーターを管理するプレイヤー。
            var player = UnityEngine.Object.FindFirstObjectByType<KazumaDragPlayer>();
            // シーン内で表示・強化・投擲を行うキュー。
            var ball = UnityEngine.Object.FindFirstObjectByType<KazumaWaterBalloon>();
            Check(arena != null && player != null && ball != null, "PlayerParry scene loads all gameplay components");
            Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "PlayerParry",
                "Validation runs in the saved PlayerParry scene");
            // 手動検証中に実際のUpdateが進行しないよう一時的に停止する。
            arena.enabled = false;
            // パリィ後も残機UIが欠けず、すべて元の表示に戻せることを確認する。
            GameObject[] stocks = (GameObject[])typeof(KazumaPrototypeArena)
                .GetField("stockObjects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena);
            Check(stocks != null && stocks.Length == 3 && Array.TrueForAll(stocks, stock => stock != null),
                "PlayerParry retains all three stock UI references");
            Check(SoundManager.Instance != null, "PlayerParry retains its sound manager");
            arena.ResetRound();
            Check(Mathf.Approximately(arena.ParryRadius, 0.6f), "PlayerParry uses the configured 0.6 parry radius");
            RunPlayerParryChecks(arena, player, ball);
            Check(Array.TrueForAll(stocks, stock => stock.activeSelf), "Parry checks preserve the scene's stock UI");
            CheckRotationDirection(ball, player.transform.position, 1);
            CheckRotationDirection(ball, player.transform.position, -1);
            // 左右を途中で切り替えても、蓄積済みの強化量が減らないことを確認する。
            ball.ResetBalloon(player.transform.position);
            ball.transform.position = player.transform.position + Vector3.right * 1.35f;
            ball.Simulate(player.transform.position, Vector3.right, true, 0f);
            // 反転直前の強化量（周回数）。
            float chargeBeforeReverse = ball.ChargedRevolutions;
            ball.transform.position = player.transform.position + Vector3.back * 1.35f;
            ball.Simulate(player.transform.position, Vector3.right, true, 0f);
            Check(ball.ChargedRevolutions > chargeBeforeReverse, "Changing rotation direction keeps adding charge");
            ball.ResetBalloon(player.transform.position);
            // iは静止したまま回し続ける時間ステップの番号。移動しない場合は強化されない。
            for (int i = 0; i < 2400; i++) ball.Simulate(player.transform.position, Vector3.zero, true, 1f / 120f);
            Check(ball.Power == 1, "Holding still does not charge without player movement");
            arena.ResetRound();
            RunImpactChecks(arena, player, ball);
            arena.enabled = true;
        }

        // 保存済みPlayerParryで、弾消しの輪と弱点命中時の停止・揺れ・入力復帰を確認する。
        private static void RunImpactChecks(KazumaPrototypeArena arena, KazumaDragPlayer player, KazumaWaterBalloon ball)
        {
            // シーンが使用しているカメラ。位置が揺れた後に確実に復元されるか確認する。
            Camera camera = (Camera)typeof(KazumaPrototypeArena).GetField("gameCamera", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena);
            // シーンで設定された弱点の位置。固定座標に依存せず、実際の配置で命中させる。
            Transform weakPoint = (Transform)typeof(KazumaPrototypeArena).GetField("weakPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena);
            // 検証中のプレイヤー位置。弾やキューの初期位置を決める基準。
            Vector3 anchor = player.transform.position;
            // flyingが0なら公転、1なら投擲。どちらも敵弾を消した際に同じ輪を表示する。
            for (int flying = 0; flying <= 1; flying++)
            {
                arena.ResetRound();
                ball.Simulate(anchor, Vector3.zero, true, 0f);
                if (flying == 1) ball.Launch(Vector3.forward);
                arena.SpawnBullet(ball.transform.position, Vector3.zero, 1);
                arena.SpawnBullet(ball.transform.position, Vector3.zero, 1);
                arena.TickBullets(anchor, anchor, 0f);
                // 同時に複数の弾を消した際の、表示中の弾消し外円。
                LineRenderer[] rings = EraseRings(arena);
                Check(arena.ActiveBulletCount == 0 && rings.Length == 1, $"Cue erase emits one ring for simultaneous contacts, flying {flying}");
                Check(rings[0].transform.position == ball.transform.position
                    && Mathf.Abs(rings[0].GetPosition(0).magnitude - ball.HitRadius) < 0.001f,
                    "Erase ring begins on the cue outer circumference");
                // 停止前のキュー状態。弾消しでは公転・投擲を消費せず、そのまま復帰させる。
                KazumaWaterBalloon.MotionState cueState = ball.State;
                Check(arena.IsHitStopped && !arena.IsCameraShaking && ball.CanHit,
                    $"Cue erase starts a short hit stop without weak-point shake, flying {flying}");
                // 停止を確認するため、キューやプレイヤーから離れた場所に動く敵弾を置く。
                KazumaBullet movingBullet = arena.SpawnBullet(anchor + Vector3.right * 3f, Vector3.forward, 1);
                // 停止開始時の敵弾位置。
                Vector3 movingBulletPosition = movingBullet.transform.position;
                // 停止開始時のキュー位置。
                Vector3 eraseCuePosition = ball.transform.position;
                arena.AdvanceFrame(0.1f, 0.01f);
                Check(arena.IsHitStopped && movingBullet.transform.position == movingBulletPosition
                    && ball.transform.position == eraseCuePosition && player.transform.position == anchor
                    && Mathf.Abs(rings[0].GetPosition(0).magnitude - ball.HitRadius) < 0.001f,
                    "Cue erase freezes gameplay and its ring while tracking real elapsed time");
                arena.AdvanceHitStop(0.014f);
                Check(arena.IsHitStopped, "Cue erase hit stop lasts through 0.024 seconds");
                arena.AdvanceHitStop(0.002f);
                Check(!arena.IsHitStopped && ball.State == cueState && ball.CanHit,
                    "Simultaneous erasures finish at 0.025 seconds without stacking or consuming the cue");
                arena.AdvanceFrame(0.1f, 0.1f);
                Check(rings[0].GetPosition(0).magnitude > ball.HitRadius, "Erase ring expands over time");
                Check(movingBullet.transform.position.z > movingBulletPosition.z, "Enemy bullets resume after the short erase hit stop");
                // iは経過フレーム。0.4秒進めて、輪の初期表示時間0.3秒を超える。
                for (int i = 0; i < 4; i++) arena.AdvanceFrame(0.1f, 0.1f);
                Check(EraseRings(arena).Length == 0, "Erase ring disappears after its configured lifetime");
            }
            arena.ResetRound();
            ball.Simulate(anchor, Vector3.zero, true, 0f);
            arena.SpawnBullet(ball.transform.position, Vector3.zero, 3);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ActiveBulletCount == 1 && EraseRings(arena).Length == 0, "A surviving enemy bullet does not emit an erase ring");
            Check(!arena.IsHitStopped, "A stronger surviving enemy bullet does not trigger erase hit stop");

            arena.ResetRound();
            // 揺れる前のカメラ位置。
            Vector3 cameraPosition = camera.transform.position;
            HitWeakPoint(arena, ball, weakPoint.position);
            // 命中後のボスHP。停止中に同じ衝突を判定し直しても減らないことを確認する。
            float health = arena.BossHealth;
            Check(arena.IsHitStopped && arena.IsCameraShaking && ball.State == KazumaWaterBalloon.MotionState.Flying
                && camera.transform.position != cameraPosition, "Weak-point impact freezes cue at contact and starts camera shake");
            arena.CheckBossHit();
            Check(arena.BossHealth == health, "Hit stop prevents duplicate weak-point damage and hit sounds");
            // 停止中の移動を確認するため、命中場所から離れて飛ぶ敵弾を置く。
            KazumaBullet bullet = arena.SpawnBullet(anchor + Vector3.right * 3f, Vector3.forward, 1);
            // 停止開始時の敵弾位置。
            Vector3 bulletPosition = bullet.transform.position;
            // 停止開始時のキュー位置。
            Vector3 cuePosition = ball.transform.position;
            // システム全体の時間倍率。UIや音声の設定が変更されないことを確認する。
            float timeScale = Time.timeScale;
            arena.AdvanceFrame(1f, 0.05f);
            Check(arena.IsHitStopped && bullet.transform.position == bulletPosition && ball.transform.position == cuePosition
                && player.transform.position == anchor && Vector3.Distance(camera.transform.position, cameraPosition) < 0.0001f,
                "Stopped gameplay does not move and camera offset is removed before reading input");
            arena.AdvanceCameraShake(0.05f);
            Check(arena.IsCameraShaking && camera.transform.position != cameraPosition, "Camera shake animates while gameplay is stopped");
            arena.AdvanceHitStop(0.099f);
            Check(arena.IsHitStopped, "Weak-point hit stop lasts through 0.149 seconds");
            arena.AdvanceHitStop(0.002f);
            arena.AdvanceCameraShake(0.101f);
            Check(!arena.IsHitStopped && !arena.IsCameraShaking && ball.State == KazumaWaterBalloon.MotionState.Recovering
                && Time.timeScale == timeScale && Vector3.Distance(camera.transform.position, cameraPosition) < 0.0001f,
                "Impact finishes at 0.15 seconds and restores camera without changing global time scale");
            arena.AdvanceFrame(0.01f, 0.01f);
            Check(bullet.transform.position.z > bulletPosition.z, "Enemy bullets resume after hit stop");

            arena.ResetRound();
            HitWeakPoint(arena, ball, weakPoint.position);
            arena.ResetRound();
            Check(!arena.IsHitStopped && !arena.IsCameraShaking && Vector3.Distance(camera.transform.position, cameraPosition) < 0.0001f,
                "Retry clears hit stop, deferred cue consumption and camera offset");
            HitWeakPoint(arena, ball, weakPoint.position);
            arena.enabled = true;
            arena.enabled = false;
            Check(!arena.IsHitStopped && !arena.IsCameraShaking && Vector3.Distance(camera.transform.position, cameraPosition) < 0.0001f,
                "Disabling gameplay clears impact effects without camera drift");
            arena.ResetRound();

            player.FeedPointer(true, new Vector2(300, 300), -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            player.FeedPointer(true, new Vector2(310, 300), -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            // 停止前のプレイヤー位置。停止中のドラッグが移動として加算されないことを確認する。
            Vector3 frozenPosition = player.transform.position;
            player.FeedPointer(true, new Vector2(700, 600), -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f, true);
            Check(player.transform.position == frozenPosition && player.Velocity == Vector3.zero, "Pointer tracking does not move the frozen player");
            player.FeedPointer(true, new Vector2(700, 600), -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position == frozenPosition, "Resuming input does not jump by the frozen drag distance");
            player.FeedPointer(false, Vector2.zero, -1, camera, KazumaPrototypeArena.MovementBounds, 0.016f, true);
            Check(player.TryConsumeBufferedRelease(out _) && !player.TryConsumeBufferedRelease(out _), "A release during hit stop is retained exactly once");
            arena.ResetRound();

            // 演出を無効化した場合にも、ダメージと弾消しが正常に成立することを確認する。
            var settings = new SerializedObject(arena);
            // 変更後に戻すヒットストップ時間。
            float stopDuration = settings.FindProperty("weakPointHitStop").floatValue;
            // 変更後に戻す弾消しの停止時間。輪の表示設定とは独立している。
            float eraseStopDuration = settings.FindProperty("bulletHitStop").floatValue;
            // 変更後に戻すカメラの揺れ幅。
            float shakeStrength = settings.FindProperty("cameraShakeStrength").floatValue;
            // 変更後に戻す弾消しの輪の表示時間。
            float ringDuration = settings.FindProperty("bulletEraseDuration").floatValue;
            try
            {
                settings.FindProperty("weakPointHitStop").floatValue = 0f;
                settings.FindProperty("cameraShakeStrength").floatValue = 0f;
                settings.FindProperty("bulletEraseDuration").floatValue = 0f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                HitWeakPoint(arena, ball, weakPoint.position);
                Check(!arena.IsHitStopped && !arena.IsCameraShaking && ball.State == KazumaWaterBalloon.MotionState.Recovering,
                    "Zero stop duration and shake strength disable impact effects without delaying cue consumption");
                arena.ResetRound();
                ball.Simulate(anchor, Vector3.zero, true, 0f);
                arena.SpawnBullet(ball.transform.position, Vector3.zero, 1);
                arena.TickBullets(anchor, anchor, 0f);
                Check(arena.ActiveBulletCount == 0 && EraseRings(arena).Length == 0, "Zero ring duration disables only the erase visual");
                Check(arena.IsHitStopped, "Disabling the erase ring preserves its short hit stop");
                settings.FindProperty("bulletHitStop").floatValue = 0f;
                settings.FindProperty("bulletEraseDuration").floatValue = ringDuration;
                settings.ApplyModifiedPropertiesWithoutUndo();
                arena.ResetRound();
                ball.Simulate(anchor, Vector3.zero, true, 0f);
                arena.SpawnBullet(ball.transform.position, Vector3.zero, 1);
                arena.TickBullets(anchor, anchor, 0f);
                Check(arena.ActiveBulletCount == 0 && !arena.IsHitStopped && ball.CanHit && EraseRings(arena).Length == 1,
                    "Zero erase stop duration preserves cue and ring while disabling only hit stop");
            }
            finally
            {
                settings.FindProperty("weakPointHitStop").floatValue = stopDuration;
                settings.FindProperty("bulletHitStop").floatValue = eraseStopDuration;
                settings.FindProperty("cameraShakeStrength").floatValue = shakeStrength;
                settings.FindProperty("bulletEraseDuration").floatValue = ringDuration;
                settings.ApplyModifiedPropertiesWithoutUndo();
                arena.ResetRound();
            }
        }

        // 実際の弱点位置へキューを配置して投擲状態にし、通常の命中処理を呼ぶ。
        private static void HitWeakPoint(KazumaPrototypeArena arena, KazumaWaterBalloon ball, Vector3 position)
        {
            ball.ResetBalloon(position + Vector3.forward * 1.35f);
            ball.Simulate(position + Vector3.forward * 1.35f, Vector3.zero, true, 0f);
            ball.Launch(Vector3.forward * 8f);
            arena.CheckBossHit();
        }

        // 紐やパリィのガイドを除いた、表示中の弾消しエフェクトのみを取得する。
        private static LineRenderer[] EraseRings(KazumaPrototypeArena arena)
        {
            return Array.FindAll(arena.GetComponentsInChildren<LineRenderer>(), line => line.name == "Bullet erase outer ring");
        }

        // directionが1なら左回り、-1なら右回り。実際の累積回転処理でLv.1→2→3を確認する。
        private static void CheckRotationDirection(KazumaWaterBalloon ball, Vector3 anchor, int direction)
        {
            ball.ResetBalloon(anchor);
            // ログで回転方向を識別するための表示名。
            string label = direction > 0 ? "Counterclockwise" : "Clockwise";
            // iは2度ずつ回す更新回数。1200回で6周以上となりLv.3まで強化できる。
            for (int i = 1; i <= 1200; i++)
            {
                // 初期位置の-90度から指定方向へ進めた、キューの角度。
                float angle = (-90f + direction * i * 2f) * Mathf.Deg2Rad;
                ball.transform.position = anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.35f;
                // 更新前の強化量。角度が±180度をまたいでも強化量が減らないか確認する。
                float previousCharge = ball.ChargedRevolutions;
                ball.Simulate(anchor, Vector3.right, true, 0f);
                if (ball.ChargedRevolutions + 0.0001f < previousCharge) throw new InvalidOperationException(label + " charge decreased");
                if (i == 480) Check(ball.Power == 1, label + " stays at tier one before three revolutions");
                if (i == 600) Check(ball.Power == 2, label + " reaches tier two after three revolutions");
            }
            Check(ball.Power == 3, label + " reaches tier three after six revolutions");
        }
        // 判定に必要な最小限の実コンポーネントを組み立てる。
        private static void Run()
        {
            // 親を共通化して、判定ガイドとエフェクトも検証対象に含める。
            var root = new GameObject("Parry validation arena");
            // 残機処理を含むゲーム管理コンポーネント。
            var arena = root.AddComponent<KazumaPrototypeArena>();
            arena.enabled = false;
            // 入力とキャラごとのパラメーターを提供するプレイヤー。
            var player = new GameObject("Player").AddComponent<KazumaDragPlayer>();
            player.transform.SetParent(root.transform);
            Set(player, "body", Body(player.transform));
            // キューのレベルがパリィ結果へ混ざらないことを確認するための実体。
            var ball = new GameObject("Cue").AddComponent<KazumaWaterBalloon>();
            ball.transform.SetParent(root.transform);
            Set(ball, "body", Body(ball.transform));
            Set(ball, "tether", ball.gameObject.AddComponent<LineRenderer>());
            // 発射時に複製する、表示Rendererを持つ敵弾の原型。
            var bullet = new GameObject("Bullet prototype").AddComponent<KazumaBullet>();
            Set(bullet, "body", Body(bullet.transform));
            Set(arena, "player", player);
            Set(arena, "balloon", ball);
            Set(arena, "bulletPrefab", bullet);
            Set(arena, "boss", new GameObject("Boss").transform);
            Set(arena, "weakPoint", new GameObject("Weak point").transform);
            Set(arena, "stockObjects", new GameObject[3]);
            Set(arena, "effectMaterial", new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            RunPlayerParryChecks(arena, player, ball);
            Check((int)typeof(KazumaPrototypeArena).GetField("currentStock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena) == 3,
                "Parries preserve the added player stock system");
        }
        // 表示色を確認できる球のRendererを、指定した親の子として作る。
        private static Renderer Body(Transform parent)
        {
            // 見た目専用の球。接触処理はArenaが独自に計算する。
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(parent, false);
            return sphere.GetComponent<Renderer>();
        }
        // 検証用の参照と受付状態を設定する。製品側に検証専用APIを追加しない。
        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
        // conditionが成立しない場合は理由付きで検証を停止する。
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + message);
            passed++;
            Debug.Log("PASS: " + message);
        }
        // プレイヤー仕様書の「敵弾Lv.1・2は接触弾のみ、Lv.3は全消去」を検証する。
        private static void RunPlayerParryChecks(KazumaPrototypeArena arena, KazumaDragPlayer player, KazumaWaterBalloon ball)
        {
            arena.ResetRound();
            // プレイヤーを中心とする外円へ弾を配置する際の基準位置。
            Vector3 anchor = player.transform.position;
            // 敵弾にもキューと同じレベル色が反映されることを確認するための期待色。
            Color[] colors = { new Color(0.1f, 0.35f, 1f), new Color(1f, 0.5f, 0f), new Color(0.65f, 0f, 1f) };
            // levelはパリィする敵弾の強さ。キューはLv.1のままで各レベルの結果を確認する。
            for (int level = 1; level <= 3; level++)
            {
                arena.ResetRound();
                // 本体には触れず、外円の端にだけ重なる弾の半径。
                float bulletRadius = level == 3 ? 0.25f : 0.17f;
                // パリィの対象となる、外円の端にある敵弾。
                KazumaBullet touching = arena.SpawnBullet(anchor + Vector3.right * (arena.ParryRadius + bulletRadius - 0.01f), Vector3.zero, level);
                // 全消去と接触弾のみの消去を区別する、外円から離れた弾。
                KazumaBullet distant = arena.SpawnBullet(anchor + Vector3.right * 3f, Vector3.zero, 1);
                // 発射時にRendererへ渡された敵弾の表示色。
                var properties = new MaterialPropertyBlock();
                touching.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(properties);
                Check(properties.GetColor("_BaseColor") == colors[level - 1], $"Enemy tier {level} uses the same color as the cue");
                arena.BeginReleaseProtection();
                arena.TickBullets(anchor, anchor, 0f);
                Check(arena.ParryCount == 1 && !touching.gameObject.activeSelf && ball.Power == 1
                    && arena.ActiveBulletCount == (level == 3 ? 0 : 1) && distant.gameObject.activeSelf == (level != 3),
                    $"Enemy tier {level} determines local or global parry regardless of cue level");
                // 成功演出がプレイヤーの外円から始まることも確認する。
                LineRenderer pulse = Array.Find(arena.GetComponentsInChildren<LineRenderer>(), line => line.name == "Parry outer ring");
                Check(pulse != null && pulse.transform.position == anchor && Mathf.Abs(pulse.GetPosition(0).magnitude - arena.ParryRadius) < 0.001f,
                    $"Tier {level} parry emits an effect on the player outer circumference");
            }

            arena.ResetRound();
            arena.BeginReleaseProtection();
            arena.SpawnBullet(anchor + Vector3.right * 0.7f, Vector3.zero, 1);
            arena.SpawnBullet(anchor + Vector3.left * 0.7f, Vector3.zero, 2);
            arena.SpawnBullet(anchor + Vector3.right * 3f, Vector3.zero, 3);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ActiveBulletCount == 1 && arena.ParryCount == 1,
                "Simultaneous low-tier contacts clear together while a distant tier-three bullet remains");
            arena.SpawnBullet(anchor + Vector3.right * 0.7f, Vector3.zero, 3);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ActiveBulletCount == 0 && arena.ParryCount == 2,
                "Low-tier parry keeps the remaining window available for a subsequent tier-three clear");

            arena.ResetRound();
            arena.BeginReleaseProtection();
            arena.SpawnBullet(anchor + Vector3.right * 0.7f, Vector3.zero, 1);
            arena.SpawnBullet(anchor + Vector3.left * 0.7f, Vector3.zero, 3);
            arena.SpawnBullet(anchor + Vector3.right * 3f, Vector3.zero, 2);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ParryCount == 1 && arena.ActiveBulletCount == 0,
                "Simultaneous mixed contacts prioritize one global tier-three parry");

            arena.ResetRound();
            arena.BeginReleaseProtection();
            arena.SpawnBullet(anchor + new Vector3(-2f, 0f, 0.65f), Vector3.right * 400f, 2);
            arena.TickBullets(anchor, anchor, 0.01f);
            Check(arena.ParryCount == 1 && arena.ActiveBulletCount == 0,
                "Fast bullets crossing only the outer ring cannot tunnel through the parry");

            arena.ResetRound();
            arena.BeginReleaseProtection();
            // 受付が終了した状態でも、外円表示だけで弾が消えないことを確認する。
            Set(arena, "parryRemaining", 0f);
            arena.SpawnBullet(anchor + Vector3.right * 0.7f, Vector3.zero, 2);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ParryCount == 0 && arena.ActiveBulletCount == 1,
                "The outer ring no longer erases bullets after the release window expires");

            try
            {
                arena.ResetRound();
                player.Parameters.SetPassiveMultipliers(parryRange: 2f);
                arena.BeginReleaseProtection();
                // キャラ補正が反映された、実際に表示中の判定範囲ガイド。
                LineRenderer guide = Array.Find(arena.GetComponentsInChildren<LineRenderer>(), line => line.name == "Parry range guide");
                Check(guide != null && guide.enabled && guide.transform.position == anchor
                    && Mathf.Abs(guide.GetPosition(0).magnitude - arena.ParryRadius) < 0.001f,
                    "The visible player ring matches the character's modified parry radius");
            }
            finally
            {
                player.Parameters.ResetPassiveMultipliers();
                arena.ResetRound();
            }
        }

    }
}
