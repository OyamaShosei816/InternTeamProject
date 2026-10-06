using UnityEngine;

namespace Prototype
{
    // 敵弾1個の強さ・移動・見た目を管理する。
    // 更新と当たり判定は PrototypeArena がまとめて行うため、このクラスには Update を置かない。
    public sealed class Bullet : MonoBehaviour
    {
        // 弾の表示部分。Prefab作成時にBuilderから設定する。
        [Header("敵弾の見た目：表示用Renderer")]
        [Tooltip("敵弾Prefab内の球のRendererを設定します。強さに応じて色が変わります。")]
        [SerializeField] private Renderer body;
        // 強さは1～3。水風船が同じ強さ以上なら、この弾を消せる。
        public int Power { get; private set; }
        // 強さ3の弾だけ大きい。当たり判定には見た目とは別にこの半径を使う。
        public float Radius => Power == 3 ? 0.25f : 0.17f;
        // ワールド座標での速度（1秒当たりの移動量）。
        public Vector3 Velocity { get; private set; }
        // 移動前の位置。高速移動でも弾が相手をすり抜けないよう、移動区間全体の判定に使う。
        public Vector3 PreviousPosition { get; private set; }
        // 生成直後に呼ぶ初期設定。出現位置・速度・強さを設定し、強さに合わせて大きさと色を変える。
        public void Initialize(Vector3 position, Vector3 velocity, int power)
        {
            transform.position = PreviousPosition = position;
            Velocity = velocity;
            Power = Mathf.Clamp(power, 1, 3);
            transform.localScale = Vector3.one * Radius * 2f;
            // 共有マテリアル自体を変更せず、この弾だけの色を設定する。
            var properties = new MaterialPropertyBlock();
            // 強さ1は緑、2は黄、3は紫で表示するための色。
            Color color = Power == 1 ? new Color(0.25f, 1f, 0.3f) : Power == 2 ? new Color(1f, 0.8f, 0.1f) : new Color(1f, 0.25f, 0.85f);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            body.SetPropertyBlock(properties);
        }
        // dt は今回進める時間（秒）。位置を記録してから直線移動する。
        public void Simulate(float dt)
        {
            PreviousPosition = transform.position;
            transform.position += Velocity * dt;
        }
    }
}
