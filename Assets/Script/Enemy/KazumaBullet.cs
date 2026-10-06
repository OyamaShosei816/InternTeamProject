using UnityEngine;

namespace KazumaPrototype
{
    /// <summary>
    /// 敵弾1個の強さ・移動・見た目を管理する。
    /// 弾の移動更新と当たり判定はKazumaPrototypeArena側でまとめて行う。
    /// </summary>
    public sealed class KazumaBullet : MonoBehaviour
    {
        // ============================================================
        // Inspector設定
        // ============================================================
        [Header("敵弾の見た目")]
        [Tooltip("敵弾Prefabの表示用Rendererを設定します。弾の強さに応じて色が変化します。")]
        [SerializeField] private Renderer body;

        // ============================================================
        // 弾の状態
        // 弾の強さ。
        // Lv1～Lv3の3段階で管理する。
        // ============================================================
        public int Power { get; private set; }

        // 弾の当たり判定半径。
        // Lv3のみ通常弾より大きくする。
        public float Radius => Power == 3 ? 0.25f : 0.17f;

        // ワールド座標上での移動速度。
        public Vector3 Velocity { get; private set; }

        // 1フレーム前の弾の位置。
        // 高速移動時のすり抜け防止判定に使用する。
        public Vector3 PreviousPosition { get; private set; }


        // ============================================================
        // 初期化
        // 敵弾生成時の初期設定を行う。
        // 位置・速度・強さ・大きさ・色を設定する。
        // ============================================================
        public void Initialize(
            Vector3 position,
            Vector3 velocity,
            int power)
        {
            transform.position = position;
            PreviousPosition = position;

            Velocity = velocity;

            // 弾の強さはLv1～Lv3の範囲に制限する。
            Power = Mathf.Clamp(power, 1, 3);

            // 当たり判定半径に合わせて見た目の大きさも変更する。
            transform.localScale =
                Vector3.one * Radius * 2.0f;

            UpdateAppearance();
        }

        // ============================================================
        // 見た目
        // 弾の強さに応じて色を変更する。
        // Lv1：青
        // Lv2：黄
        // Lv3：紫
        // ============================================================
        private void UpdateAppearance()
        {
            if (body == null)
            {
                Debug.LogWarning(
                    $"{nameof(KazumaBullet)} : Rendererが設定されていません。",
                    this);

                return;
            }

            Color color = Power switch
            {
                1 => new Color(0.05f, 0.65f, 1.0f),
                2 => new Color(1.0f, 0.9f, 0.05f),
                3 => new Color(0.55f, 0.15f, 0.8f),
                _ => Color.white
            };

            // sharedMaterialを直接変更すると
            // 他の敵弾にも色変更が反映されるため、
            // MaterialPropertyBlockを使用する。
            MaterialPropertyBlock properties =
                new MaterialPropertyBlock();

            properties.SetColor(
                "_BaseColor",
                color);

            properties.SetColor(
                "_Color",
                color);

            body.SetPropertyBlock(properties);
        }

        // ============================================================
        // 移動
        // 指定された時間分だけ敵弾を移動させる。
        // 移動前の位置を保存してから移動することで、
        // 抜け防止判定を行えるようにする。
        // ============================================================
        public void Simulate(float deltaTime)
        {
            PreviousPosition = transform.position;

            transform.position +=
                Velocity * deltaTime;
        }
    }
}