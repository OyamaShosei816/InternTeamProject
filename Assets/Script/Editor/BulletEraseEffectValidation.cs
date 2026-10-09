using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Prototype.Editor
{
    // 検証用コピーで、実際の弾消しと各Prefabの粒子速度を確認する。
    public static class BulletEraseEffectValidation
    {
        // Play Modeの再読み込み後も検証を継続するためのキー。
        private const string Pending = "Prototype.BulletEraseValidation";
        // ゲーム初期化後にテストを始める時刻。
        private static double nextCheck;

        // 設定を保存してから、実際のPlay Modeでテストを開始する。
        public static void BuildAndValidateBatch()
        {
            BulletEraseEffectSetup.Build();
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        // スクリプト再読み込み後、検証用更新処理を再登録する。
        [InitializeOnLoadMethod]
        private static void Register() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

        // 初期化を待ち、テスト結果を保存して検証用Unityを終了する。
        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (nextCheck == 0) nextCheck = EditorApplication.timeSinceStartup + 3;
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            SessionState.SetBool(Pending, false);
            try { RunChecks(); Finish("PASS: Lv1/2/3 collision selection, four spark directions, stationary fallback, Effect layer, URP materials, reset cleanup.", 0); }
            catch (Exception error) { Finish("FAIL: " + error, 1); }
        }

        // レベル別の実衝突と、上下左右に向けた粒子の速度を検証する。
        private static void RunChecks()
        {
            // Effectシーンに登録した管理元と、衝突テスト対象。
            var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeArena>();
            // 新しい弾消し管理コンポーネント。
            var effects = arena.GetComponent<BulletEraseEffectPlayer>();
            // テスト中に操作するキュー。
            var ball = UnityEngine.Object.FindFirstObjectByType<WaterBalloon>();
            // 敵弾をプレイヤーの当たり判定から離して配置する基準。
            var player = UnityEngine.Object.FindFirstObjectByType<DragPlayer>();
            Check(effects != null, "Scene registration");
            // Lv3キューで各レベルの敵弾を消し、敵弾のレベルで選択されることを確認する。
            for (int level = 1; level <= 3; level++)
            {
                arena.ResetRound();
                ball.Simulate(player.transform.position, Vector3.zero, true, 0f);
                typeof(WaterBalloon).GetProperty("Power").SetValue(ball, 3);
                ball.Launch(Vector3.right);
                arena.SpawnBullet(ball.transform.position, Vector3.zero, level);
                arena.TickBullets(player.transform.position, player.transform.position, 0f);
                Check(arena.ActiveBulletCount == 0, "Enemy bullet removed at level " + level);
                Check(FindEffect(level) != null, "Selected enemy bullet level " + level + " with cue level 3");
                arena.ResetRound();
                Check(FindEffect(level) == null, "Reset removes remaining particles");
                // ワールドのX/Z平面で四方向へ発射する。
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    Check(effects.TryPlay(level, new Vector3(0, 0.65f, 0), direction), "Prefab assigned");
                    // 今回再生したレベルのエフェクト。
                    GameObject root = FindEffect(level);
                    Check(root != null && Vector3.Dot(root.transform.forward, direction) > 0.99f, "Emission orientation");
                    // 実際に速度を持つRect/Cubeパーティクルの合計数。
                    int movingParticles = 0;
                    // 全システムの描画と、粒子の飛行方向を確認する。
                    foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        Check(system.gameObject.layer == LayerMask.NameToLayer("Effect"), "Effect layer");
                        // Emitter専用ノードは描画しないので、マテリアル検査から除外する。
                        var renderer = system.GetComponent<ParticleSystemRenderer>();
                        if (renderer.renderMode != ParticleSystemRenderMode.None)
                            Check(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name.StartsWith("Universal Render Pipeline/"), "URP particle material");
                        if (!system.name.Contains("Rect") && !system.name.Contains("Cube")) continue;
                        system.Simulate(0.05f, false, true, true);
                        // 放射された粒子のワールド速度を読み取るバッファ。
                        var particles = new ParticleSystem.Particle[system.main.maxParticles];
                        // 実際に発生した粒子数。
                        int count = system.GetParticles(particles);
                        // コーン内の各粒子が、指定方向と同じ半球に飛んでいることを確認する。
                        for (int i = 0; i < count; i++)
                        {
                            if (particles[i].velocity.sqrMagnitude < 0.01f) continue;
                            Check(Vector3.Dot(particles[i].velocity.normalized, direction) > 0.7f, "Spark velocity direction");
                            movingParticles++;
                        }
                    }
                    Check(movingParticles > 0, "Sparks emitted");
                    effects.Clear();
                }
            }
            Check(BulletEraseEffectPlayer.ResolveDirection(Vector3.zero, Vector3.zero, Vector3.left, Vector3.zero) == Vector3.right, "Stationary cue fallback");
            Check(BulletEraseEffectPlayer.ResolveDirection(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero) == Vector3.forward, "Zero vector fallback");
        }

        // 非表示のDestroy待ちオブジェクトを除き、今回のPrefabインスタンスだけを探す。
        private static GameObject FindEffect(int level)
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(root => root.activeSelf && root.name == $"Bullet_Dest_Lv{level}_Null 1(Clone)");
        }

        // テストが失敗した理由を、例外として結果ファイルへ伝える。
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

        // テスト結果を保存し、コピー側のUnityだけを終了する。
        private static void Finish(string result, int code)
        {
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/BulletEraseEffect-validation.txt", result);
            Debug.Log(result); EditorApplication.Exit(code);
        }
    }
}
