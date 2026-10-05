using UnityEngine;

namespace KazumaPrototype
{
    public sealed class KazumaBullet : MonoBehaviour
    {
        [SerializeField] private Renderer body;
        public int Power { get; private set; }
        public float Radius => Power == 3 ? 0.25f : 0.17f;
        public Vector3 Velocity { get; private set; }
        public Vector3 PreviousPosition { get; private set; }
        public void Initialize(Vector3 position, Vector3 velocity, int power)
        {
            transform.position = PreviousPosition = position;
            Velocity = velocity;
            Power = Mathf.Clamp(power, 1, 3);
            transform.localScale = Vector3.one * Radius * 2f;
            var properties = new MaterialPropertyBlock();
            Color color = Power == 1 ? new Color(0.25f, 1f, 0.3f) : Power == 2 ? new Color(1f, 0.8f, 0.1f) : new Color(1f, 0.25f, 0.85f);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            body.SetPropertyBlock(properties);
        }
        public void Simulate(float dt)
        {
            PreviousPosition = transform.position;
            transform.position += Velocity * dt;
        }
    }
}
