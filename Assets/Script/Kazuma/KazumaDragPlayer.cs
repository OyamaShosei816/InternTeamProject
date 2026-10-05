using UnityEngine;
using UnityEngine.InputSystem;

namespace KazumaPrototype
{
    // Relative drag works anywhere on the screen, without teleporting under the finger.
    public sealed class KazumaDragPlayer : MonoBehaviour
    {
        [SerializeField] private float dragSensitivity = 1f;
        [SerializeField] private float maximumFlickSpeed = 18f;
        [SerializeField] private Renderer body;
        public const float Radius = 0.34f;
        public bool IsHeld { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 FlickVelocity { get; private set; }
        public bool PressedThisFrame { get; private set; }
        public bool ReleasedThisFrame { get; private set; }
        private Vector2 previousPointer;
        private int pointerId = -1;
        private bool ignoreUntilRelease;
        private float lastMovementTime;
        private MaterialPropertyBlock properties;

        // falseになるとPlayerを操作できなくする。
        // GameOver時などに使用する。
        public bool CanMove { get; private set; } = true; // Playerの操作状態

        public void ReadInput(Camera camera, Rect bounds, float dt)
        {
            // 操作禁止状態なら入力を受け付けない
            if (!CanMove)
            {
                Velocity = Vector3.zero;
                FlickVelocity = Vector3.zero;
                IsHeld = false;
                PressedThisFrame = false;
                ReleasedThisFrame = false;
                return;
            }

            bool held = false;
            Vector2 position = default;
            int id = -1;
            var touchscreen = Touchscreen.current;
            // Lock to the first finger. Additional touches must never cause a jump.
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed || (IsHeld && pointerId >= 0 && touch.touchId.ReadValue() != pointerId))
                        continue;
                    held = true;
                    position = touch.position.ReadValue();
                    id = touch.touchId.ReadValue();
                    break;
                }
            }
            if (!held && pointerId < 0 && Mouse.current != null)
            {
                held = Mouse.current.leftButton.isPressed;
                position = Mouse.current.position.ReadValue();
            }
            FeedPointer(held, position, id, camera, bounds, dt);
        }

        // ============================================================
        // Playerの操作可否を変更
        // ============================================================
        public void SetCanMove(bool canMove)
        {
            CanMove = canMove;

            // 操作禁止になった瞬間に移動情報もリセットする
            if (!CanMove)
            {
                IsHeld = false;
                PressedThisFrame = false;
                ReleasedThisFrame = false;
                Velocity = Vector3.zero;
                FlickVelocity = Vector3.zero;
                pointerId = -1;
            }
        }
        public void FeedPointer(bool held, Vector2 position, int id, Camera camera, Rect bounds, float dt)
        {
            PressedThisFrame = ReleasedThisFrame = false;
            Velocity = Vector3.zero;
            if (ignoreUntilRelease)
            {
                if (!held) ignoreUntilRelease = false;
                return;
            }
            if (!held)
            {
                ReleasedThisFrame = IsHeld;
                IsHeld = false;
                pointerId = -1;
                if (Time.unscaledTime - lastMovementTime > 0.1f) FlickVelocity = Vector3.zero;
                return;
            }
            if (!IsHeld)
            {
                IsHeld = true;
                PressedThisFrame = true;
                previousPointer = position;
                pointerId = id;
                FlickVelocity = Vector3.zero;
                return;
            }
            var plane = new Plane(Vector3.up, transform.position);
            Ray before = camera.ScreenPointToRay(previousPointer);
            Ray after = camera.ScreenPointToRay(position);
            previousPointer = position;
            if (!plane.Raycast(before, out float a) || !plane.Raycast(after, out float b)) return;
            Vector3 oldPosition = transform.position;
            Vector3 target = oldPosition + (after.GetPoint(b) - before.GetPoint(a)) * dragSensitivity;
            target.x = Mathf.Clamp(target.x, bounds.xMin, bounds.xMax);
            target.z = Mathf.Clamp(target.z, bounds.yMin, bounds.yMax);
            transform.position = target;
            Velocity = Vector3.ClampMagnitude((target - oldPosition) / Mathf.Max(dt, 0.001f), maximumFlickSpeed);
            if (Velocity.sqrMagnitude > 0.01f)
            {
                FlickVelocity = Velocity;
                lastMovementTime = Time.unscaledTime;
            }
            else if (Time.unscaledTime - lastMovementTime > 0.1f) FlickVelocity = Vector3.zero;
        }

        public void ResetPlayer(Vector3 position, bool waitForRelease = false)
        {
            transform.position = position;
            IsHeld = PressedThisFrame = ReleasedThisFrame = false;
            Velocity = FlickVelocity = Vector3.zero;
            pointerId = -1;
            ignoreUntilRelease = waitForRelease;
            SetColor(new Color(0.12f, 0.8f, 1f));
        }

        public void SetColor(Color color)
        {
            if (body == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            body.SetPropertyBlock(properties);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ResetPlayer(transform.position, true);
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetPlayer(transform.position, true);
        }
    }
}
