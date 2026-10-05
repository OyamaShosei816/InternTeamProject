using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KazumaPrototype.Editor
{
    // Runs against the saved scene in real Play Mode; callable in an isolated batch editor.
    public static class KazumaPrototypeValidation
    {
        private static int waitFrames;
        private static int passed;
        private const string Pending = "Kazuma.Validation.Pending";

        public static void BuildAndValidate()
        {
            KazumaPrototypeBuilder.Build();
            Validate();
        }

        [MenuItem("Tools/Kazuma/Validate gameplay in Play Mode")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validating.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(KazumaPrototypeBuilder.ScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update += WaitForPlay;
        }

        private static void WaitForPlay()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
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
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorApplication.isPlaying = false;
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            passed++;
            Debug.Log("PASS: " + description);
        }

        private static void RunChecks()
        {
            var arena = UnityEngine.Object.FindFirstObjectByType<KazumaPrototypeArena>();
            var player = UnityEngine.Object.FindFirstObjectByType<KazumaDragPlayer>();
            var ball = UnityEngine.Object.FindFirstObjectByType<KazumaWaterBalloon>();
            Camera camera = Camera.main;
            arena.enabled = false;
            arena.ResetRound();
            Check(player != null && ball != null && camera.orthographic, "Saved scene references and top-down camera");
            Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length == 0, "No UI canvas");
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
            Vector3 touchStart = player.transform.position;
            player.FeedPointer(true, new Vector2(10, 10), 42, camera, KazumaPrototypeArena.MovementBounds, 0.016f);
            Check(player.transform.position.x < touchStart.x && player.transform.position.z < touchStart.z, "Touch uses the same drag mapping");

            Vector3 anchor = new Vector3(0f, 0.65f, -4f);
            ball.ResetBalloon(anchor);
            for (int i = 0; i < 2400; i++) ball.Simulate(anchor, Vector3.zero, true, 1f / 120f);
            Check(ball.Power == 1, "Holding still orbits without charging");
            ball.ResetBalloon(anchor);
            int lastPower = 1;
            for (int i = 0; i < 3000; i++)
            {
                float t = i / 120f;
                Vector3 movingAnchor = anchor + new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t)) * 0.4f;
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
            Vector3 orbitVelocity = ball.Velocity;
            Check(ball.Launch(Vector3.zero) && Vector3.Distance(ball.Velocity, orbitVelocity) < 0.001f, "Release retains world velocity");
            Vector3 launched = ball.transform.position;
            ball.Simulate(anchor, Vector3.zero, false, 0.01f);
            Check(Vector3.Distance(ball.transform.position, launched + orbitVelocity * 0.01f) < 0.001f, "Ball flies along its release vector");
            Check(!ball.Launch(Vector3.forward), "Cannot launch twice while in flight");
            ball.Consume();
            ball.Simulate(anchor, Vector3.zero, false, 1f);
            Check(ball.State == KazumaWaterBalloon.MotionState.Ready && ball.Power == 1, "Ball recovers and resets strength");
            ball.Simulate(anchor, Vector3.zero, true, 0.01f);
            Vector3 beforeFlick = ball.Velocity;
            ball.Launch(Vector3.forward * 10f);
            Check(ball.Velocity.z > beforeFlick.z + 7f, "Flick adds directional force");
            Check(KazumaWaterBalloon.DamageAtSpeed(20f) > KazumaWaterBalloon.DamageAtSpeed(5f), "Faster throws deal more damage");
            Check(KazumaPrototypeArena.Sweep(new Vector3(-10, 0, 0), new Vector3(10, 0, 0), 0.5f, out _), "Fast projectile sweep cannot tunnel");
            Check(!KazumaPrototypeArena.Sweep(new Vector3(-10, 0, 2), new Vector3(10, 0, 2), 0.5f, out _), "Sweep rejects a near miss");

            arena.ResetRound();
            anchor = player.transform.position;
            ball.Simulate(anchor, Vector3.zero, true, 0.01f);
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
            arena.SpawnBullet(anchor, Vector3.zero, 3);
            arena.SpawnBullet(anchor + Vector3.right * 2f, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.ParryCount == 1 && arena.ActiveBulletCount == 0 && arena.State == KazumaPrototypeArena.RoundState.Playing,
                "Timed release against a strong attack parries and clears every projectile");
            arena.ResetRound();
            arena.BeginReleaseProtection();
            arena.SpawnBullet(anchor, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.State == KazumaPrototypeArena.RoundState.Playing && arena.ParryCount == 0, "Release grants brief protection without parrying a weak attack");
            arena.ResetRound();
            arena.SpawnBullet(anchor, Vector3.zero, 1);
            arena.TickBullets(anchor, anchor, 0f);
            Check(arena.State == KazumaPrototypeArena.RoundState.Failed, "One unprotected hit fails the round");
            arena.ResetRound();
            Check(arena.State == KazumaPrototypeArena.RoundState.Playing && arena.ActiveBulletCount == 0 && arena.BossHealth == 150f,
                "Retry resets health, bullets and round state");

            float initialHealth = arena.BossHealth;
            ThrowAt(ball, arena, new Vector3(1.2f, 0.65f, 5.6f));
            Check(arena.BossHealth == initialHealth, "Boss armor takes no damage");
            for (int i = 0; i < 30 && arena.State == KazumaPrototypeArena.RoundState.Playing; i++)
                ThrowAt(ball, arena, new Vector3(0f, 0.65f, 4.45f));
            Check(arena.State == KazumaPrototypeArena.RoundState.Cleared && arena.BossHealth == 0f, "Weak-point throws deplete HP and clear the round");
            arena.ResetRound();
        }

        private static void ThrowAt(KazumaWaterBalloon ball, KazumaPrototypeArena arena, Vector3 target)
        {
            ball.ResetBalloon(target + Vector3.forward * 1.35f);
            ball.Simulate(target + Vector3.forward * 1.35f, Vector3.zero, true, 0.001f);
            ball.Launch(Vector3.forward * 18f);
            arena.CheckBossHit();
        }

        private static void CapturePreview()
        {
            var arena = UnityEngine.Object.FindFirstObjectByType<KazumaPrototypeArena>();
            var player = UnityEngine.Object.FindFirstObjectByType<KazumaDragPlayer>();
            var ball = UnityEngine.Object.FindFirstObjectByType<KazumaWaterBalloon>();
            for (int i = 0; i < 120; i++) ball.Simulate(player.transform.position, Vector3.zero, true, 1f / 120f);
            for (int row = 0; row < 3; row++)
                for (int j = -2; j <= 2; j++)
                    arena.SpawnBullet(new Vector3(j * 1.1f, 0.65f, 2.7f - row * 2f - Mathf.Abs(j) * 0.3f), Vector3.zero, row + 1);
            Camera camera = Camera.main;
            var texture = new RenderTexture(540, 960, 24);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            camera.targetTexture = texture;
            camera.aspect = 540f / 960f;
            camera.orthographicSize = 9f;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/KazumaPreview.png", image.EncodeToPNG());
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
