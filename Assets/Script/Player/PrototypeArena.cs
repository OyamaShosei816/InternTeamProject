using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KazumaPrototype
{
    // 繧ｲ繝ｼ繝騾ｲ陦後・蜿ｸ莉､蝪斐ょ・蜉帚・豌ｴ鬚ｨ闊ｹ縺ｮ遘ｻ蜍補・蠑ｾ縺ｮ陦晉ｪ≫・繝懊せ蛻､螳壹・鬆・↓譖ｴ譁ｰ縺吶ｋ縲・
    // 蠑ｾ蟷輔∵兜謫ｲ逶ｴ蠕後・辟｡謨ｵ・上ヱ繝ｪ繧｣縲∝享謨励√Μ繝医Λ繧､縲∝・蠖｢繧ｨ繝輔ぉ繧ｯ繝医ｂ縺薙％縺ｧ邂｡逅・☆繧九・
    public sealed class KazumaPrototypeArena : MonoBehaviour
    {
        // 繝励Ξ繧､荳ｭ繝ｻ繧ｯ繝ｪ繧｢繝ｻ螟ｱ謨励・3迥ｶ諷九らｵゆｺ・ｾ後・荳螳壽凾髢薙ｒ鄂ｮ縺・※謚ｼ縺礼峩縺吶→蜀埼幕縺ｧ縺阪ｋ縲・
        public enum RoundState { Playing, Cleared, Failed }
        // 画面表示と、タッチ位置からの移動計算に使用するカメラ。
        [Header("画面：ゲーム用カメラ")]
        [Tooltip("プレイ画面のCameraを設定します。")][SerializeField] private Camera gameCamera;

        // プレイヤーの入力と移動を管理するコンポーネント。
        [Header("操作対象：プレイヤー")]
        [Tooltip("シーン内のプレイヤーに付いているKazumaDragPlayerを設定します。")]
        [SerializeField] private KazumaDragPlayer player;

        // 水風船の移動と投擲を管理するコンポーネント。
        [Header("攻撃対象：水風船")]
        [Tooltip("シーン内の水風船に付いているKazumaWaterBalloonを設定します。")]
        [SerializeField] private KazumaWaterBalloon balloon;

        // 発射時に複製する敵弾のPrefab。
        [Header("敵の攻撃：弾Prefab")]
        [Tooltip("敵弾Prefabに付いているKazumaBulletを設定します。")]
        [SerializeField] private KazumaBullet bulletPrefab;

        // ボス本体との接触判定と、クリア時の非表示に使用する。
        [Header("敵の配置：ボス本体")]
        [Tooltip("シーン内のボス本体のTransformを設定します。")]
        [SerializeField] private Transform boss;

        // 投げた水風船が当たると、ボスへダメージを与える位置。
        [Header("敵の弱点：ダメージ判定位置")]
        [Tooltip("シーン内の弱点のTransformを設定します。")]
        [SerializeField] private Transform weakPoint;

        // 命中やパリィなどの円形エフェクトに使用する。
        [Header("演出：円形エフェクトの素材")]
        [Tooltip("エフェクトのLineRendererに使用するマテリアルを設定します。")]
        [SerializeField] private Material effectMaterial;

        [Header("難易度：ボスの最大HP")]
        [Tooltip("ラウンド開始時のボスのHPです。")]
        [SerializeField] private float bossMaxHealth = 150f;

        [Header("難易度：弾の発射間隔")]
        [Tooltip("弾を発射する間隔です。単位は秒です。最初の発射は開始から2.5秒後です。")]
        [SerializeField] private float shotInterval = 1.5f;

        [Header("救済：投げた直後の無敵時間")]
        [Tooltip("水風船を投げた直後の無敵時間です。単位は秒です。")]
        [SerializeField] private float releaseInvulnerability = 0.22f;

        [Header("パリィ：投擲後の受付時間")]
        [Tooltip("投擲後にパリィが成立する受付時間です。単位は秒です。")]
        [SerializeField] private float parryWindow = 0.12f;

        // ============================================================
        // Playerダメージ設定
        // ============================================================
        [Header("Player Damage Settings")]

        [Tooltip("被弾後の無敵時間")]
        [SerializeField] private float damageInvincibleTime = 2.0f;

        [Tooltip("被弾時の点滅間隔")]
        [SerializeField] private float blinkInterval = 0.1f;

        [Tooltip("残機表示。Stock1 → Stock2 → Stock3の順番で登録する")]
        [SerializeField] private GameObject[] stockObjects;

        // 現在残っているStock数
        private int currentStock;

        // 被弾による無敵時間
        private float damageInvincibleTimer;

        // 点滅用タイマー
        private float blinkTimer;

        // GameOver状態か
        private bool isGameOver;

        // 迴ｾ蝨ｨ縺ｮ騾ｲ陦檎憾諷九・
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
        private readonly List<KazumaBullet> bullets = new List<KazumaBullet>();
        // 陦ｨ遉ｺ荳ｭ縺ｮ蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝医・荳隕ｧ縲・
        private readonly List<Pulse> pulses = new List<Pulse>();
        // 逋ｺ蟆・∪縺ｧ縺ｮ谿九ｊ遘呈焚縲∫┌謨ｵ縺ｮ谿九ｊ遘呈焚縲√ヱ繝ｪ繧｣縺ｮ谿九ｊ遘呈焚縲∫ｵゆｺ・ｾ後・邨碁℃遘呈焚縲・
        private float shotTimer, // 谺｡縺ｮ逋ｺ蟆・∪縺ｧ縺ｮ谿九ｊ遘呈焚縲・
            invincible, // 辟｡謨ｵ縺ｮ谿九ｊ遘呈焚縲・
            parryRemaining, // 繝代Μ繧｣蜿嶺ｻ倥・谿九ｊ遘呈焚縲・
            endTimer; // 繝ｩ繧ｦ繝ｳ繝臥ｵゆｺ・ｾ後・邨碁℃遘呈焚縲・
        // 逋ｺ蟆・ｸ医∩蠑ｾ蟷輔・騾壹＠逡ｪ蜿ｷ縲ょｼｷ縺・竊・竊・縺ｮ蛻・ｊ譖ｿ縺医↓菴ｿ縺・・
        private int wave;
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
        }

        // Android縺ｧ縺ｯ邵ｦ逕ｻ髱｢縺ｫ蝗ｺ螳壹＠縲∵怙蛻昴・繝ｩ繧ｦ繝ｳ繝峨ｒ髢句ｧ九☆繧九・
        private void Start()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayBGM("Stage1BGM");
            }
                if (Application.platform == RuntimePlatform.Android) Screen.orientation = ScreenOrientation.Portrait;
            ResetRound();
        }

        // 谿九▲縺溷ｼｾ繝ｻ貍泌・繧堤援莉倥￠縲？P縲√ち繧､繝槭・縲√・繝ｬ繧､繝､繝ｼ縺ｨ鬚ｨ闊ｹ繧帝幕蟋狗憾諷九↓謌ｻ縺吶・
        public void ResetRound()
        {
            // ============================================================
            // PlayerのStockを初期化
            // ============================================================
            currentStock = stockObjects.Length;
            damageInvincibleTimer = 0.0f;
            blinkTimer = 0.0f;
            isGameOver = false;

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
            // 逕ｻ髱｢縺檎ｸｦ髟ｷ縺ｧ繧ょｷｦ蜿ｳ縺ｮ繝励Ξ繧､鬆伜沺繧堤｢ｺ菫昴☆繧九ｈ縺・√き繝｡繝ｩ縺ｮ陦ｨ遉ｺ遽・峇繧貞ｺ・￡繧九・
            gameCamera.orthographicSize = Mathf.Max(9f, 5f / Mathf.Max(gameCamera.aspect, 0.1f));
            // 蜃ｦ逅・誠縺｡蠕後↓荳蠎ｦ縺ｫ螟ｧ縺阪￥蜍輔￥縺ｮ繧帝∩縺代ｋ縺溘ａ縲・繝輔Ξ繝ｼ繝縺ｧ騾ｲ繧√ｋ譎る俣繧・.1遘偵∪縺ｧ縺ｫ縺吶ｋ縲・
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            UpdateDamageInvincibility(dt);
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
                // 邨ゆｺ・°繧・.75遘貞ｾ後∵眠縺励￥繧ｿ繝・・・上け繝ｪ繝・け縺吶ｋ縺ｨ蜀埼幕縺吶ｋ縲ゅΓ繝九Η繝ｼ謫堺ｽ懊・荳崎ｦ√・
                if (endTimer > 0.75f && player.PressedThisFrame) ResetRound();
                else player.transform.position = previousPlayerPosition;
                return;
            }
            if (player.ReleasedThisFrame && balloon.Launch(player.FlickVelocity))
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
                if (State != RoundState.Playing) break;
                CheckBossHit();
                invincible = Mathf.Max(0f, invincible - step);
                parryRemaining = Mathf.Max(0f, parryRemaining - step);
            }
            previousPlayerPosition = currentPlayerPosition;
            if (State != RoundState.Playing) return;

            // 被弾無敵中ではない場合だけ、既存の色処理を行う
            if (damageInvincibleTimer <= 0.0f && !isGameOver)
            {
                player.SetColor(
                    invincible > 0.0f
                        ? Color.white
                        : new Color(0.12f, 0.8f, 1.0f)
                );
            }

            shotTimer -= dt;
            if (shotTimer <= 0f)
            {
                FireWave();
                shotTimer = shotInterval;
            }
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

        // 謚墓憧謌仙粥譎ゅ↓辟｡謨ｵ縺ｨ繝代Μ繧｣縺ｮ蜿嶺ｻ倥ｒ蜷梧凾縺ｫ髢句ｧ九＠縲∫區縺・・縺ｧ蜷亥峙縺吶ｋ縲・
        public void BeginReleaseProtection()
        {
            invincible = releaseInvulnerability;
            parryRemaining = parryWindow;
            EmitPulse(player.transform.position, Color.white, 0.8f);
        }

        // 蠑ｷ縺・竊・竊・繧堤ｹｰ繧願ｿ斐☆謇・憾蠑ｾ蟷輔ら匱蟆・凾轤ｹ縺ｮ繝励Ξ繧､繝､繝ｼ譁ｹ蜷代ｒ迢吶≧縲・
        private void FireWave()
        {
            // 莉雁屓縺ｮ蠑ｾ蟷輔・蠑ｷ縺輔ら匱蟆・・縺溘・縺ｫ1竊・竊・繧堤ｹｰ繧願ｿ斐☆縲・
            int power = 1 + wave++ % 3;
            // 繝懊せ縺ｮ蠑ｱ轤ｹ繧医ｊ蟆代＠謇句燕縺ｫ鄂ｮ縺乗雰蠑ｾ縺ｮ逋ｺ蟆・ｽ咲ｽｮ縲・
            Vector3 origin = weakPoint.position + Vector3.back * 0.5f;
            // 逋ｺ蟆・ｽ咲ｽｮ縺九ｉ迴ｾ蝨ｨ縺ｮ繝励Ξ繧､繝､繝ｼ縺ｸ蜷代°縺・聞縺・縺ｮ譁ｹ蜷代・
            Vector3 direction = (player.transform.position - origin).normalized;
            // 荳蠎ｦ縺ｫ逋ｺ蟆・☆繧句ｼｾ縺ｮ謨ｰ縲ょｼｷ縺・縺ｪ繧・逋ｺ縲√◎繧御ｻ･螟悶・5逋ｺ縲・
            int count = power == 3 ? 3 : 5;
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = 0; i < count; i++)
            {
                // 謇・憾縺ｫ14蠎ｦ縺壹▽譁ｹ蜷代ｒ縺壹ｉ縺励∝ｼｷ縺輔↓蠢懊§縺滄溘＆繧剃ｸ弱∴縺溷ｼｾ縺ｮ騾溷ｺｦ縲・
                Vector3 velocity = Quaternion.AngleAxis((i - (count - 1) * 0.5f) * 14f, Vector3.up) * direction * (2.7f + power * 0.35f);
                SpawnBullet(origin, velocity, power);
            }
        }

        // 蠑ｾ繧堤函謌舌＠縺ｦ邂｡逅・Μ繧ｹ繝医∈逋ｻ骭ｲ縺吶ｋ縲りｲ闕ｷ蟇ｾ遲悶→縺励※96蛟九ｒ荳企剞縺ｨ縺励∬ｶ・∴縺溘ｉnull繧定ｿ斐☆縲・
        public KazumaBullet SpawnBullet(Vector3 position, Vector3 velocity, int power)
        {
            if (bullets.Count >= 96) return null;
            // 逕滓・縺ｾ縺溘・蛻､螳壼ｯｾ雎｡縺ｨ縺ｪ繧区雰蠑ｾ縲・
            var bullet = Instantiate(bulletPrefab, transform);
            bullet.Initialize(position, velocity, power);
            bullets.Add(bullet);
            return bullet;
        }

        // 蠑ｾ繧帝ｲ繧√※陦晉ｪ√ｒ隗｣豎ｺ縺吶ｋ縲ＱlayerFrom/To縺ｯ縺薙・譎る俣蛹ｺ髢薙・繝励Ξ繧､繝､繝ｼ遘ｻ蜍募燕・丞ｾ後・菴咲ｽｮ縲・
        public void TickBullets(Vector3 playerFrom, Vector3 playerTo, float dt)
        {
            // 騾壼ｸｸ縺ｮ陲ｫ蠑ｾ繧医ｊ蜈医↓繝代Μ繧｣繧貞愛螳壹☆繧九ょ女莉倅ｸｭ縺ｫ蠑ｷ縺・縺ｮ蠑ｾ縺ｸ謗･隗ｦ縺吶ｋ縺ｨ蜈ｨ蠑ｾ繧呈ｶ医☆縲・
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = 0; i < bullets.Count; i++) bullets[i].Simulate(dt);
            if (parryRemaining > 0f)
            {
                // bullet・壻ｸ隕ｧ縺九ｉ蜿悶ｊ蜃ｺ縺励◆縲∽ｻ雁屓蜃ｦ逅・☆繧句ｯｾ雎｡縲・
                foreach (var bullet in bullets)
                {
                    if (bullet.Power == 3 && Sweep(bullet.PreviousPosition - playerFrom,
                        bullet.transform.position - playerTo, bullet.Radius + KazumaDragPlayer.Radius * player.Parameters.ParryRange, out _))
                    {
                        ParryCount++;
                        if (SoundManager.Instance != null)
                        {
                            SoundManager.Instance.PlaySE("ParrySE");
                        }
                        ClearBullets();
                        parryRemaining = 0f;
                        EmitPulse(playerTo, Color.cyan, 5f);
                        return;
                    }
                }
            }
            // 蜑企勁縺ｧ繝ｪ繧ｹ繝医・豺ｻ蟄励′縺壹ｌ縺ｦ繧よ悴蜃ｦ逅・・蠑ｾ繧帝｣帙・縺輔↑縺・ｈ縺・∝ｾ後ｍ縺九ｉ隱ｿ縺ｹ繧九・
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                // 逕滓・縺ｾ縺溘・蛻､螳壼ｯｾ雎｡縺ｨ縺ｪ繧区雰蠑ｾ縲・
                var bullet = bullets[i];
                // 謨ｵ蠑ｾ縺後％縺ｮ遘ｻ蜍募玄髢薙〒繝励Ξ繧､繝､繝ｼ縺ｫ謗･隗ｦ縺吶ｋ縺九ＱlayerTime縺ｯ謗･隗ｦ譎らせ・・・・・峨・
                bool hitsPlayer = Sweep(bullet.PreviousPosition - playerFrom,
                    bullet.transform.position - playerTo, bullet.Radius + KazumaDragPlayer.Radius, out float playerTime);
                // 謨ｵ蠑ｾ縺梧ｰｴ鬚ｨ闊ｹ縺ｸ隗ｦ繧後ｋ譎らせ・・・・・峨よ悴謗･隗ｦ縺ｪ繧臥┌髯仙､ｧ縺ｮ縺ｾ縺ｾ縺ｫ縺吶ｋ縲・
                float ballTime = float.PositiveInfinity;
                // 豌ｴ鬚ｨ闊ｹ縺ｮ蠑ｷ縺輔′雜ｳ繧翫※縺翫ｊ縲∫ｧｻ蜍募玄髢薙〒謨ｵ蠑ｾ縺ｫ謗･隗ｦ縺吶ｋ縺九・
                bool hitsBall = balloon.CanHit && balloon.Power >= bullet.Power && Sweep(
                    bullet.PreviousPosition - balloon.PreviousPosition,
                    bullet.transform.position - balloon.transform.position,
                    bullet.Radius + balloon.HitRadius, out ballTime);
                // 邏舌・蠑ｾ繧呈ｶ医＆縺ｪ縺・る｢ｨ闊ｹ譛ｬ菴薙′繝励Ξ繧､繝､繝ｼ繧医ｊ蜈医↓蠑ｾ縺ｸ蠖薙◆縺｣縺溘→縺阪□縺鷹亟縺舌・
                if (hitsBall && (!hitsPlayer || ballTime <= playerTime))
                {
                    RemoveBullet(i);
                    continue;
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

        // 謚墓憧荳ｭ縺ｮ鬚ｨ闊ｹ縺縺代・繧ｹ縺ｫ蠖薙◆繧九ょｼｱ轤ｹ縺ｨ閭ｴ菴薙・縺・■蜈医↓謗･隗ｦ縺励◆譁ｹ繧呈治逕ｨ縺吶ｋ縲・
        // 蠑ｱ轤ｹ縺ｪ繧蛾溷ｺｦ縺ｫ蠢懊§縺溘ム繝｡繝ｼ繧ｸ縲∬Χ菴薙↑繧峨ム繝｡繝ｼ繧ｸ縺ｪ縺励〒鬚ｨ闊ｹ繧呈ｶ郁ｲｻ縺吶ｋ縲・
        public void CheckBossHit()
        {
            if (balloon.State != KazumaWaterBalloon.MotionState.Flying) return;
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
                balloon.Consume();
                Debug.Log($"Kazuma: weak point hit. Boss HP {BossHealth:0}/{bossMaxHealth:0}", this);
                if (BossHealth <= 0f) EndRound(true);
            }
            else if (bodyHit)
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySE("BossDamageSE");
                }
                EmitPulse(balloon.transform.position, Color.gray, 0.7f);
                balloon.Consume();
            }
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
            State = cleared ? RoundState.Cleared : RoundState.Failed;
            player.SetColor(cleared ? Color.green : Color.red);
            EmitPulse(cleared ? boss.position : player.transform.position, cleared ? Color.green : Color.red, 3f);
            if (cleared) boss.gameObject.SetActive(false);
            ClearBullets();
            Debug.Log(cleared ? "Kazuma: CLEAR. Tap/click to restart." : "Kazuma: HIT. Tap/click to retry.", this);
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

        // 謖・ｮ壻ｽ咲ｽｮ縺ｫ蠎・′繧句・繧剃ｽ懊ｋ縲Ｓadius縺ｯ譛邨ょ濠蠕・〒縲∝酔譎り｡ｨ遉ｺ縺ｯ8蛟九∪縺ｧ縲・
        private void EmitPulse(Vector3 position, Color color, float radius)
        {
            if (pulses.Count >= 8) return;
            // 蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝育畑縺ｫ菴懊ｋ荳譎ら噪縺ｪGameObject縲・
            var go = new GameObject("Gameplay pulse");
            go.transform.SetParent(transform);
            go.transform.position = position;
            // 邱壹ｒ陦ｨ遉ｺ縺吶ｋLineRenderer縲・
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = effectMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.widthMultiplier = 0.07f;
            line.startColor = line.endColor = color;
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = 0; i < 40; i++)
            {
                // 蜀・捉荳翫・驟咲ｽｮ縺ｫ菴ｿ逕ｨ縺吶ｋ隗貞ｺｦ・医Λ繧ｸ繧｢繝ｳ・峨・
                float angle = i * Mathf.PI * 2f / 40f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.2f);
            }
            pulses.Add(new Pulse { line = line, color = color, radius = radius });
        }
        // 蜀・ｒ0.45遘偵〒諡｡螟ｧ繝ｻ證励￥縺励∝ｯｿ蜻ｽ繧定ｿ弱∴縺溘ｉ蜑企勁縺吶ｋ縲・
        private void UpdatePulses(float dt)
        {
            // i・壹％縺ｮ郢ｰ繧願ｿ斐＠縺ｧ蜃ｦ逅・☆繧句ｯｾ雎｡縺ｮ逡ｪ蜿ｷ縲よ擅莉ｶ繧呈ｺ縺溘☆髢薙・・分縺ｫ譖ｴ譁ｰ縺吶ｋ縲・
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                // 譖ｴ譁ｰ縺ｾ縺溘・蜑企勁蟇ｾ雎｡縺ｮ蜀・ｽ｢繧ｨ繝輔ぉ繧ｯ繝・蛟句・縺ｮ繝・・繧ｿ縲・
                var pulse = pulses[i];
                pulse.age += dt;
                // 繧ｨ繝輔ぉ繧ｯ繝医・蟇ｿ蜻ｽ縺ｫ蟇ｾ縺吶ｋ邨碁℃蜑ｲ蜷医・縺ｫ縺ｪ繧九→蜑企勁縺吶ｋ縲・
                float t = pulse.age / 0.45f;
                if (t >= 1f)
                {
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
                    pulse.line.SetPosition(j, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Lerp(0.2f, pulse.radius, t));
                }
            }
        }
    }
}
