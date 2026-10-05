using UnityEngine;

namespace KazumaPrototype
{
    public sealed class KazumaWaterBalloon : MonoBehaviour
    {
        public enum MotionState { Ready, Orbiting, Flying, Recovering }
        [Header("Elastic orbit")]
        [SerializeField] private float orbitRadius = 1.35f;
        [SerializeField] private float spring = 65f;
        [SerializeField] private float damping = 6f;
        [SerializeField] private float baseAngularSpeed = 3.8f;
        [SerializeField] private float maximumTetherLength = 2.25f;
        [SerializeField, Min(1)] private int revolutionsPerLevel = 3;
        [Header("Throw")]
        [SerializeField] private float flickInfluence = 0.8f;
        [SerializeField] private float maximumLaunchSpeed = 30f;
        [SerializeField] private float recoveryDelay = 0.65f;
        [SerializeField] private Renderer body;
        [SerializeField] private LineRenderer tether;
        public const float Radius = 0.4f;
        public MotionState State { get; private set; }
        public int Power { get; private set; } = 1;
        public float ChargedRevolutions => chargedRadians / (Mathf.PI * 2f);
        public Vector3 Velocity { get; private set; }
        public Vector3 PreviousPosition { get; private set; }
        public bool CanHit => State == MotionState.Orbiting || State == MotionState.Flying;
        private float phase, chargedRadians, remaining, previousAngle;
        private MaterialPropertyBlock properties;

        public void ResetBalloon(Vector3 anchor)
        {
            State = MotionState.Ready;
            Power = 1;
            phase = -Mathf.PI / 2f;
            chargedRadians = 0f;
            Velocity = Vector3.zero;
            transform.position = anchor + Vector3.back * orbitRadius;
            PreviousPosition = transform.position;
            previousAngle = -90f;
            body.enabled = true;
            UpdateAppearance(anchor);
        }

        public void Simulate(Vector3 anchor, Vector3 playerVelocity, bool held, float dt)
        {
            PreviousPosition = transform.position;
            if (State == MotionState.Recovering)
            {
                remaining -= dt;
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
                float movement = Mathf.Min(playerVelocity.magnitude, 12f);
                if (held) phase += (baseAngularSpeed + (Power - 1) * 1.4f + movement * 0.22f) * dt;
                Vector3 radial = new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase));
                Vector3 target = anchor + radial * orbitRadius;
                // A spring follows the moving anchor; the ball keeps its world-space inertia.
                Velocity += ((target - transform.position) * spring - Velocity * damping) * dt;
                transform.position += Velocity * dt;
                Vector3 offset = transform.position - anchor;
                if (offset.magnitude > maximumTetherLength)
                {
                    Vector3 normal = offset.normalized;
                    transform.position = anchor + normal * maximumTetherLength;
                    float outwardSpeed = Vector3.Dot(Velocity - playerVelocity, normal);
                    if (outwardSpeed > 0f) Velocity -= normal * outwardSpeed;
                }
                offset = transform.position - anchor;
                float angle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
                float turned = Mathf.DeltaAngle(previousAngle, angle) * Mathf.Deg2Rad;
                // Only actual forward orbit while moving charges the three strength tiers.
                if (held && movement > 0.15f && offset.sqrMagnitude > 0.25f)
                    chargedRadians = Mathf.Max((Power - 1) * Mathf.PI * 2f * revolutionsPerLevel, chargedRadians + turned);
                previousAngle = angle;
                Power = Mathf.Clamp(1 + Mathf.FloorToInt(chargedRadians / (Mathf.PI * 2f * revolutionsPerLevel)), 1, 3);
            }
            UpdateAppearance(anchor);
        }

        public bool Launch(Vector3 flick)
        {
            if (State != MotionState.Orbiting) return false;
            Vector3 direction = Velocity + flick * flickInfluence;
            if (direction.sqrMagnitude < 0.1f)
                direction = new Vector3(-Mathf.Sin(phase), 0f, Mathf.Cos(phase)) * baseAngularSpeed * orbitRadius;
            Velocity = Vector3.ClampMagnitude(direction, maximumLaunchSpeed);
            State = MotionState.Flying;
            remaining = 3f;
            tether.enabled = false;
            return true;
        }

        public void Consume()
        {
            State = MotionState.Recovering;
            remaining = recoveryDelay;
            Velocity = Vector3.zero;
            body.enabled = false;
            tether.enabled = false;
        }

        public static float DamageAtSpeed(float speed) => Mathf.Clamp(speed * 2f, 8f, 60f);

        private void UpdateAppearance(Vector3 anchor)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            Color color = Power == 1 ? new Color(0.25f, 1f, 0.3f) : Power == 2 ? new Color(1f, 0.8f, 0.1f) : new Color(1f, 0.25f, 0.85f);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            body.SetPropertyBlock(properties);
            // Squash/stretch is visual only: collision size stays predictable.
            float stretch = Mathf.Clamp(Velocity.magnitude * 0.01f, 0f, 0.2f);
            body.transform.localScale = new Vector3(0.8f - stretch * 0.4f, 0.8f - stretch * 0.4f, 0.8f + stretch);
            if (Velocity.sqrMagnitude > 0.05f) body.transform.rotation = Quaternion.LookRotation(Velocity, Vector3.up);
            tether.enabled = State == MotionState.Ready || State == MotionState.Orbiting;
            if (!tether.enabled) return;
            tether.positionCount = 9;
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                Vector3 point = Vector3.Lerp(anchor, transform.position, t);
                point += Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.2f);
                tether.SetPosition(i, point);
            }
        }
    }
}
