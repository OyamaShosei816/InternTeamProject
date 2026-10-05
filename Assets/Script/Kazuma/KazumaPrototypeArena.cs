using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KazumaPrototype
{
    public sealed class KazumaPrototypeArena : MonoBehaviour
    {
        public enum RoundState { Playing, Cleared, Failed }
        [SerializeField] private Camera gameCamera;
        [SerializeField] private KazumaDragPlayer player;
        [SerializeField] private KazumaWaterBalloon balloon;
        [SerializeField] private KazumaBullet bulletPrefab;
        [SerializeField] private Transform boss;
        [SerializeField] private Transform weakPoint;
        [SerializeField] private Material effectMaterial;
        [Header("Prototype tuning")]
        [SerializeField] private float bossMaxHealth = 150f;
        [SerializeField] private float shotInterval = 1.5f;
        [SerializeField] private float releaseInvulnerability = 0.22f;
        [SerializeField] private float parryWindow = 0.12f;
        public RoundState State { get; private set; }
        public float BossHealth { get; private set; }
        public int ParryCount { get; private set; }
        public int ActiveBulletCount => bullets.Count;
        public static Rect MovementBounds => Rect.MinMaxRect(-4.1f, -7.5f, 4.1f, 3.7f);
        private readonly List<KazumaBullet> bullets = new List<KazumaBullet>();
        private readonly List<Pulse> pulses = new List<Pulse>();
        private float shotTimer, invincible, parryRemaining, endTimer;
        private int wave;
        private Vector3 previousPlayerPosition;
        private sealed class Pulse { public LineRenderer line; public float age; public Color color; public float radius; }

        private void Start()
        {
            if (Application.platform == RuntimePlatform.Android) Screen.orientation = ScreenOrientation.Portrait;
            ResetRound();
        }

        public void ResetRound()
        {
            ClearBullets();
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
            balloon.ResetBalloon(player.transform.position);
        }

        private void Update()
        {
            gameCamera.orthographicSize = Mathf.Max(9f, 5f / Mathf.Max(gameCamera.aspect, 0.1f));
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            UpdatePulses(dt);
            player.ReadInput(gameCamera, MovementBounds, dt);
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetRound();
                return;
            }
            if (State != RoundState.Playing)
            {
                endTimer += dt;
                // A new tap/press restarts, with no menu or HUD.
                if (endTimer > 0.75f && player.PressedThisFrame) ResetRound();
                else player.transform.position = previousPlayerPosition;
                return;
            }
            if (player.ReleasedThisFrame && balloon.Launch(player.FlickVelocity))
                BeginReleaseProtection();
            Vector3 currentPlayerPosition = player.transform.position;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
            float step = dt / steps;
            for (int i = 0; i < steps && State == RoundState.Playing; i++)
            {
                Vector3 from = Vector3.Lerp(previousPlayerPosition, currentPlayerPosition, i / (float)steps);
                Vector3 to = Vector3.Lerp(previousPlayerPosition, currentPlayerPosition, (i + 1f) / steps);
                balloon.Simulate(to, player.Velocity, player.IsHeld, step);
                TickBullets(from, to, step);
                if (State != RoundState.Playing) break;
                CheckBossHit();
                invincible = Mathf.Max(0f, invincible - step);
                parryRemaining = Mathf.Max(0f, parryRemaining - step);
            }
            previousPlayerPosition = currentPlayerPosition;
            if (State != RoundState.Playing) return;
            player.SetColor(invincible > 0f ? Color.white : new Color(0.12f, 0.8f, 1f));
            shotTimer -= dt;
            if (shotTimer <= 0f)
            {
                FireWave();
                shotTimer = shotInterval;
            }
        }

        public void BeginReleaseProtection()
        {
            invincible = releaseInvulnerability;
            parryRemaining = parryWindow;
            EmitPulse(player.transform.position, Color.white, 0.8f);
        }

        private void FireWave()
        {
            int power = 1 + wave++ % 3;
            Vector3 origin = weakPoint.position + Vector3.back * 0.5f;
            Vector3 direction = (player.transform.position - origin).normalized;
            int count = power == 3 ? 3 : 5;
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = Quaternion.AngleAxis((i - (count - 1) * 0.5f) * 14f, Vector3.up) * direction * (2.7f + power * 0.35f);
                SpawnBullet(origin, velocity, power);
            }
        }

        public KazumaBullet SpawnBullet(Vector3 position, Vector3 velocity, int power)
        {
            if (bullets.Count >= 96) return null;
            var bullet = Instantiate(bulletPrefab, transform);
            bullet.Initialize(position, velocity, power);
            bullets.Add(bullet);
            return bullet;
        }

        public void TickBullets(Vector3 playerFrom, Vector3 playerTo, float dt)
        {
            // Resolve a release-time parry before any normal collision in this step.
            // Only tier 3 bullets are the strong attacks described in the design.
            for (int i = 0; i < bullets.Count; i++) bullets[i].Simulate(dt);
            if (parryRemaining > 0f)
            {
                foreach (var bullet in bullets)
                {
                    if (bullet.Power == 3 && Sweep(bullet.PreviousPosition - playerFrom,
                        bullet.transform.position - playerTo, bullet.Radius + KazumaDragPlayer.Radius, out _))
                    {
                        ParryCount++;
                        ClearBullets();
                        parryRemaining = 0f;
                        EmitPulse(playerTo, Color.cyan, 5f);
                        return;
                    }
                }
            }
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var bullet = bullets[i];
                bool hitsPlayer = Sweep(bullet.PreviousPosition - playerFrom,
                    bullet.transform.position - playerTo, bullet.Radius + KazumaDragPlayer.Radius, out float playerTime);
                float ballTime = float.PositiveInfinity;
                bool hitsBall = balloon.CanHit && balloon.Power >= bullet.Power && Sweep(
                    bullet.PreviousPosition - balloon.PreviousPosition,
                    bullet.transform.position - balloon.transform.position,
                    bullet.Radius + KazumaWaterBalloon.Radius, out ballTime);
                // The string has no collider. Only the actual ball erases projectiles.
                if (hitsBall && (!hitsPlayer || ballTime <= playerTime))
                {
                    RemoveBullet(i);
                    continue;
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

        public void CheckBossHit()
        {
            if (balloon.State != KazumaWaterBalloon.MotionState.Flying) return;
            bool weakHit = Sweep(balloon.PreviousPosition - weakPoint.position,
                balloon.transform.position - weakPoint.position, KazumaWaterBalloon.Radius + 0.55f, out float weakTime);
            bool bodyHit = Sweep(balloon.PreviousPosition - boss.position,
                balloon.transform.position - boss.position, KazumaWaterBalloon.Radius + 1.15f, out float bodyTime);
            if (weakHit && (!bodyHit || weakTime <= bodyTime))
            {
                BossHealth = Mathf.Max(0f, BossHealth - KazumaWaterBalloon.DamageAtSpeed(balloon.Velocity.magnitude));
                EmitPulse(weakPoint.position, Color.yellow, 1.6f);
                balloon.Consume();
                Debug.Log($"Kazuma: weak point hit. Boss HP {BossHealth:0}/{bossMaxHealth:0}", this);
                if (BossHealth <= 0f) EndRound(true);
            }
            else if (bodyHit)
            {
                EmitPulse(balloon.transform.position, Color.gray, 0.7f);
                balloon.Consume();
            }
        }

        // Continuous moving-sphere collision: fast flicks cannot tunnel through bullets/targets.
        public static bool Sweep(Vector3 from, Vector3 to, float radius, out float time)
        {
            time = 0f;
            float c = from.sqrMagnitude - radius * radius;
            if (c <= 0f) return true;
            Vector3 delta = to - from;
            float a = delta.sqrMagnitude;
            if (a < 0.000001f) return false;
            float b = Vector3.Dot(from, delta);
            float discriminant = b * b - a * c;
            if (discriminant < 0f) return false;
            time = (-b - Mathf.Sqrt(discriminant)) / a;
            return time >= 0f && time <= 1f;
        }

        private void EndRound(bool cleared)
        {
            State = cleared ? RoundState.Cleared : RoundState.Failed;
            player.SetColor(cleared ? Color.green : Color.red);
            EmitPulse(cleared ? boss.position : player.transform.position, cleared ? Color.green : Color.red, 3f);
            if (cleared) boss.gameObject.SetActive(false);
            ClearBullets();
            Debug.Log(cleared ? "Kazuma: CLEAR. Tap/click to restart." : "Kazuma: HIT. Tap/click to retry.", this);
        }

        private void RemoveBullet(int index)
        {
            bullets[index].gameObject.SetActive(false);
            Destroy(bullets[index].gameObject);
            bullets.RemoveAt(index);
        }
        private void ClearBullets()
        {
            for (int i = bullets.Count - 1; i >= 0; i--) RemoveBullet(i);
        }

        private void EmitPulse(Vector3 position, Color color, float radius)
        {
            if (pulses.Count >= 8) return;
            var go = new GameObject("Gameplay pulse");
            go.transform.SetParent(transform);
            go.transform.position = position;
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = effectMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.widthMultiplier = 0.07f;
            line.startColor = line.endColor = color;
            for (int i = 0; i < 40; i++)
            {
                float angle = i * Mathf.PI * 2f / 40f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.2f);
            }
            pulses.Add(new Pulse { line = line, color = color, radius = radius });
        }
        private void UpdatePulses(float dt)
        {
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                var pulse = pulses[i];
                pulse.age += dt;
                float t = pulse.age / 0.45f;
                if (t >= 1f)
                {
                    Destroy(pulse.line.gameObject);
                    pulses.RemoveAt(i);
                    continue;
                }
                Color color = pulse.color * (1f - t);
                color.a = 1f;
                pulse.line.startColor = pulse.line.endColor = color;
                for (int j = 0; j < 40; j++)
                {
                    float angle = j * Mathf.PI * 2f / 40;
                    pulse.line.SetPosition(j, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Lerp(0.2f, pulse.radius, t));
                }
            }
        }
    }
}
