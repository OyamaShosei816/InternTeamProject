using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Prototype
{
	// 蜈･蜉帙∵ｰｴ鬚ｨ闊ｹ縺ｮ遘ｻ蜍輔∝ｼｾ縺ｮ陦晉ｪ√√・繧ｹ蛻､螳壹ｒ鬆・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
    // 繧ｲ繝ｼ繝騾ｲ陦後→繝偵ャ繝医せ繝医ャ繝励√し繧ｦ繝ｳ繝峨↑縺ｩ繧堤ｮ｡逅・☆繧九・
    public sealed class PrototypeArena : MonoBehaviour
    {
        // 繝励Ξ繧､荳ｭ繝ｻ繧ｯ繝ｪ繧｢繝ｻ螟ｱ謨励・3迥ｶ諷九らｵゆｺ・ｾ後・荳螳壽凾髢薙ｒ鄂ｮ縺・※謚ｼ縺礼峩縺吶→蜀埼幕縺ｧ縺阪ｋ縲・
        public enum RoundState { Playing, Cleared, Failed }
        private float roundElapsed;
        [Header("操作対象：プレイヤー")]
        [Tooltip("シーン内のプレイヤーのDragPlayerを設定します。")]
        [SerializeField] private DragPlayer player;

        // 表示と、タッチ座標の変換に使用するカメラ。
        [Header("画面：ゲーム用カメラ")]
        [Tooltip("ゲーム画面を映すCameraを設定します。")]
        [SerializeField] private Camera gameCamera;

        // 豌ｴ鬚ｨ闊ｹ縺ｮ遘ｻ蜍輔・謚墓憧繧堤ｮ｡逅・☆繧九さ繝ｳ繝昴・繝阪Φ繝医・
        [Header("攻撃対象：水風船")]
        [Tooltip("シーン内の水風船のWaterBalloonを設定します。")]
        [SerializeField] private WaterBalloon balloon;

        // 逋ｺ蟆・凾縺ｫ隍・｣ｽ縺吶ｋ謨ｵ蠑ｾ縺ｮPrefab縲・
        [Header("敵の攻撃：弾Prefab")]
        [Tooltip("敵弾PrefabのBulletを設定します。")]
        [SerializeField] private Bullet bulletPrefab;

        // 繝懊せ譛ｬ菴薙・謗･隗ｦ蛻､螳壹→縲√け繝ｪ繧｢譎ゅ・髱櫁｡ｨ遉ｺ縺ｫ菴ｿ逕ｨ縺吶ｋ縲・
        [Header("敵の配置：ボス本体")]
        [Tooltip("シーン内のボス本体のTransformを設定します。")]
        [SerializeField] private Transform boss;

        // 投げた水風船が当たると、ボスへダメージを与える位置。
        [Header("敵の弱点：ダメージ判定位置")]
        [Tooltip("シーン内の弱点のTransformを設定します。")]
        [SerializeField] private Transform weakPoint;

        // ============================================================
        // 敵弾幕制御：BulletPattern参照
        // ============================================================
        // Bossが使用する6種類の弾幕そのものはBulletPattern側で管理する。
        // Arena側はこの参照を通して、
        //
        // ・どのPatternを発射するか
        // ・現在まだ弾幕を生成中か
        // ============================================================
        [Header("敵の攻撃：弾幕パターン")]
        [Tooltip("6種類の敵弾幕を管理しているBulletPatternを設定します。")]
        [SerializeField]
        private BulletPattern bulletPattern;

        // 命中やパリィなどの円形エフェクトに使用する。
        [Header("演出：円形エフェクトの素材")]
        [Tooltip("エフェクトのLineRendererに使用するマテリアルを設定します。")]
        [SerializeField] private Material effectMaterial;

        [Header("難易度：ボスの最大HP")]
        [Tooltip("ラウンド開始時のボスのHPです。")]
        [SerializeField] private float bossMaxHealth = 150f;

        [Header("難易度：弾の発射間隔")]
        [Tooltip("弾を発射する間隔")]
        [SerializeField] private float shotInterval = 1.5f;

        [Header("キュー：無敵時間")]
        [Tooltip("キューを投げた直後の無敵時間")]
        [SerializeField] private float releaseInvulnerability = 0.22f;

        [Header("パリィ：投擲後の受付時間")]
        [Tooltip("投擲後にパリィが成立する受付時間")]
        [SerializeField] private float parryWindow = 0.12f;

        // ============================================================
        // 敵攻撃ルーティン設定
        // ============================================================
        [Header("敵攻撃ルーティン：各攻撃パターン後インターバル")]
        [Tooltip("Pattern1インターバル")]
        [SerializeField, Min(0.0f)]
        private float pattern1Interval = 2.0f;

        [Tooltip("Pattern2インターバル")]
        [SerializeField, Min(0.0f)]
        private float pattern2Interval = 2.0f;

        [Tooltip("Pattern3インターバル")]
        [SerializeField, Min(0.0f)]
        private float pattern3Interval = 2.0f;

        [Tooltip("Pattern4インターバル")]
        [SerializeField, Min(0.0f)]
        private float pattern4Interval = 2.0f;

        [Tooltip("Pattern5インターバル")]
        [SerializeField, Min(0.0f)]
        private float pattern5Interval = 2.0f;

        [Tooltip("Pattern6インターバル")]
        [SerializeField, Min(0.0f)]
        private float pattern6Interval = 2.0f;

        // ============================================================
        // 敵行動のランダム抽選設定
        // ============================================================
        // 画像の仕様:
        //
        // 行動1 : Pattern1 → Pattern2 → Pattern4
        // 行動2 : Pattern2 → Pattern4
        // 行動3 : Pattern3
        // 行動4 : Pattern2 → Pattern3
        //
        // weightは「確率そのもの」ではなく抽選時の重み。
        // 例えば全部25なら、それぞれ25%になる。
        // 10 / 20 / 30 / 40なら
        // 10% / 20% / 30% / 40%になる。
        // ============================================================
        [Header("敵行動：通常時の抽選確率（Weightなので合計が100％にする）")]

        [Tooltip("行動1：Pattern1 → Pattern2 → Pattern4の抽選")]
        [SerializeField, Min(0.0f)]
        private float action1Weight = 25.0f;

        [Tooltip("行動2：Pattern2 → Pattern4の抽選")]
        [SerializeField, Min(0.0f)]
        private float action2Weight = 25.0f;

        [Tooltip("行動3：Pattern3の抽選")]
        [SerializeField, Min(0.0f)]
        private float action3Weight = 25.0f;

        [Tooltip("行動4：Pattern2 → Pattern3の抽選")]
        [SerializeField, Min(0.0f)]
        private float action4Weight = 25.0f;

        // ============================================================
        // HP半分以下での抽選補正
        // ============================================================

        [Header("敵行動：HP低下時の抽選補正")]

        [Tooltip("このHP割合以下になると行動3・4の確率を上昇させる")]
        [SerializeField, Range(0.0f, 1.0f)]
        private float lowHpThreshold = 0.5f;

        [Tooltip("HP低下時の行動3の抽選倍率")]
        [SerializeField, Min(1.0f)]
        private float action3LowHpMultiplier = 2.0f;

        [Tooltip("HP低下時の行動4の抽選倍率")]
        [SerializeField, Min(1.0f)]
        private float action4LowHpMultiplier = 2.0f;

        // ============================================================
        // Pattern同時発動設定
        // ============================================================
        [Header("敵行動：Pattern同時発動")]
        [Tooltip("選ばれたPatternと別Patternを同時発動する確率")]
        [SerializeField, Range(0.0f, 100.0f)]
        private float simultaneousPatternChance = 20.0f;

        [Tooltip("同時発動時に追加されるPattern1の抽選ウェイト")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern1Weight = 10.0f;

        [Tooltip("同時発動時に追加されるPattern2の抽選ウェイト")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern2Weight = 10.0f;

        [Tooltip("同時発動時に追加されるPattern3の抽選ウェイト")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern3Weight = 10.0f;

        [Tooltip("同時発動時に追加されるPattern4の抽選ウェイト")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern4Weight = 10.0f;

        [Tooltip("同時発動時に追加されるPattern5の抽選ウェイト")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern5Weight = 10.0f;

        [Tooltip("同時発動時に追加されるPattern6の抽選ウェイト")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern6Weight = 10.0f;

        [Header("強攻撃：弾幕が薄くなった判定")]
        [Tooltip("画面に残っている弾幕がこの数以下になると強攻撃開始")]
        [SerializeField, Min(0)]
        private int strongAttackBulletThreshold = 5;

        [Tooltip("弾幕からの強攻撃インターバル")]
        [SerializeField, Min(0.0f)]
        private float strongAttackMaximumWait = 4.0f;

        [Header("強攻撃：AOEオブジェクト")]
        [Tooltip("赤い強攻撃範囲として使用するGameObject")]
        [SerializeField]
        private GameObject strongAttackObject;

        [Header("強攻撃：予兆時間")]
        [Tooltip("AOEが透明から完全表示になるまでの時間")]
        [SerializeField, Min(0.1f)]
        private float strongAttackChargeTime = 2.0f;

        [Header("強攻撃：当たり判定")]
        [Tooltip("赤い強攻撃範囲の半径")]
        [SerializeField, Min(0.01f)]
        private float strongAttackRadius = 3.0f;

        [Header("強攻撃：攻撃後の疲労時間")]
        [Tooltip("強攻撃終了後のインターバル")]
        [SerializeField, Min(0.0f)]
        private float strongAttackFatigueTime = 3.0f;

        // ============================================================
        // 敵ルーティン内部状態
        // ============================================================
        private enum EnemyAttackState
        {
            PatternFiring,
            PatternInterval,
            WaitingForBulletClear,
            StrongCharging,
            Fatigue
        }

        private EnemyAttackState enemyAttackState = EnemyAttackState.PatternInterval;

        // ============================================================
        // 現在実行中の「行動」の情報
        // ============================================================
        // 現在選ばれている行動のPattern配列。
        // 例：行動1なら { 1, 2, 4 }
        private int[] currentActionPatterns;

        // 配列の何番目まで実行したか。
        // 例：
        // 0 → Pattern1
        // 1 → Pattern2
        // 2 → Pattern4
        private int currentActionIndex = 0;

        // 今発射したPatternの番号。
        // 終了後のインターバル取得に使用する。
        private int lastMainPattern = 0;

        // 行動間待機タイマー
        private float patternIntervalTimer;

        // 弾幕が薄くなるのを待っている時間
        private float bulletClearWaitTimer;

        // 強攻撃予兆時間
        private float strongAttackTimer;

        // 強攻撃後の疲労時間
        private float fatigueTimer;

        // StrongAttackのRenderer
        private Renderer strongAttackRenderer;

        // α変更用
        private MaterialPropertyBlock strongAttackProperties;

        // ============================================================
        // Playerダメージ設定
        // ============================================================
        [Header("Player Damage Settings")]

        [Tooltip("被弾後の無敵時間です。単位は秒です。")]
        [SerializeField] private float damageInvincibleTime = 2.0f;

        [Tooltip("被弾時の点滅間隔です。単位は秒です。")]
        [SerializeField] private float blinkInterval = 0.1f;

        [Tooltip("残機UIをStock1、Stock2、Stock3の順に登録します。")]
        [SerializeField] private GameObject[] stockObjects;

        // 迴ｾ蝨ｨ縺ｮ谿区ｩ滓焚縺ｨ縲∬｢ｫ蠑ｾ貍泌・縺ｮ迥ｶ諷九・
        private int currentStock;
        private float damageInvincibleTimer;
        private float blinkTimer;
        private bool isGameOver;

        // 谿九ｊ譎る俣縺後≠繧矩俣縺ｯ縲√ヲ繝・ヨ繧ｹ繝医ャ繝嶺ｸｭ縺ｨ縺励※謇ｱ縺・・
        // 繧ｭ繝･繝ｼ縺悟ｼｱ轤ｹ縺ｫ蠖薙◆縺｣縺溽椪髢薙↓繧ｲ繝ｼ繝騾ｲ陦後ｒ豁｢繧√ｋ譎る俣縲ょｮ滓凾髢薙・遘呈焚縺ｧ謖・ｮ壹☆繧九・
        [Header("弱点命中：ヒットストップ")]
        [Tooltip("停止時間を秒で指定します。0で無効です。")]
        [SerializeField, Range(0f, 0.3f)] private float weakPointHitStop = 0.15f;
        // 蠑ｱ轤ｹ蜻ｽ荳ｭ譎ゅ↓縲√き繝｡繝ｩ繧堤判髱｢縺ｮ讓ｪ繝ｻ邵ｦ譁ｹ蜷代∈謠ｺ繧峨☆譛螟ｧ霍晞屬縲・
        [Header("弱点命中：カメラの揺れ幅")]
        [Tooltip("揺れの大きさです。0で無効にします。")]
        [SerializeField, Min(0f)] private float cameraShakeStrength = 0.12f;
        // 繝偵ャ繝医せ繝医ャ繝嶺ｸｭ繧ょｮ滓凾髢薙〒騾ｲ繧縲√き繝｡繝ｩ繧ｷ繧ｧ繧､繧ｯ縺ｮ陦ｨ遉ｺ譎る俣縲・
        [Header("弱点命中：カメラが揺れる時間")]
        [Tooltip("揺れの表示時間です。単位は秒です。")]
        [SerializeField, Min(0f)] private float cameraShakeDuration = 0.15f;
        // 繧ｫ繝｡繝ｩ繧ｷ繧ｧ繧､繧ｯ縺ｮ1遘偵≠縺溘ｊ縺ｮ謖ｯ蜍募屓謨ｰ縲・
        [Header("弱点命中：カメラの揺れる速さ")]
        [Tooltip("1秒あたりの振動回数です。")]
        [SerializeField, Min(1f)] private float cameraShakeFrequency = 35f;
        // 繧ｫ繝｡繝ｩ繧ｷ繧ｧ繧､繧ｯ繧帝幕蟋九＠縺ｦ縺九ｉ縺ｮ螳溽ｵ碁℃遘呈焚縲・
        private float cameraShakeElapsed;
        // 蜑榊屓縺ｮ謠冗判逕ｨ縺ｫ繧ｫ繝｡繝ｩ縺ｸ蜉縺医◆菴咲ｽｮ縺ｮ縺壹ｌ縲ょ・蜉帛愛螳壼燕縺ｨ貍泌・邨ゆｺ・凾縺ｫ蜿悶ｊ髯､縺上・
        private Vector3 cameraShakeOffset;
        // 蠑ｱ轤ｹ蜻ｽ荳ｭ縺ｮ繧ｷ繧ｧ繧､繧ｯ縺碁ｲ陦御ｸｭ縺九ゅご繝ｼ繝縺ｮ蛛懈ｭ｢荳ｭ繧よ昭繧後・譖ｴ譁ｰ縺ｯ邯咏ｶ壹☆繧九・
        public bool IsCameraShaking { get; private set; }
        // 繝繝｡繝ｼ繧ｸ縺悟・繧峨↑縺・Χ菴薙∈縺ｮ蜻ｽ荳ｭ縺ｫ菴ｿ縺・∫洒繧√・蛛懈ｭ｢譎る俣縲・
        [Header("ヒットストップ：ボス本体命中")]
        [Tooltip("本体命中時の停止時間です。単位は秒です。")]
        [SerializeField, Range(0f, 0.3f)] private float bodyHitStop = 0.04f;
        // 蜈ｬ霆｢荳ｭ繝ｻ謚墓憧荳ｭ縺ｮ繧ｭ繝･繝ｼ縺梧雰蠑ｾ繧呈ｶ医＠縺溘→縺阪・蛛懈ｭ｢譎る俣縲・
        [Header("ヒットストップ：敵弾消去")]
        [Tooltip("敵弾を消したときの停止時間です。単位は秒です。")]
        [SerializeField, Range(0f, 0.3f)] private float bulletHitStop = 0.025f;
        // 謨ｵ蠑ｾ繧偵く繝･繝ｼ縺ｧ豸医＠縺滄圀縲√く繝･繝ｼ縺ｮ螟門捉縺九ｉ蠎・′繧玖ｼｪ縺ｮ濶ｲ縲・
        [Header("弾消し演出：輪の色")]
        [Tooltip("水風船の外周に表示する輪の色です。")]
        [SerializeField] private Color bulletEraseColor = new Color(0.4f, 1f, 1f, 1f);
        // 蠖薙◆繧雁愛螳壹・螟門捉縺九ｉ縲∬ｼｪ縺ｮ蜊雁ｾ・ｒ霑ｽ蜉縺ｧ蠎・￡繧玖ｷ晞屬縲・
        [Header("弾消し演出：輪が広がる距離")]
        [Tooltip("当たり判定の外側へ広がる距離です。")]
        [SerializeField, Min(0f)] private float bulletEraseExpansion = 0.8f;
        // 蠑ｾ豸医＠縺ｮ霈ｪ縺悟・迴ｾ縺励※縺九ｉ豸医∴繧九∪縺ｧ縺ｮ譎る俣縲ゅヲ繝・ヨ繧ｹ繝医ャ繝嶺ｸｭ縺ｯ騾ｲ繧√↑縺・・
        [Header("弾消し演出：輪の表示時間")]
        [Tooltip("表示時間を秒で指定します。0で無効にします。")]
        [SerializeField, Min(0f)] private float bulletEraseDuration = 0.3f;
        // 蠑ｾ豸医＠縺ｮ霈ｪ繧呈緒縺冗ｷ壹・螟ｪ縺輔・
        [Header("弾消し演出：輪の線の太さ")]
        [Tooltip("輪を描く線の太さです。")]
        [SerializeField, Min(0.001f)] private float bulletEraseWidth = 0.09f;
        // 迴ｾ蝨ｨ繝偵ャ繝医せ繝医ャ繝嶺ｸｭ縺九ょ●豁｢荳ｭ縺ｯ繧ｲ繝ｼ繝騾ｲ陦後→霑ｽ蜉縺ｮ蜻ｽ荳ｭ蛻､螳壹ｒ陦後ｏ縺ｪ縺・・
        public bool IsHitStopped => hitStopRemaining > 0f;
        private float hitStopRemaining;

        // 蜻ｽ荳ｭ縺励◆豌ｴ鬚ｨ闊ｹ繧偵∝●豁｢邨ゆｺ・ｾ後↓豸郁ｲｻ縺吶ｋ縺溘ａ縺ｮ莠育ｴ・・
        private bool consumeBalloonAfterHitStop;

        // 迴ｾ蝨ｨ縺ｮ繧ｲ繝ｼ繝騾ｲ陦檎憾諷九・
        public RoundState State { get; private set; }
        // 繝懊せ縺ｮ谿九ｊHP縲・縺ｫ縺ｪ繧九→繧ｯ繝ｪ繧｢縲・
        public float BossHealth { get; private set; }
        // 縺薙・繝ｩ繧ｦ繝ｳ繝峨〒謌仙粥縺励◆繝代Μ繧｣縺ｮ蝗樊焚縲・
        public int ParryCount { get; private set; }
        // 迴ｾ蝨ｨ邂｡逅・＠縺ｦ縺・ｋ謨ｵ蠑ｾ縺ｮ謨ｰ縲・
        public int ActiveBulletCount => bullets.Count;
        // 繝励Ξ繧､繝､繝ｼ荳ｭ蠢・・遘ｻ蜍慕ｯ・峇縲３ect縺ｮ讓ｪ霆ｸ縺ｯ繝ｯ繝ｼ繝ｫ繝厩縲∫ｸｦ霆ｸ縺ｯ繝ｯ繝ｼ繝ｫ繝瓜縺ｫ蟇ｾ蠢懊☆繧九・
        public static Rect MovementBounds => Rect.MinMaxRect(-4.1f, -7.5f, 4.1f, 3.7f);
        // 逕ｻ髱｢蜀・〒譖ｴ譁ｰ繝ｻ蛻､螳壹☆繧区雰蠑ｾ縺ｮ荳隕ｧ縲・
        private readonly List<Bullet> bullets = new List<Bullet>();
        // 陦ｨ遉ｺ荳ｭ縺ｮ蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝医・荳隕ｧ縲・
        private readonly List<Pulse> pulses = new List<Pulse>();
        // 逋ｺ蟆・∪縺ｧ縺ｮ谿九ｊ遘呈焚縲∫┌謨ｵ縺ｮ谿九ｊ遘呈焚縲√ヱ繝ｪ繧｣縺ｮ谿九ｊ遘呈焚縲∫ｵゆｺ・ｾ後・邨碁℃遘呈焚縲・
        private float shotTimer, // 谺｡縺ｮ逋ｺ蟆・∪縺ｧ縺ｮ谿九ｊ遘呈焚縲・
            invincible, // 辟｡謨ｵ縺ｮ谿九ｊ遘呈焚縲・
            parryRemaining, // 繝代Μ繧｣蜿嶺ｻ倥・谿九ｊ遘呈焚縲・
            endTimer; // 繝ｩ繧ｦ繝ｳ繝臥ｵゆｺ・ｾ後・邨碁℃遘呈焚縲・
        // 逋ｺ蟆・ｸ医∩蠑ｾ蟷輔・騾壹＠逡ｪ蜿ｷ縲ょｼｷ縺・竊・竊・縺ｮ蛻・ｊ譖ｿ縺医↓菴ｿ縺・・
        private int wave;

        // 同時に存在できる敵弾の最大数。
        private const int MaximumBulletCount = 200; // 弾幕によるGameObjectの無制限生成を防止する。

        // 蜑阪ヵ繝ｬ繝ｼ繝縺ｮ繝励Ξ繧､繝､繝ｼ菴咲ｽｮ縲り｡晉ｪ∬ｨ育ｮ礼畑縺ｮ遘ｻ蜍募玄髢薙・蟋狗せ縲・
        private Vector3 previousPlayerPosition;
        // 荳譎ら噪縺ｪ蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝医・邱壹∫ｵ碁℃譎る俣縲∬牡縲∵怙邨ょ濠蠕・ｒ縺ｾ縺ｨ繧√◆繝・・繧ｿ縲・
        private sealed class Pulse
        {
            // 蜀・ｒ謠冗判縺吶ｋ邱壹さ繝ｳ繝昴・繝阪Φ繝医・
            public LineRenderer line;
            // 蜃ｺ迴ｾ縺九ｉ縺ｮ邨碁℃譎る俣・育ｧ抵ｼ峨・
            public float age;
            // 蜃ｺ迴ｾ譎ゅ・蝓ｺ譛ｬ濶ｲ縲よ凾髢鍋ｵ碁℃縺ｧ證励￥縺吶ｋ蝓ｺ貅悶・
            public Color color;
            // 諡｡螟ｧ縺檎ｵゅｏ縺｣縺溘→縺阪・蜊雁ｾ・ｼ医Ρ繝ｼ繝ｫ繝牙腰菴搾ｼ峨・
            public float radius;
            // 蜃ｺ迴ｾ譎ゅ・蜊雁ｾ・ょｼｾ豸医＠縺ｧ縺ｯ繧ｭ繝･繝ｼ縺ｮ蠖薙◆繧雁愛螳壹・螟門捉縺九ｉ謠上￥縲・
            public float startRadius;
            // 蜀・′螳悟・縺ｫ豸医∴繧九∪縺ｧ縺ｮ遘呈焚縲・
            public float duration;
        }

        // Android縺ｧ縺ｯ邵ｦ逕ｻ髱｢縺ｫ蝗ｺ螳壹＠縲∵怙蛻昴・繝ｩ繧ｦ繝ｳ繝峨ｒ髢句ｧ九☆繧九・
        private void Start()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayBGM("Stage1BGM");
            }
            if (Application.platform == RuntimePlatform.Android) Screen.orientation = ScreenOrientation.Portrait;

            // ============================================================
            // StrongAttack初期設定
            // ============================================================
            if (strongAttackObject != null)
            {
                strongAttackRenderer =
                    strongAttackObject.GetComponentInChildren<Renderer>();

                strongAttackProperties =
                    new MaterialPropertyBlock();

                SetStrongAttackAlpha(0.0f);

                strongAttackObject.SetActive(false);
            }

            ResetRound();
        }

        // ============================================================
        // 次に実行する行動をランダム抽選
        // ============================================================
        private void SelectRandomAction()
        {
            // Bossの現在HP割合。
            float hpRate =
                BossHealth /
                Mathf.Max(bossMaxHealth, 0.01f);

            // Inspectorで設定された通常ウェイトを取得。
            float weight1 = action1Weight;
            float weight2 = action2Weight;
            float weight3 = action3Weight;
            float weight4 = action4Weight;

            // --------------------------------------------------------
            // HPが指定割合以下なら行動3・4を選ばれやすくする
            // --------------------------------------------------------
            if (hpRate <= lowHpThreshold)
            {
                weight3 *= action3LowHpMultiplier;
                weight4 *= action4LowHpMultiplier;
            }

            // 全ウェイト合計。
            float totalWeight =
                weight1 +
                weight2 +
                weight3 +
                weight4;

            // 全部0だと抽選できないため行動1を使用。
            if (totalWeight <= 0.0f)
            {
                currentActionPatterns =
                    new int[] { 1, 2, 4 };

                currentActionIndex = 0;
                return;
            }

            // 0～合計値のどこかをランダム選択。
            float randomValue =
                Random.Range(0.0f, totalWeight);

            // --------------------------------------------------------
            // 行動1
            // Pattern1 → Pattern2 → Pattern4
            // --------------------------------------------------------
            if (randomValue < weight1)
            {
                currentActionPatterns =
                    new int[] { 1, 2, 4 };

                Debug.Log(
                    "Enemy Action Selected : Action1 [1 → 2 → 4]");
            }

            // --------------------------------------------------------
            // 行動2
            // Pattern2 → Pattern4
            // --------------------------------------------------------
            else if (randomValue < weight1 + weight2)
            {
                currentActionPatterns =
                    new int[] { 2, 4 };

                Debug.Log(
                    "Enemy Action Selected : Action2 [2 → 4]");
            }

            // --------------------------------------------------------
            // 行動3
            // Pattern3のみ
            // --------------------------------------------------------
            else if (randomValue <
                     weight1 + weight2 + weight3)
            {
                currentActionPatterns =
                    new int[] { 3 };

                Debug.Log(
                    "Enemy Action Selected : Action3 [3]");
            }

            // --------------------------------------------------------
            // 行動4
            // Pattern2 → Pattern3
            // --------------------------------------------------------
            else
            {
                currentActionPatterns =
                    new int[] { 2, 3 };

                Debug.Log(
                    "Enemy Action Selected : Action4 [2 → 3]");
            }

            // 新しい行動なので先頭から開始。
            currentActionIndex = 0;
        }

        // 谿九▲縺溷ｼｾ繝ｻ貍泌・繧堤援莉倥￠縲？P縲√ち繧､繝槭・縲√・繝ｬ繧､繝､繝ｼ縺ｨ鬚ｨ闊ｹ繧帝幕蟋狗憾諷九↓謌ｻ縺吶・
        public void ResetRound()
        {
            roundElapsed = 0f;
            // ============================================================
            // PlayerのStockを初期化
            // ============================================================
            currentStock = stockObjects.Length;
            damageInvincibleTimer = 0.0f;
            blinkTimer = 0.0f;
            isGameOver = false;

            // 敵行動を初期状態へ戻す。
            // 最初の攻撃時に行動1～4からランダム抽選される。
            currentActionPatterns = null;
            currentActionIndex = 0;
            lastMainPattern = 0;

            // Playerを操作可能状態に戻す
            player.SetCanMove(true);

            // Stockをすべて表示する
            for (int i = 0; i < stockObjects.Length; i++)
            {
                if (stockObjects[i] != null)
                {
                    stockObjects[i].SetActive(true);
                }
            }

            StopCameraShake();
            CompleteHitStop();
            ClearBullets();
            // pulse・壻ｸ隕ｧ縺九ｉ蜿悶ｊ蜃ｺ縺励◆縲∽ｻ雁屓蜃ｦ逅・☆繧句ｯｾ雎｡縲・
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

            // ============================================================
            // 敵攻撃ルーティン初期化
            // ============================================================
            enemyAttackState = EnemyAttackState.PatternInterval;

            // 最初だけ少し待ってから攻撃開始
            patternIntervalTimer = 2.5f;

            bulletClearWaitTimer = 0.0f;

            strongAttackTimer = 0.0f;

            fatigueTimer = 0.0f;

            if (strongAttackObject != null)
            {
                strongAttackObject.SetActive(false);

                SetStrongAttackAlpha(0.0f);
            }

            boss.gameObject.SetActive(true);
            weakPoint.gameObject.SetActive(true);
            player.ResetPlayer(new Vector3(0f, 0.65f, -4.5f));
            previousPlayerPosition = player.transform.position;
            // 繝励Ξ繧､繝､繝ｼ縺ｨ鬚ｨ闊ｹ縺ｧ諤ｧ閭ｽ繧貞・譛峨☆繧九ゅΜ繝医Λ繧､縺励※繧り｣・ｙ荳ｭ縺ｮ繧ｹ繧ｭ繝ｫ陬懈ｭ｣縺ｯ菫晄戟縺吶ｋ縲・
            balloon.SetPlayerParameters(player.Parameters);
            balloon.ResetBalloon(player.transform.position);
        }

        // 豈弱ヵ繝ｬ繝ｼ繝縺ｮ騾ｲ陦悟・逅・ゅ・繝ｬ繧､繝､繝ｼ蜈･蜉帙・1蝗櫁ｪｭ縺ｿ縲∫黄逅・噪縺ｪ遘ｻ蜍輔→陦晉ｪ√□縺醍ｴｰ蛻・喧縺吶ｋ縲・
        private void Update()
        {
            AdvanceFrame(Time.deltaTime, Time.unscaledDeltaTime);
        }

        // 蜈･蜉帙・遘ｻ蜍輔・蜃ｦ逅・ｾ後↓謠冗判逕ｨ縺ｮ謠ｺ繧後ｒ蜉縺医ｋ縲５ime.timeScale縺ｫ縺ｯ萓晏ｭ倥＠縺ｪ縺・・
        private void LateUpdate()
        {
            AdvanceCameraShake(Time.unscaledDeltaTime);
        }

        // deltaTime縺ｯ繧ｲ繝ｼ繝蜀・・邨碁℃遘呈焚縲「nscaledDeltaTime縺ｯ譎る俣蛟咲紫縺ｫ萓晏ｭ倥＠縺ｪ縺・ｮ溽ｵ碁℃遘呈焚縲・
        // 螳滄圀縺ｮUpdate縺ｨ讀懆ｨｼ縺ｧ蜷後§騾ｲ陦悟・逅・ｒ菴ｿ縺・∝●豁｢荳ｭ縺ｮ遘ｻ蜍輔ｄ蠕ｩ蟶ｰ繧堤｢ｺ隱阪〒縺阪ｋ繧医≧縺ｫ縺吶ｋ縲・
        public void AdvanceFrame(float deltaTime, float unscaledDeltaTime)
        {
            if (State == RoundState.Playing) roundElapsed += Mathf.Max(0f, deltaTime);
            // 謠ｺ繧後ｒ繧ｿ繝・メ蠎ｧ讓吶・螟画鋤縺ｫ豺ｷ縺懊↑縺・る撕豁｢縺励◆謖・〒繝励Ξ繧､繝､繝ｼ縺悟虚縺上・繧帝亟縺舌・
            RestoreCameraOffset();
            // 蛛懈ｭ｢荳ｭ繧３繧ｭ繝ｼ縺ｧ蜊ｳ蠎ｧ縺ｫ繝ｪ繝医Λ繧､縺ｧ縺阪ｋ縲・
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetRound();
                return;
            }
            if (IsHitStopped)
            {
                // 蠎ｧ讓吶・蝓ｺ貅悶□縺第峩譁ｰ縺励※蠕ｩ蟶ｰ譎ゅ・繧ｸ繝｣繝ｳ繝励ｒ髦ｲ縺弱・屬縺励◆蜈･蜉帙・繝励Ξ繧､繝､繝ｼ蛛ｴ縺ｫ菫晏ｭ倥☆繧九・
                player.ReadInput(gameCamera, MovementBounds, unscaledDeltaTime, freezeMovement: true);
                AdvanceHitStop(unscaledDeltaTime);
                return;
            }
            // 逕ｻ髱｢縺檎ｸｦ髟ｷ縺ｧ繧ょｷｦ蜿ｳ縺ｮ繝励Ξ繧､鬆伜沺繧堤｢ｺ菫昴☆繧九ｈ縺・√き繝｡繝ｩ縺ｮ陦ｨ遉ｺ遽・峇繧貞ｺ・￡繧九・
            gameCamera.orthographicSize = Mathf.Max(9f, 5f / Mathf.Max(gameCamera.aspect, 0.1f));
            // 蜃ｦ逅・誠縺｡蠕後↓荳蠎ｦ縺ｫ螟ｧ縺阪￥蜍輔￥縺ｮ繧帝∩縺代ｋ縺溘ａ縲・繝輔Ξ繝ｼ繝縺ｧ騾ｲ繧√ｋ譎る俣繧・.1遘偵∪縺ｧ縺ｫ縺吶ｋ縲・
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            
            // 陲ｫ蠑ｾ蠕後・辟｡謨ｵ譎る俣縺ｨ轤ｹ貊・ｒ譖ｴ譁ｰ縺吶ｋ縲・
            UpdateDamageInvincibility(dt);
            
            UpdatePulses(dt);
            player.ReadInput(gameCamera, MovementBounds, dt);
            if (State != RoundState.Playing)
            {
                endTimer += dt;
                if (Application.CanStreamedLevelBeLoaded(ResultScreen.ScenePath))
                {
                    if (endTimer > 0.75f) SceneManager.LoadScene(ResultScreen.ScenePath);
                    return;
                }
                // 邨ゆｺ・°繧・.75遘貞ｾ後∵眠縺励￥繧ｿ繝・・・上け繝ｪ繝・け縺吶ｋ縺ｨ蜀埼幕縺吶ｋ縲ゅΓ繝九Η繝ｼ謫堺ｽ懊・荳崎ｦ√・
                if (endTimer > 0.75f && player.PressedThisFrame) ResetRound();
                else player.transform.position = previousPlayerPosition;
                return;
            }
            // bufferedRelease縺ｯ蛛懈ｭ｢荳ｭ縺ｮ謚墓憧莠育ｴ・・譛臥┌縲｜ufferedFlick縺ｯ縺昴・迸ｬ髢薙・騾溷ｺｦ縲・
            // 莠育ｴ・′縺ゅｌ縺ｰ譛譁ｰ縺ｮ蜈･蜉帙ｈ繧雁━蜈医＠縲∝ｾｩ蟶ｰ譎ゅ↓1蝗槭□縺第兜縺偵ｋ縲・
            bool bufferedRelease = player.TryConsumeBufferedRelease(out Vector3 bufferedFlick);
            if ((bufferedRelease || player.ReleasedThisFrame) && balloon.Launch(bufferedRelease ? bufferedFlick : player.FlickVelocity))
                BeginReleaseProtection();
            // 莉雁屓縺ｮ蜈･蜉帙ｒ蜿肴丐縺励◆繝励Ξ繧､繝､繝ｼ縺ｮ蛻ｰ驕比ｽ咲ｽｮ縲・
            Vector3 currentPlayerPosition = player.transform.position;
            // 1蝗槭・譖ｴ譁ｰ繧呈怙螟ｧ1/120遘偵↓蛻・牡縺励※縺ｰ縺ｭ縺ｮ險育ｮ励ｒ螳牙ｮ壹＆縺帙ｋ縲・
            // 繝励Ξ繧､繝､繝ｼ縺ｮ遘ｻ蜍輔ｂ蛹ｺ髢薙＃縺ｨ縺ｫ陬憺俣縺励※縲∫ｧｻ蜍穂ｸｭ縺ｮ陦晉ｪ√ｒ蛻､螳壹☆繧九・
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
            // 邏ｰ蛻・喧縺励◆1蝗槫・縺ｮ繧ｷ繝溘Η繝ｬ繝ｼ繧ｷ繝ｧ繝ｳ譎る俣・育ｧ抵ｼ峨・
            float step = dt / steps;
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = 0; i < steps && State == RoundState.Playing; i++)
            {
                // 莉雁屓縺ｮ邏ｰ蛻・喧蛹ｺ髢薙・蟋狗せ縺ｨ縺ｪ繧九・繝ｬ繧､繝､繝ｼ菴咲ｽｮ縲・
                Vector3 from = Vector3.Lerp(previousPlayerPosition, currentPlayerPosition, i / (float)steps);
                // 莉雁屓縺ｮ邏ｰ蛻・喧蛹ｺ髢薙・邨らせ縺ｨ縺ｪ繧九・繝ｬ繧､繝､繝ｼ菴咲ｽｮ縲・
                Vector3 to = Vector3.Lerp(previousPlayerPosition, currentPlayerPosition, (i + 1f) / steps);
                balloon.Simulate(to, player.Velocity, player.IsHeld, step);
                TickBullets(from, to, step);
                if (State != RoundState.Playing || IsHitStopped) break;
                CheckBossHit();
                // 蜻ｽ荳ｭ縺励◆繝輔Ξ繝ｼ繝縺ｮ谿九ｊ縺ｮ邏ｰ蛻・喧蜃ｦ逅・ｂ豁｢繧√√く繝･繝ｼ縺碁ｲ縺ｿ邯壹￠繧九・繧帝亟縺舌・
                if (IsHitStopped) break;
                invincible = Mathf.Max(0f, invincible - step);
                parryRemaining = Mathf.Max(0f, parryRemaining - step);
            }
            previousPlayerPosition = currentPlayerPosition;
            if (State != RoundState.Playing || IsHitStopped) return;

            // 被弾無敵中ではない場合だけ、既存の色処理を行う
            if (damageInvincibleTimer <= 0.0f && !isGameOver)
            {
                player.SetColor(
                    invincible > 0.0f
                        ? Color.white
                        : new Color(0.12f, 0.8f, 1.0f)
                );
            }

            if (damageInvincibleTimer <= 0.0f && !isGameOver)
            {
                player.SetColor(
                    invincible > 0.0f
                        ? Color.white
                        : new Color(0.12f, 0.8f, 1.0f)
                );
            }

            // ============================================================
            // 敵攻撃ルーティン
            // ============================================================
            UpdateEnemyAttackRoutine(dt);
        }

        public Bullet SpawnBullet(
               Vector3 position,
               Vector3 velocity,
               int power)
        {
            // 弾幕が大量に発生しても、
            // 無制限にGameObjectを生成しない。
            if (bullets.Count >= MaximumBulletCount)
            {
                return null;
            }

            Bullet Bullet =
                Instantiate(
                    bulletPrefab,
                    transform);

            Bullet.Initialize(
                position,
                velocity,
                power);

            bullets.Add(Bullet);

            return Bullet;
        }

        // ============================================================
        // Player被弾処理
        // ============================================================
        private void DamagePlayer()
        {
            // すでにGameOverなら何もしない
            if (isGameOver)
            {
                return;
            }

            // 無敵時間中ならダメージを受けない
            if (damageInvincibleTimer > 0.0f)
            {
                return;
            }
            else
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySE("PlayerDamageSE");
                }
            }

            // ---------------------------------------------------------
            // Stockが残っている場合
            // ---------------------------------------------------------
            if (currentStock > 0)
            {
                // Stockを1つ減らす
                currentStock--;

                // 減ったStockのUIを非表示にする
                if (currentStock < stockObjects.Length &&
                    stockObjects[currentStock] != null)
                {
                    stockObjects[currentStock].SetActive(false);
                }

                // 被弾後の無敵時間開始
                damageInvincibleTimer = damageInvincibleTime;

                // 点滅タイマーを初期化
                blinkTimer = 0.0f;

                Debug.Log("Player Hit! Remaining Stock : " + currentStock);

                return;
            }

            // ---------------------------------------------------------
            // Stockが0の状態でさらに被弾した場合
            // ---------------------------------------------------------
            //GameOver();
        }

        // ============================================================
        // 被弾後の無敵時間・点滅処理
        // ============================================================
        private void UpdateDamageInvincibility(float dt)
        {
            // 無敵時間が終了している場合
            if (damageInvincibleTimer <= 0.0f)
            {
                // 通常色へ戻す
                player.SetColor(new Color(0.12f, 0.8f, 1.0f));
                return;
            }

            // 無敵時間を減らす
            damageInvincibleTimer -= dt;

            // 点滅タイマーを進める
            blinkTimer += dt;

            // 一定時間ごとに色を切り替える
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0.0f;

                // 残り時間から白と通常色を交互に切り替える
                bool showWhite =
                    Mathf.FloorToInt(damageInvincibleTimer / blinkInterval) % 2 == 0;

                if (showWhite)
                {
                    player.SetColor(Color.white);
                }
                else
                {
                    player.SetColor(new Color(0.12f, 0.8f, 1.0f));
                }
            }

            // 無敵時間終了
            if (damageInvincibleTimer <= 0.0f)
            {
                damageInvincibleTimer = 0.0f;
                player.SetColor(new Color(0.12f, 0.8f, 1.0f));
            }
        }

        // ============================================================
        // Pattern発射
        // ============================================================
        // メインPatternを必ず発射する。
        // さらにInspectorで設定した確率に成功した場合、
        // 別のPatternを1種類追加して同時発射する。
        // ============================================================
        private void FireEnemyPattern(
            int mainPattern)
        {
            // メインPattern発射。
            bulletPattern.FirePattern(
                mainPattern);

            lastMainPattern =
                mainPattern;

            // --------------------------------------------------------
            // 同時発動するか抽選
            // --------------------------------------------------------
            float simultaneousRoll =
                Random.Range(0.0f, 100.0f);

            // 抽選失敗ならメインPatternだけ。
            if (simultaneousRoll >
                simultaneousPatternChance)
            {
                return;
            }

            // 追加Patternを抽選。
            int extraPattern =
                SelectSimultaneousPattern(
                    mainPattern);

            // 有効なPatternがなければ終了。
            if (extraPattern <= 0)
            {
                return;
            }

            // --------------------------------------------------------
            // 追加Patternを同時発射
            // --------------------------------------------------------
            bulletPattern.FirePattern(
                extraPattern);

            Debug.Log(
                $"Simultaneous Pattern : " +
                $"{mainPattern} + {extraPattern}");
        }

        // ============================================================
        // 同時発動する追加Patternを抽選
        // ============================================================
        // mainPatternと同じPatternは候補から除外する。
        // 各Patternの出現しやすさはInspectorから調整可能。
        // ============================================================
        private int SelectSimultaneousPattern(
            int mainPattern)
        {
            float[] weights =
            {
                simultaneousPattern1Weight,
                simultaneousPattern2Weight,
                simultaneousPattern3Weight,
                simultaneousPattern4Weight,
                simultaneousPattern5Weight,
                simultaneousPattern6Weight
            };

            // メインと同じPatternは同時発動させない。
            weights[mainPattern - 1] = 0.0f;

            float totalWeight = 0.0f;

            for (int i = 0;
                 i < weights.Length;
                 i++)
            {
                totalWeight +=
                    Mathf.Max(
                        0.0f,
                        weights[i]);
            }

            // 候補が全部0なら追加Patternなし。
            if (totalWeight <= 0.0f)
            {
                return 0;
            }

            float randomValue =
                Random.Range(
                    0.0f,
                    totalWeight);

            float accumulatedWeight =
                0.0f;

            for (int i = 0;
                 i < weights.Length;
                 i++)
            {
                accumulatedWeight +=
                    Mathf.Max(
                        0.0f,
                        weights[i]);

                if (randomValue <
                    accumulatedWeight)
                {
                    // 配列0～5をPattern1～6へ変換。
                    return i + 1;
                }
            }

            return 0;
        }

        // 謚墓憧謌仙粥譎ゅ↓辟｡謨ｵ縺ｨ繝代Μ繧｣縺ｮ蜿嶺ｻ倥ｒ蜷梧凾縺ｫ髢句ｧ九＠縲∫區縺・・縺ｧ蜷亥峙縺吶ｋ縲・
        public void BeginReleaseProtection()
        {
            invincible = releaseInvulnerability;
            parryRemaining = parryWindow;
            EmitPulse(player.transform.position, Color.white, 0.8f);
        }

        // ============================================================
        // 敵攻撃ルーティン更新
        // ============================================================
        // Bossの攻撃を以下の順番で進行させる。
        //
        // Pattern1
        //   ↓
        // 待機
        //   ↓
        // Pattern2
        //   ↓
        // 待機
        //   ↓
        // Pattern3
        //   ↓
        // 待機
        //   ↓
        // Pattern4
        //   ↓
        // 待機
        //   ↓
        // Pattern5
        //   ↓
        // 待機
        //   ↓
        // Pattern6
        //   ↓
        // 待機
        //   ↓
        // 画面上の敵弾が少なくなるまで待機
        //   ↓
        // 強攻撃予兆
        //   ↓
        // 強攻撃判定
        //   ↓
        // 疲労時間
        //   ↓
        // Pattern1へ戻る
        //
        // 実際の弾生成はBulletPatternが担当し、
        // PrototypeArena「攻撃全体の進行」
        // ============================================================
        private void UpdateEnemyAttackRoutine(float dt)
        {
            // BulletPatternがInspectorで設定されていなければ
            // 弾幕を発射できないので処理を終了する。
            if (bulletPattern == null)
            {
                return;
            }

            // 現在のBoss攻撃状態によって処理を切り替える。
            switch (enemyAttackState)
            {
                // ========================================================
                // ① Pattern発射中
                // ========================================================
                case EnemyAttackState.PatternFiring:
                    {
                        // 同時発動したPatternを含め、
                        // 全Coroutineが終了するまで待つ。
                        if (bulletPattern.IsFiring)
                        {
                            return;
                        }

                        // 今発射したメインPatternに対応した
                        // Inspector設定のインターバルを取得。
                        patternIntervalTimer =
                            GetPatternInterval(
                                lastMainPattern);

                        // 現在の行動内で次のPatternへ進む。
                        currentActionIndex++;

                        enemyAttackState =
                            EnemyAttackState.PatternInterval;

                        break;
                    }

                // ========================================================
                // ② Pattern間インターバル
                // ========================================================
                case EnemyAttackState.PatternInterval:
                    {
                        patternIntervalTimer -= dt;

                        // まだ待機中。
                        if (patternIntervalTimer > 0.0f)
                        {
                            return;
                        }

                        // ----------------------------------------------------
                        // 行動がまだ選ばれていない場合
                        // ----------------------------------------------------
                        if (currentActionPatterns == null)
                        {
                            SelectRandomAction();
                        }

                        // ----------------------------------------------------
                        // 現在の行動に含まれるPatternを全部終了
                        // ----------------------------------------------------
                        if (currentActionIndex >=
                            currentActionPatterns.Length)
                        {
                            // 今回の行動終了。
                            currentActionPatterns = null;

                            // 強攻撃前の弾減少待機へ。
                            bulletClearWaitTimer = 0.0f;

                            enemyAttackState =
                                EnemyAttackState.WaitingForBulletClear;

                            return;
                        }

                        // ----------------------------------------------------
                        // 行動内の次のPatternを取得
                        // ----------------------------------------------------
                        int nextPattern =
                            currentActionPatterns[
                                currentActionIndex];

                        // Pattern発射。
                        // この中で同時発動抽選も行う。
                        FireEnemyPattern(
                            nextPattern);

                        enemyAttackState =
                            EnemyAttackState.PatternFiring;

                        break;
                    }

                // ========================================================
                // ③ Pattern6終了後
                //    画面上の敵弾が少なくなるまで待機
                // ========================================================
                case EnemyAttackState.WaitingForBulletClear:
                    {
                        // この状態になってからの経過時間を加算。
                        bulletClearWaitTimer += dt;

                        // ----------------------------------------------------
                        // 条件A：
                        // 残っている敵弾が指定数以下になった
                        // ----------------------------------------------------
                        bool enoughBulletsCleared =
                            bullets.Count <=
                            strongAttackBulletThreshold;

                        // ----------------------------------------------------
                        // 条件B：
                        // 最大待機時間を超えた
                        // ----------------------------------------------------
                        //
                        // 弾が何らかの理由で減らない場合でも、
                        // Bossが永久に止まらないための保険。
                        bool waitedTooLong =
                            bulletClearWaitTimer >=
                            strongAttackMaximumWait;

                        // A・Bどちらも満たしていない場合は
                        // まだ強攻撃を開始しない。
                        if (!enoughBulletsCleared &&
                            !waitedTooLong)
                        {
                            return;
                        }

                        // 弾が十分減った、または最大待機時間を超えたので
                        // 強攻撃予兆を開始。
                        BeginStrongAttack();

                        break;
                    }


                // ========================================================
                // ④ 強攻撃の予兆中
                // ========================================================
                case EnemyAttackState.StrongCharging:
                    {
                        // 強攻撃予兆の経過時間。
                        strongAttackTimer += dt;

                        // ----------------------------------------------------
                        // 予兆の進行度を0～1へ変換
                        // ----------------------------------------------------
                        //
                        // 例：
                        // strongAttackChargeTime = 2秒
                        //
                        // 0秒 → 0.0
                        // 1秒 → 0.5
                        // 2秒 → 1.0
                        //
                        float progress =
                            Mathf.Clamp01(
                                strongAttackTimer /
                                Mathf.Max(
                                    strongAttackChargeTime,
                                    0.01f));

                        // 赤い強攻撃範囲を
                        // 透明 → 完全表示へ徐々に変化させる。
                        SetStrongAttackAlpha(
                            progress);

                        // まだ100%になっていなければ
                        // 強攻撃は発動しない。
                        if (progress < 1.0f)
                        {
                            return;
                        }

                        // 100%になった瞬間に強攻撃判定。
                        ExecuteStrongAttack();

                        break;
                    }


                // ========================================================
                // ⑤ 強攻撃終了後の疲労状態
                // ========================================================
                case EnemyAttackState.Fatigue:
                    {
                        // 毎フレーム疲労時間を減らす。
                        fatigueTimer -= dt;

                        // 疲労時間が残っている間はBossは何もしない。
                        if (fatigueTimer > 0.0f)
                        {
                            return;
                        }

                        // ========================================================
                        // 攻撃ルーティン1周終了
                        // 強攻撃後、次の行動を新しくランダム抽選する
                        // ========================================================
                        // 前回の行動を破棄。
                        currentActionPatterns = null;

                        // 配列位置を初期化。
                        currentActionIndex = 0;

                        // 前回Pattern情報もリセット。
                        lastMainPattern = 0;

                        // 次フレームから新しい行動を抽選。
                        patternIntervalTimer = 0.0f;

                        enemyAttackState =
                            EnemyAttackState.PatternInterval;

                        break;
                        
                    }
            }
        }

        // 謾ｻ謦・ヱ繧ｿ繝ｼ繝ｳ邨ゆｺ・ｾ後・蠕・■譎る俣繧定ｿ斐☆縲・
        private float GetPatternInterval(int patternNumber)
        {
            switch (patternNumber)
            {
                case 1:
                    return pattern1Interval;

                case 2:
                    return pattern2Interval;

                case 3:
                    return pattern3Interval;

                case 4:
                    return pattern4Interval;

                case 5:
                    return pattern5Interval;

                case 6:
                    return pattern6Interval;

                default:
                    return 0.0f;
            }
        }

        // ============================================================
        // 強攻撃開始
        // ============================================================
        private void BeginStrongAttack()
        {
            enemyAttackState =
                EnemyAttackState.StrongCharging;

            strongAttackTimer = 0.0f;

            // 注意：
            // ここではClearBulletsしない。
            // 残っている弾はそのまま画面外へ流す。

            if (strongAttackObject != null)
            {
                strongAttackObject.SetActive(true);

                SetStrongAttackAlpha(0.0f);
            }

            Debug.Log( "Strong Attack Start! Remaining Bullets : " + bullets.Count);
        }

        // ============================================================
        // 強攻撃発動
        // ============================================================
        private void ExecuteStrongAttack()
        {
            SetStrongAttackAlpha(1.0f);

            bool playerInside =
                IsPlayerInsideStrongAttack();

            if (playerInside)
            {
                DamagePlayer();

                Debug.Log(
                    "Strong Attack HIT!");
            }
            else
            {
                Debug.Log(
                    "Strong Attack MISS!");
            }

            if (strongAttackObject != null)
            {
                strongAttackObject.SetActive(false);
            }

            SetStrongAttackAlpha(0.0f);

            enemyAttackState =
                EnemyAttackState.Fatigue;

            fatigueTimer =
                strongAttackFatigueTime;
        }

        // ============================================================
        // 強攻撃範囲内にPlayerがいるか判定
        // ============================================================
        // StrongAttackオブジェクトの中心位置とPlayer位置の距離を調べ、
        // Inspectorで設定したstrongAttackRadius以内ならtrueを返す。
        // このゲームは上から見下ろす形式なので、Y（高さ）は無視して
        // XZ平面だけで距離を計算する。
        // ============================================================
        private bool IsPlayerInsideStrongAttack()
        {
            // StrongAttackまたはPlayerが未設定なら
            // 正しい判定ができないため攻撃範囲外として扱う。
            if (strongAttackObject == null ||
                player == null)
            {
                return false;
            }

            // 強攻撃オブジェクトの中心位置を取得。
            Vector3 attackPosition =
                strongAttackObject.transform.position;

            // 現在のPlayer位置を取得。
            Vector3 playerPosition =
                player.transform.position;

            // --------------------------------------------------------
            // XZ平面へ変換
            // --------------------------------------------------------
            // 上から見下ろすゲームなので、
            // Y方向（高さ）の差は当たり判定に使用しない。
            Vector2 attackXZ =
                new Vector2(
                    attackPosition.x,
                    attackPosition.z);

            Vector2 playerXZ =
                new Vector2(
                    playerPosition.x,
                    playerPosition.z);

            // 強攻撃中心からPlayerまでの距離を計算。
            float distance =
                Vector2.Distance(
                    attackXZ,
                    playerXZ);

            // Inspectorで設定した半径以内なら命中。
            // true  = 強攻撃範囲内
            // false = 強攻撃範囲外
            return distance <=
                strongAttackRadius;
        }
        // ============================================================
        // StrongAttack透明度変更
        // ============================================================
        private void SetStrongAttackAlpha(float alpha)
        {
            if (strongAttackRenderer == null)
            {
                return;
            }

            if (strongAttackProperties == null)
            {
                strongAttackProperties =
                    new MaterialPropertyBlock();
            }

            strongAttackRenderer.GetPropertyBlock(
                strongAttackProperties);

            Color color = Color.red;

            // 現在のマテリアル色を取得
            if (strongAttackRenderer.sharedMaterial != null)
            {
                if (strongAttackRenderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    color =
                        strongAttackRenderer.sharedMaterial.GetColor("_BaseColor");
                }
                else if (strongAttackRenderer.sharedMaterial.HasProperty("_Color"))
                {
                    color =
                        strongAttackRenderer.sharedMaterial.GetColor("_Color");
                }
            }

            color.a = Mathf.Clamp01(alpha);

            strongAttackProperties.SetColor(
                "_BaseColor",
                color);

            strongAttackProperties.SetColor(
                "_Color",
                color);

            strongAttackRenderer.SetPropertyBlock(
                strongAttackProperties);
        }

        // 蠑ｾ繧帝ｲ繧√※陦晉ｪ√ｒ隗｣豎ｺ縺吶ｋ縲ＱlayerFrom/To縺ｯ縺薙・譎る俣蛹ｺ髢薙・繝励Ξ繧､繝､繝ｼ遘ｻ蜍募燕・丞ｾ後・菴咲ｽｮ縲・
        public void TickBullets(Vector3 playerFrom, Vector3 playerTo, float dt)
        {
            if (IsHitStopped) return;
            // 騾壼ｸｸ縺ｮ陲ｫ蠑ｾ繧医ｊ蜈医↓繝代Μ繧｣繧貞愛螳壹☆繧九ょ女莉倅ｸｭ縺ｫ蠑ｷ縺・縺ｮ蠑ｾ縺ｸ謗･隗ｦ縺吶ｋ縺ｨ蜈ｨ蠑ｾ繧呈ｶ医☆縲・
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = 0; i < bullets.Count; i++) bullets[i].Simulate(dt);
            if (parryRemaining > 0f)
            {
                // bullet・壻ｸ隕ｧ縺九ｉ蜿悶ｊ蜃ｺ縺励◆縲∽ｻ雁屓蜃ｦ逅・☆繧句ｯｾ雎｡縲・
                foreach (var bullet in bullets)
                {
                    if (bullet.Power == 3 && Sweep(bullet.PreviousPosition - playerFrom,
                        bullet.transform.position - playerTo, bullet.Radius + DragPlayer.Radius * player.Parameters.ParryRange, out _))
                    {
                        ResolveParry(playerTo);
                        return;
                    }
                }
            }
            // 鬮倬滓兜謫ｲ縺瑚､・焚縺ｮ蠑ｾ繧呈ｨｪ蛻・ｋ蝣ｴ蜷医ｂ謗･隗ｦ鬆・↓蜃ｦ逅・☆繧九・
            // 譬ｼ荳翫・蠑ｾ縺ｧ繧ｭ繝･繝ｼ縺梧ｶ域ｻ・＠縺溷ｾ後√◎縺ｮ螂･縺ｮ蠑ｾ縺ｾ縺ｧ豸医＠縺ｦ縺励∪縺・％縺ｨ繧帝亟縺舌・
            if (balloon.CanHit && bullets.Count > 1) bullets.Sort(CompareCueContactOrder);
            // 蜑企勁縺ｧ繝ｪ繧ｹ繝医・豺ｻ蟄励′縺壹ｌ縺ｦ繧よ悴蜃ｦ逅・・蠑ｾ繧帝｣帙・縺輔↑縺・ｈ縺・∝ｾ後ｍ縺九ｉ隱ｿ縺ｹ繧九・
            // 蜷後§蛻､螳壼玄髢薙〒隍・焚縺ｮ蠑ｾ繧呈ｶ医＠縺ｦ繧ゅ∝､門・繧帝㍾縺ｭ謠上″縺励↑縺・◆繧√・蜊ｰ縲・
            bool emittedEraseEffect = false;
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                // 逕滓・縺ｾ縺溘・蛻､螳壼ｯｾ雎｡縺ｨ縺ｪ繧区雰蠑ｾ縲・
                var bullet = bullets[i];
                // 謨ｵ蠑ｾ縺後％縺ｮ遘ｻ蜍募玄髢薙〒繝励Ξ繧､繝､繝ｼ縺ｫ謗･隗ｦ縺吶ｋ縺九ＱlayerTime縺ｯ謗･隗ｦ譎らせ・・・・・峨・
                bool hitsPlayer = Sweep(bullet.PreviousPosition - playerFrom,
                    bullet.transform.position - playerTo, bullet.Radius + DragPlayer.Radius, out float playerTime);
                // 謨ｵ蠑ｾ縺梧ｰｴ鬚ｨ闊ｹ縺ｸ隗ｦ繧後ｋ譎らせ・・・・・峨よ悴謗･隗ｦ縺ｪ繧臥┌髯仙､ｧ縺ｮ縺ｾ縺ｾ縺ｫ縺吶ｋ縲・
                float ballTime = float.PositiveInfinity;
                // 蠑ｷ縺輔↓髢｢菫ゅ↑縺上く繝･繝ｼ縺ｸ縺ｮ謗･隗ｦ繧定ｪｿ縺ｹ繧九よ磁隗ｦ蠕後↓繝ｬ繝吶Ν縺ｮ螟ｧ蟆上〒邨先棡繧貞・縺代ｋ縲・
                bool hitsBall = balloon.CanHit && Sweep(
                    bullet.PreviousPosition - balloon.PreviousPosition,
                    bullet.transform.position - balloon.transform.position,
                    bullet.Radius + balloon.HitRadius, out ballTime);
                // 邏舌・蠑ｾ繧呈ｶ医＆縺ｪ縺・る｢ｨ闊ｹ譛ｬ菴薙′繝励Ξ繧､繝､繝ｼ繧医ｊ蜈医↓蠑ｾ縺ｸ蠖薙◆縺｣縺溘→縺阪□縺鷹亟縺舌・
                if (hitsBall && (!hitsPlayer || ballTime <= playerTime))
                {
                    if (balloon.Power >= bullet.Power)
                    {
                        // 蜷後Ξ繝吶Ν莉･荳九↑繧画雰蠑ｾ繧呈ｶ医＠縲√く繝･繝ｼ縺ｯ蠑ｷ縺輔→鬟幄｡鯉ｼ丞・霆｢繧堤ｶｭ謖√＠縺ｦ雋ｫ騾壹☆繧九・
                        if (!emittedEraseEffect)
                        {
                            EmitBulletEraseEffect();
                            emittedEraseEffect = true;
                        }
                        RemoveBullet(i);
                        BeginHitStop(bulletHitStop);
                        continue;
                    }
                    // 譬ｼ荳翫・謨ｵ蠑ｾ縺ｯ谿九ｊ縲√く繝･繝ｼ縺縺代′豸医∴繧九よ雰蠑ｾ縺ｮ繝励Ξ繧､繝､繝ｼ謗･隗ｦ蛻､螳壹・邯咏ｶ壹☆繧九・
                    balloon.Consume();
                }
                if (hitsPlayer)
                {
                    // Playerに当たった弾を削除
                    RemoveBullet(i);

                    // パリィなどによる無敵時間中ではない場合
                    if (invincible <= 0.0f)
                    {
                        DamagePlayer();
                    }

                    // GameOverになった場合は処理終了
                    if (isGameOver)
                    {
                        return;
                    }
                    continue;
                }
                if (Mathf.Abs(bullet.transform.position.x) > 6f || Mathf.Abs(bullet.transform.position.z) > 10f)
                    RemoveBullet(i);
            }
        }

        // 蠕後ｍ縺九ｉ蜑企勁縺吶ｋ繝ｫ繝ｼ繝励↓蜷医ｏ縺帙∵磁隗ｦ縺碁≦縺・ｼｾ縺九ｉ譌ｩ縺・ｼｾ縺ｮ鬆・∈荳ｦ縺ｹ繧九・
        private int CompareCueContactOrder(Bullet left, Bullet right)
        {
            return CueContactTime(right).CompareTo(CueContactTime(left));
        }

        // 繧ｭ繝･繝ｼ縺ｨbullet縺檎ｧｻ蜍募玄髢薙〒謗･隗ｦ縺吶ｋ譎らせ縲よ磁隗ｦ縺励↑縺・ｴ蜷医・譛蠕後↓蜃ｦ逅・☆繧九◆繧∫┌髯仙､ｧ繧定ｿ斐☆縲・
        private float CueContactTime(Bullet bullet)
        {
            // 謗･隗ｦ縺瑚ｵｷ縺阪ｋ譎らせ・亥玄髢薙・髢句ｧ・・樒ｵゆｺ・・峨ょｼｷ縺輔〒縺ｯ邨槭ｊ霎ｼ縺ｾ縺壽ｼ荳翫ｂ蜷ｫ繧√ｋ縲・
            bool hits = Sweep(bullet.PreviousPosition - balloon.PreviousPosition,
                bullet.transform.position - balloon.transform.position, bullet.Radius + balloon.HitRadius, out float time);
            return hits ? time : float.PositiveInfinity;
        }

        // 繝代Μ繧｣謌仙粥繧堤｢ｺ螳壹☆繧九りｷ晞屬繧・ｼｾ縺ｮ蠑ｷ縺輔ｒ蝠上ｏ縺壹∫ｮ｡逅・ｸｭ縺ｮ謨ｵ蠑ｾ繧貞叉蠎ｧ縺ｫ蜈ｨ豸亥悉縺吶ｋ縲・
        // 蜿嶺ｻ倥ｂ邨ゆｺ・＆縺帙∝酔縺俶兜謫ｲ縺ｧ繝代Μ繧｣蝗樊焚繧・ｼ泌・縺碁㍾隍・＠縺ｪ縺・ｈ縺・↓縺吶ｋ縲・
        private void ResolveParry(Vector3 position)
        {
            ParryCount++;
            
            // 繝代Μ繧｣謌仙粥縺ｮSE繧貞・逕溘☆繧九・
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySE("ParrySE");
            }
            parryRemaining = 0f;
            ClearBullets();
            EmitPulse(position, Color.cyan, 5f, DragPlayer.Radius * player.Parameters.ParryRange);
        }

        // 蠑ｾ繧呈遠縺｡豸医＠縺滉ｽ咲ｽｮ縺ｮ繧ｭ繝･繝ｼ螟門捉縺ｫ霈ｪ繧剃ｽ懊ｋ縲ょｼｷ蛹悶ｄ繝代ャ繧ｷ繝悶↓繧医ｋ蛻､螳壼濠蠕・ｂ蜿肴丐縺吶ｋ縲・
        private void EmitBulletEraseEffect()
        {
            if (bulletEraseDuration <= 0f) return;
            EmitPulse(balloon.transform.position, bulletEraseColor,
                balloon.HitRadius + Mathf.Max(0f, bulletEraseExpansion), balloon.HitRadius,
                bulletEraseDuration, Mathf.Max(0.001f, bulletEraseWidth), "Bullet erase outer ring");
        }

        // 謚墓憧荳ｭ縺ｮ鬚ｨ闊ｹ縺縺代・繧ｹ縺ｫ蠖薙◆繧九ょｼｱ轤ｹ縺ｨ閭ｴ菴薙・縺・■蜈医↓謗･隗ｦ縺励◆譁ｹ繧呈治逕ｨ縺吶ｋ縲・
        // 蠑ｱ轤ｹ縺ｪ繧峨く繝｣繝ｩ謾ｻ謦・鴨縺ｨ繝ｬ繝吶Ν縺ｫ蠢懊§縺溘ム繝｡繝ｼ繧ｸ縲∬Χ菴薙↑繧峨ム繝｡繝ｼ繧ｸ縺ｪ縺励〒鬚ｨ闊ｹ繧呈ｶ郁ｲｻ縺吶ｋ縲・
        public void CheckBossHit()
        {
            if (IsHitStopped) return;
            if (balloon.State != WaterBalloon.MotionState.Flying) return;
            // 豌ｴ鬚ｨ闊ｹ縺悟ｼｱ轤ｹ縺ｫ謗･隗ｦ縺吶ｋ縺九ＸeakTime縺ｯ遘ｻ蜍募玄髢灘・縺ｮ譛蛻昴・謗･隗ｦ譎らせ・・・・・峨・
            bool weakHit = Sweep(balloon.PreviousPosition - weakPoint.position,
                balloon.transform.position - weakPoint.position, balloon.HitRadius + 0.55f, out float weakTime);
            // 豌ｴ鬚ｨ闊ｹ縺瑚Χ菴薙↓謗･隗ｦ縺吶ｋ縺九ＣodyTime縺ｯ遘ｻ蜍募玄髢灘・縺ｮ譛蛻昴・謗･隗ｦ譎らせ・・・・・峨・
            bool bodyHit = Sweep(balloon.PreviousPosition - boss.position,
                balloon.transform.position - boss.position, balloon.HitRadius + 1.15f, out float bodyTime);
            if (weakHit && (!bodyHit || weakTime <= bodyTime))
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySE("BossWeakSE");
                }
                BossHealth = Mathf.Max(0f, BossHealth - balloon.CurrentDamage);
                EmitPulse(weakPoint.position, Color.yellow, 1.6f);
                StopOnBalloonImpact(weakPointHitStop);
                BeginCameraShake();
                Debug.Log($"Prototype: weak point hit. Boss HP {BossHealth:0}/{bossMaxHealth:0}", this);
                if (BossHealth <= 0f) EndRound(true);
            }
            else if (bodyHit)
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySE("BossDamageSE");
                }
                EmitPulse(balloon.transform.position, Color.gray, 0.7f);
                StopOnBalloonImpact(bodyHitStop);
            }
        }

        // 遘ｻ蜍募玄髢薙→逅・・陦晉ｪ√ｒ隱ｿ縺ｹ縲・ｫ倬溘↑蠑ｾ繧・ヵ繝ｪ繝・け縺ｮ縺吶ｊ謚懊￠繧帝亟縺舌・
        // from/to縺ｯ逶ｸ謇九°繧芽ｦ九◆逶ｸ蟇ｾ菴咲ｽｮ縲〉adius縺ｯ蜿梧婿縺ｮ蜊雁ｾ・・蜷郁ｨ医・
        // time縺ｯ譛蛻昴↓謗･隗ｦ縺吶ｋ譎らせ・・=蛹ｺ髢薙・髢句ｧ九・=邨ゆｺ・ｼ峨よ綾繧雁､縺荊rue縺ｮ縺ｨ縺阪↓菴ｿ縺・・
        // duration縺ｯ蛛懈ｭ｢縺輔○繧句ｮ滓凾髢薙・遘呈焚縲ょ酔譎ょ多荳ｭ縺ｧ蛛懈ｭ｢譎る俣縺檎ｩ阪∩荳翫′繧峨↑縺・ｈ縺・聞縺・婿繧剃ｽｿ縺・・
        private void BeginHitStop(float duration)
        {
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            hitStopRemaining = Mathf.Max(hitStopRemaining, duration);
        }

        // duration遘偵・蜻ｽ荳ｭ貍泌・繧帝幕蟋九☆繧九ょ●豁｢荳ｭ縺ｯ繧ｭ繝･繝ｼ繧呈ｮ九＠縲∝●豁｢邨ゆｺ・凾縺ｫ蜀咲函逕｣蠕・■縺ｸ遘ｻ縺吶・
        private void StopOnBalloonImpact(float duration)
        {
            BeginHitStop(duration);
            if (IsHitStopped) consumeBalloonAfterHitStop = true;
            else balloon.Consume();
        }

        // unscaledDeltaTime遘偵□縺大●豁｢譎る俣繧帝ｲ繧√ｋ縲ゅご繝ｼ繝譛ｬ菴薙・譎る俣蛟咲紫繧・ｸ譎ょ●豁｢繧貞､画峩縺励↑縺・・
        public void AdvanceHitStop(float unscaledDeltaTime)
        {
            if (!IsHitStopped) return;
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - Mathf.Max(0f, unscaledDeltaTime));
            if (!IsHitStopped) CompleteHitStop();
        }

        // 蛛懈ｭ｢繧堤ｵゆｺ・＠縲∽ｿ晉蕗縺励※縺・◆蜻ｽ荳ｭ貂医∩繧ｭ繝･繝ｼ縺ｮ豸郁ｲｻ繧・蝗槭□縺題｡後≧縲・
        private void CompleteHitStop()
        {
            hitStopRemaining = 0f;
            if (consumeBalloonAfterHitStop && balloon != null) balloon.Consume();
            consumeBalloonAfterHitStop = false;
        }

        // 辟｡蜉ｹ蛹悶・繧ｷ繝ｼ繝ｳ遘ｻ蜍輔〒蛛懈ｭ｢繧・兜謫ｲ莠育ｴ・ｒ謖√■雜翫＆縺ｪ縺・ｈ縺・↓縺吶ｋ縲・
        private void OnDisable()
        {
            StopCameraShake();
            CompleteHitStop();
            if (player != null) player.TryConsumeBufferedRelease(out _);
        }

        // 遘ｻ蜍募玄髢薙→逅・・陦晉ｪ√ｒ隱ｿ縺ｹ縲・ｫ倬溘↑蠑ｾ繧・ヵ繝ｪ繝・け縺ｮ縺吶ｊ謚懊￠繧帝亟縺舌・
        // from/to縺ｯ逶ｸ謇九°繧芽ｦ九◆逶ｸ蟇ｾ菴咲ｽｮ縲〉adius縺ｯ蜿梧婿縺ｮ蜊雁ｾ・・蜷郁ｨ医・
        // time縺ｯ譛蛻昴↓謗･隗ｦ縺吶ｋ譎らせ・・=蛹ｺ髢薙・髢句ｧ九・=邨ゆｺ・ｼ峨よ綾繧雁､縺荊rue縺ｮ縺ｨ縺阪↓菴ｿ縺・・
        public static bool Sweep(Vector3 from, Vector3 to, float radius, out float time)
        {
            time = 0f;
            // 髢句ｧ区凾轤ｹ縺ｧ縺吶〒縺ｫ驥阪↑縺｣縺ｦ縺・ｌ縺ｰ縲∵凾蛻ｻ0縺ｧ謗･隗ｦ縺励※縺・ｋ縲・
            float c = from.sqrMagnitude - radius * radius;
            if (c <= 0f) return true;
            // 逶ｸ謇九°繧芽ｦ九◆縲∫ｧｻ蜍募玄髢薙・蟋狗せ縺九ｉ邨らせ縺ｸ縺ｮ螟牙喧驥上・
            Vector3 delta = to - from;
            // 陦晉ｪ√・莠梧ｬ｡譁ｹ遞句ｼ上・菫よ焚縲らｧｻ蜍暮㍼縺ｮ髟ｷ縺輔・2荵励・
            float a = delta.sqrMagnitude;
            if (a < 0.000001f) return false;
            // 陦晉ｪ√・莠梧ｬ｡譁ｹ遞句ｼ上・菫よ焚縲ょｧ狗せ縺ｨ遘ｻ蜍墓婿蜷代・蜀・ｩ阪・
            float b = Vector3.Dot(from, delta);
            // 遘ｻ蜍輔☆繧狗せ縺檎帥髱｢縺ｸ蛻ｰ驕斐☆繧倶ｺ梧ｬ｡譁ｹ遞句ｼ上ｒ隗｣縺上ょ愛蛻･蠑上′雋縺ｪ繧画磁隗ｦ縺励↑縺・・
            float discriminant = b * b - a * c;
            if (discriminant < 0f) return false;
            time = (-b - Mathf.Sqrt(discriminant)) / a;
            return time >= 0f && time <= 1f;
        }

        // 蜍晄風繧堤｢ｺ螳壹＠縲∬牡縺ｨ蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝医〒邨先棡繧堤､ｺ縺励※谿九ｊ縺ｮ蠑ｾ繧堤援莉倥￠繧九・
        private void EndRound(bool cleared)
        {
            ResultScreen.Record(cleared, roundElapsed, currentStock, gameObject.scene.path);
            State = cleared ? RoundState.Cleared : RoundState.Failed;
            player.SetColor(cleared ? Color.green : Color.red);
            EmitPulse(cleared ? boss.position : player.transform.position, cleared ? Color.green : Color.red, 3f);
            if (cleared) boss.gameObject.SetActive(false);
            ClearBullets();
            Debug.Log(cleared ? "Prototype: CLEAR. Tap/click to restart." : "Prototype: HIT. Tap/click to retry.", this);
        }

        // Destroy縺ｯ繝輔Ξ繝ｼ繝譛ｫ蟆ｾ縺ｾ縺ｧ驕・ｻｶ縺吶ｋ縺溘ａ縲∝・縺ｫ髱櫁｡ｨ遉ｺ縺ｫ縺励※邂｡逅・Μ繧ｹ繝医°繧蛾勁縺上・
        private void RemoveBullet(int index)
        {
            bullets[index].gameObject.SetActive(false);
            Destroy(bullets[index].gameObject);
            bullets.RemoveAt(index);
        }
        // 繝ｪ繧ｹ繝域忰蟆ｾ縺九ｉ蜈ｨ蠑ｾ繧貞炎髯､縺吶ｋ縲ゅΜ繝医Λ繧､縲√ヱ繝ｪ繧｣縲√Λ繧ｦ繝ｳ繝臥ｵゆｺ・凾縺ｫ菴ｿ逕ｨ縺吶ｋ縲・
        private void ClearBullets()
        {
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = bullets.Count - 1; i >= 0; i--) RemoveBullet(i);
        }

        // position繧剃ｸｭ蠢・↓startRadius縺九ｉradius縺ｸ蠎・′繧句・繧偵‥uration遘帝俣陦ｨ遉ｺ縺吶ｋ縲・
        // width縺ｯ邱壹・螟ｪ縺輔‘ffectName縺ｯHierarchy縺ｧ貍泌・繧定ｭ伜挨縺吶ｋ蜷榊燕縲ょ酔譎り｡ｨ遉ｺ縺ｯ8蛟九∪縺ｧ縲・
        private void EmitPulse(Vector3 position, Color color, float radius, float startRadius = 0.2f,
            float duration = 0.45f, float width = 0.07f, string effectName = "Gameplay pulse")
        {
            // 荳企剞譎ゅ・蜿､縺・ｼｪ繧堤ｵゆｺ・＠縲∵眠縺励￥襍ｷ縺阪◆蠑ｾ豸医＠繧・ヱ繝ｪ繧｣縺ｮ貍泌・繧貞ｿ・★陦ｨ遉ｺ縺吶ｋ縲・
            if (pulses.Count >= 8)
            {
                pulses[0].line.gameObject.SetActive(false);
                Destroy(pulses[0].line.gameObject);
                pulses.RemoveAt(0);
            }
            // 蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝育畑縺ｫ菴懊ｋ荳譎ら噪縺ｪGameObject縲・
            var go = new GameObject(effectName);
            go.transform.SetParent(transform);
            go.transform.position = position;
            // 邱壹ｒ陦ｨ遉ｺ縺吶ｋLineRenderer縲・
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = effectMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = 0; i < 40; i++)
            {
                // 蜀・捉荳翫・驟咲ｽｮ縺ｫ菴ｿ逕ｨ縺吶ｋ隗貞ｺｦ・医Λ繧ｸ繧｢繝ｳ・峨・
                float angle = i * Mathf.PI * 2f / 40f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * startRadius);
            }
            pulses.Add(new Pulse { line = line, color = color, radius = radius,
                startRadius = startRadius, duration = Mathf.Max(0.001f, duration) });
        }

        // 蠑ｱ轤ｹ蜻ｽ荳ｭ縺ｮ迸ｬ髢薙↓謠ｺ繧後ｒ髢句ｧ九☆繧九ょ商縺・ｽ咲ｽｮ縺ｮ縺壹ｌ繧呈ｮ九＆縺壹・｣邯壼多荳ｭ縺ｧ縺ｯ貍泌・繧呈峩譁ｰ縺吶ｋ縲・
        private void BeginCameraShake()
        {
            StopCameraShake();
            if (gameCamera == null || cameraShakeStrength <= 0f || cameraShakeDuration <= 0f) return;
            IsCameraShaking = true;
            cameraShakeElapsed = 0f;
            AdvanceCameraShake(0f);
        }

        // unscaledDeltaTime遘偵□縺第昭繧後ｒ騾ｲ繧√ｋ縲ゅヲ繝・ヨ繧ｹ繝医ャ繝嶺ｸｭ繧よｸ幄｡ｰ縺励∫ｵゆｺ・凾縺ｯ蜈・・菴咲ｽｮ縺ｸ謌ｻ縺吶・
        public void AdvanceCameraShake(float unscaledDeltaTime)
        {
            RestoreCameraOffset();
            if (!IsCameraShaking || gameCamera == null) return;
            cameraShakeElapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (cameraShakeDuration <= 0f || cameraShakeElapsed >= cameraShakeDuration)
            {
                StopCameraShake();
                return;
            }
            // 貍泌・邨ら乢縺ｻ縺ｩ蟆上＆縺上☆繧区昭繧悟ｹ・ゅご繝ｼ繝蜀・・蛻､螳壻ｽ咲ｽｮ縺ｯ蜍輔°縺輔↑縺・・
            float amplitude = cameraShakeStrength * (1f - cameraShakeElapsed / cameraShakeDuration);
            // 譎る俣縺九ｉ豎ｺ縺ｾ繧区険蜍輔・菴咲嶌縲ゆｹｱ謨ｰ繧剃ｽｿ繧上★縲∵､懆ｨｼ譎ゅｂ蜷後§謠ｺ繧後ｒ蜀咲樟縺ｧ縺阪ｋ縲・
            float phase = cameraShakeElapsed * Mathf.PI * 2f * cameraShakeFrequency;
            cameraShakeOffset = (gameCamera.transform.right * Mathf.Cos(phase)
                + gameCamera.transform.up * Mathf.Cos(phase * 0.73f + 1f)) * amplitude;
            gameCamera.transform.position += cameraShakeOffset;
        }

        // 譛蠕後↓蜉縺医◆謠冗判逕ｨ縺ｮ縺壹ｌ縺縺代ｒ蜿悶ｊ髯､縺阪√き繝｡繝ｩ譛ｬ譚･縺ｮ菴咲ｽｮ繧剃ｿ晄戟縺吶ｋ縲・
        private void RestoreCameraOffset()
        {
            if (gameCamera != null) gameCamera.transform.position -= cameraShakeOffset;
            cameraShakeOffset = Vector3.zero;
        }

        // 繝ｪ繝医Λ繧､縲∫┌蜉ｹ蛹悶∵昭繧檎ｵゆｺ・〒蜻ｼ縺ｶ蠕檎援莉倥￠縲ゅき繝｡繝ｩ縺ｮ縺壹ｌ繧呈ｬ｡縺ｮ繝ｩ繧ｦ繝ｳ繝峨↓謖√■雜翫＆縺ｪ縺・・
        private void StopCameraShake()
        {
            RestoreCameraOffset();
            IsCameraShaking = false;
            cameraShakeElapsed = 0f;
        }
        // 蜀・ｒ謖・ｮ壹＠縺溯｡ｨ遉ｺ譎る俣縺ｧ諡｡螟ｧ繝ｻ證励￥縺励∝ｯｿ蜻ｽ繧定ｿ弱∴縺溘ｉ蜊ｳ蠎ｧ縺ｫ髱櫁｡ｨ遉ｺ縺ｫ縺励※蜑企勁縺吶ｋ縲・
        private void UpdatePulses(float dt)
        {
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                // 譖ｴ譁ｰ縺ｾ縺溘・蜑企勁蟇ｾ雎｡縺ｮ蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝・蛟句・縺ｮ繝・・繧ｿ縲・
                var pulse = pulses[i];
                pulse.age += dt;
                // 繧ｨ繝輔ぉ繧ｯ繝医・蟇ｿ蜻ｽ縺ｫ蟇ｾ縺吶ｋ邨碁℃蜑ｲ蜷医・縺ｫ縺ｪ繧九→蜑企勁縺吶ｋ縲・
                float t = pulse.age / pulse.duration;
                if (t >= 1f)
                {
                    pulse.line.gameObject.SetActive(false);
                    Destroy(pulse.line.gameObject);
                    pulses.RemoveAt(i);
                    continue;
                }
                // 蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝医・陦ｨ遉ｺ濶ｲ縲・
                Color color = pulse.color * (1f - t);
                color.a = 1f;
                pulse.line.startColor = pulse.line.endColor = color;
                // j・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
                for (int j = 0; j < 40; j++)
                {
                    // 蜀・捉荳翫・驟咲ｽｮ縺ｫ菴ｿ逕ｨ縺吶ｋ隗貞ｺｦ・医Λ繧ｸ繧｢繝ｳ・峨・
                    float angle = j * Mathf.PI * 2f / 40;
                    pulse.line.SetPosition(j, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Lerp(pulse.startRadius, pulse.radius, t));
                }
            }
        }
    }
}
