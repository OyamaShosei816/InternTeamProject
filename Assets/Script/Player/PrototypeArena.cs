using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Prototype
{
	// å…¥åŠ›ã€æ°´é¢¨èˆ¹ã®ç§»å‹•ã€å¼¾ã®è¡çªã€ãEã‚¹åˆ¤å®šã‚’é E•ªã«æ›´æ–°ã™ã‚‹ã€E
    // ã‚²ãƒ¼ãƒ é€²è¡Œã¨ãƒ’ãƒƒãƒˆã‚¹ãƒˆãƒƒãƒ—ã€ã‚µã‚¦ãƒ³ãƒ‰ãªã©ã‚’ç®¡çE™ã‚‹ã€E
    public sealed class PrototypeArena : MonoBehaviour
    {
        // ãƒ—ãƒ¬ã‚¤ä¸­ãƒ»ã‚¯ãƒªã‚¢ãƒ»å¤±æ•—ãE3çŠ¶æ…‹ã€‚çµ‚äºE¾ŒãEä¸€å®šæ™‚é–“ã‚’ç½®ãE¦æŠ¼ã—ç›´ã™ã¨å†é–‹ã§ãã‚‹ã€E
        public enum RoundState { Playing, Cleared, Failed }
        private float roundElapsed;
        [Header("‘€ì‘ÎÛFƒvƒŒƒCƒ„[")]
        [Tooltip("ƒV[ƒ““à‚ÌƒvƒŒƒCƒ„[‚ÌDragPlayer‚ğİ’è‚µ‚Ü‚·B")]
        [SerializeField] private DragPlayer player;

        // •\¦‚ÆAƒ^ƒbƒ`À•W‚Ì•ÏŠ·‚Ég—p‚·‚éƒJƒƒ‰B
        [Header("‰æ–ÊFƒQ[ƒ€—pƒJƒƒ‰")]
        [Tooltip("ƒQ[ƒ€‰æ–Ê‚ğ‰f‚·Camera‚ğİ’è‚µ‚Ü‚·B")]
        [SerializeField] private Camera gameCamera;

        // æ°´é¢¨èˆ¹ã®ç§»å‹•ãEæŠ•æ“²ã‚’ç®¡çE™ã‚‹ã‚³ãƒ³ãƒãEãƒãƒ³ãƒˆã€E
        [Header("UŒ‚‘ÎÛF…•—‘D")]
        [Tooltip("ƒV[ƒ““à‚Ì…•—‘D‚ÌWaterBalloon‚ğİ’è‚µ‚Ü‚·B")]
        [SerializeField] private WaterBalloon balloon;

        // ç™ºå°E™‚ã«è¤E£½ã™ã‚‹æ•µå¼¾ã®Prefabã€E
        [Header("“G‚ÌUŒ‚F’ePrefab")]
        [Tooltip("“G’ePrefab‚ÌBullet‚ğİ’è‚µ‚Ü‚·B")]
        [SerializeField] private Bullet bulletPrefab;

        // ãƒœã‚¹æœ¬ä½“ãEæ¥è§¦åˆ¤å®šã¨ã€ã‚¯ãƒªã‚¢æ™‚ãEéè¡¨ç¤ºã«ä½¿ç”¨ã™ã‚‹ã€E
        [Header("“G‚Ì”z’uFƒ{ƒX–{‘Ì")]
        [Tooltip("ƒV[ƒ““à‚Ìƒ{ƒX–{‘Ì‚ÌTransform‚ğİ’è‚µ‚Ü‚·B")]
        [SerializeField] private Transform boss;

        // “Š‚°‚½…•—‘D‚ª“–‚½‚é‚ÆAƒ{ƒX‚Öƒ_ƒ[ƒW‚ğ—^‚¦‚éˆÊ’uB
        [Header("“G‚Ìã“_Fƒ_ƒ[ƒW”»’èˆÊ’u")]
        [Tooltip("ƒV[ƒ““à‚Ìã“_‚ÌTransform‚ğİ’è‚µ‚Ü‚·B")]
        [SerializeField] private Transform weakPoint;

        // ============================================================
        // “G’e–‹§ŒäFBulletPatternQÆ
        // ============================================================
        // Boss‚ªg—p‚·‚é6í—Ş‚Ì’e–‹‚»‚Ì‚à‚Ì‚ÍBulletPattern‘¤‚ÅŠÇ—‚·‚éB
        // Arena‘¤‚Í‚±‚ÌQÆ‚ğ’Ê‚µ‚ÄA
        //
        // E‚Ç‚ÌPattern‚ğ”­Ë‚·‚é‚©
        // EŒ»İ‚Ü‚¾’e–‹‚ğ¶¬’†‚©
        // ============================================================
        [Header("“G‚ÌUŒ‚F’e–‹ƒpƒ^[ƒ“")]
        [Tooltip("6í—Ş‚Ì“G’e–‹‚ğŠÇ—‚µ‚Ä‚¢‚éBulletPattern‚ğİ’è‚µ‚Ü‚·B")]
        [SerializeField]
        private BulletPattern bulletPattern;

        // –½’†‚âƒpƒŠƒB‚È‚Ç‚Ì‰~Œ`ƒGƒtƒFƒNƒg‚Ég—p‚·‚éB
        [Header("‰‰oF‰~Œ`ƒGƒtƒFƒNƒg‚Ì‘fŞ")]
        [Tooltip("ƒGƒtƒFƒNƒg‚ÌLineRenderer‚Ég—p‚·‚éƒ}ƒeƒŠƒAƒ‹‚ğİ’è‚µ‚Ü‚·B")]
        [SerializeField] private Material effectMaterial;

        [Header("Enemy hit effects")]
        [SerializeField] private GameObject bodyHitPrefab;
        [SerializeField] private GameObject weakHitPrefab;

        [Header("“ïˆÕ“xFƒ{ƒX‚ÌÅ‘åHP")]
        [Tooltip("ƒ‰ƒEƒ“ƒhŠJn‚Ìƒ{ƒX‚ÌHP‚Å‚·B")]
        [SerializeField] private float bossMaxHealth = 150f;

        [Header("ƒ{ƒX‚ÌHPƒo[")]
        [SerializeField] private UnityEngine.UI.Slider bossHpSlider;

        [Header("“ïˆÕ“xF’e‚Ì”­ËŠÔŠu")]
        [Tooltip("’e‚ğ”­Ë‚·‚éŠÔŠu")]
        [SerializeField] private float shotInterval = 1.5f;

        [Header("ƒLƒ…[F–³“GŠÔ")]
        [Tooltip("ƒLƒ…[‚ğ“Š‚°‚½’¼Œã‚Ì–³“GŠÔ")]
        [SerializeField] private float releaseInvulnerability = 0.22f;

        [Header("ƒpƒŠƒBF“Š±Œã‚Ìó•tŠÔ")]
        [Tooltip("“Š±Œã‚ÉƒpƒŠƒB‚ª¬—§‚·‚éó•tŠÔ")]
        [SerializeField] private float parryWindow = 0.12f;

        // ============================================================
        // “GUŒ‚ƒ‹[ƒeƒBƒ“İ’è
        // ============================================================
        [Header("“GUŒ‚ƒ‹[ƒeƒBƒ“FŠeUŒ‚ƒpƒ^[ƒ“ŒãƒCƒ“ƒ^[ƒoƒ‹")]
        [Tooltip("Pattern1ƒCƒ“ƒ^[ƒoƒ‹")]
        [SerializeField, Min(0.0f)]
        private float pattern1Interval = 2.0f;

        [Tooltip("Pattern2ƒCƒ“ƒ^[ƒoƒ‹")]
        [SerializeField, Min(0.0f)]
        private float pattern2Interval = 2.0f;

        [Tooltip("Pattern3ƒCƒ“ƒ^[ƒoƒ‹")]
        [SerializeField, Min(0.0f)]
        private float pattern3Interval = 2.0f;

        [Tooltip("Pattern4ƒCƒ“ƒ^[ƒoƒ‹")]
        [SerializeField, Min(0.0f)]
        private float pattern4Interval = 2.0f;

        [Tooltip("Pattern5ƒCƒ“ƒ^[ƒoƒ‹")]
        [SerializeField, Min(0.0f)]
        private float pattern5Interval = 2.0f;

        [Tooltip("Pattern6ƒCƒ“ƒ^[ƒoƒ‹")]
        [SerializeField, Min(0.0f)]
        private float pattern6Interval = 2.0f;

        // ============================================================
        // “Gs“®‚Ìƒ‰ƒ“ƒ_ƒ€’Š‘Iİ’è
        // ============================================================
        // ‰æ‘œ‚Ìd—l:
        //
        // s“®1 : Pattern1 ¨ Pattern2 ¨ Pattern4
        // s“®2 : Pattern2 ¨ Pattern4
        // s“®3 : Pattern3
        // s“®4 : Pattern2 ¨ Pattern3
        //
        // weight‚ÍuŠm—¦‚»‚Ì‚à‚Ìv‚Å‚Í‚È‚­’Š‘I‚Ìd‚İB
        // —á‚¦‚Î‘S•”25‚È‚çA‚»‚ê‚¼‚ê25%‚É‚È‚éB
        // 10 / 20 / 30 / 40‚È‚ç
        // 10% / 20% / 30% / 40%‚É‚È‚éB
        // ============================================================
        [Header("“Gs“®F’Êí‚Ì’Š‘IŠm—¦iWeight‚È‚Ì‚Å‡Œv‚ª100“‚É‚·‚éj")]

        [Tooltip("s“®1FPattern1 ¨ Pattern2 ¨ Pattern4‚Ì’Š‘I")]
        [SerializeField, Min(0.0f)]
        private float action1Weight = 25.0f;

        [Tooltip("s“®2FPattern2 ¨ Pattern4‚Ì’Š‘I")]
        [SerializeField, Min(0.0f)]
        private float action2Weight = 25.0f;

        [Tooltip("s“®3FPattern3‚Ì’Š‘I")]
        [SerializeField, Min(0.0f)]
        private float action3Weight = 25.0f;

        [Tooltip("s“®4FPattern2 ¨ Pattern3‚Ì’Š‘I")]
        [SerializeField, Min(0.0f)]
        private float action4Weight = 25.0f;

        // ============================================================
        // HP”¼•ªˆÈ‰º‚Å‚Ì’Š‘I•â³
        // ============================================================

        [Header("“Gs“®FHP’á‰º‚Ì’Š‘I•â³")]

        [Tooltip("‚±‚ÌHPŠ„‡ˆÈ‰º‚É‚È‚é‚Æs“®3E4‚ÌŠm—¦‚ğã¸‚³‚¹‚é")]
        [SerializeField, Range(0.0f, 1.0f)]
        private float lowHpThreshold = 0.5f;

        [Tooltip("HP’á‰º‚Ìs“®3‚Ì’Š‘I”{—¦")]
        [SerializeField, Min(1.0f)]
        private float action3LowHpMultiplier = 2.0f;

        [Tooltip("HP’á‰º‚Ìs“®4‚Ì’Š‘I”{—¦")]
        [SerializeField, Min(1.0f)]
        private float action4LowHpMultiplier = 2.0f;

        // ============================================================
        // Pattern“¯”­“®İ’è
        // ============================================================
        [Header("“Gs“®FPattern“¯”­“®")]
        [Tooltip("‘I‚Î‚ê‚½Pattern‚Æ•ÊPattern‚ğ“¯”­“®‚·‚éŠm—¦")]
        [SerializeField, Range(0.0f, 100.0f)]
        private float simultaneousPatternChance = 20.0f;

        [Tooltip("“¯”­“®‚É’Ç‰Á‚³‚ê‚éPattern1‚Ì’Š‘IƒEƒFƒCƒg")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern1Weight = 10.0f;

        [Tooltip("“¯”­“®‚É’Ç‰Á‚³‚ê‚éPattern2‚Ì’Š‘IƒEƒFƒCƒg")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern2Weight = 10.0f;

        [Tooltip("“¯”­“®‚É’Ç‰Á‚³‚ê‚éPattern3‚Ì’Š‘IƒEƒFƒCƒg")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern3Weight = 10.0f;

        [Tooltip("“¯”­“®‚É’Ç‰Á‚³‚ê‚éPattern4‚Ì’Š‘IƒEƒFƒCƒg")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern4Weight = 10.0f;

        [Tooltip("“¯”­“®‚É’Ç‰Á‚³‚ê‚éPattern5‚Ì’Š‘IƒEƒFƒCƒg")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern5Weight = 10.0f;

        [Tooltip("“¯”­“®‚É’Ç‰Á‚³‚ê‚éPattern6‚Ì’Š‘IƒEƒFƒCƒg")]
        [SerializeField, Min(0.0f)]
        private float simultaneousPattern6Weight = 10.0f;

        [Header("‹­UŒ‚F’e–‹‚ª”–‚­‚È‚Á‚½”»’è")]
        [Tooltip("‰æ–Ê‚Éc‚Á‚Ä‚¢‚é’e–‹‚ª‚±‚Ì”ˆÈ‰º‚É‚È‚é‚Æ‹­UŒ‚ŠJn")]
        [SerializeField, Min(0)]
        private int strongAttackBulletThreshold = 5;

        [Tooltip("’e–‹‚©‚ç‚Ì‹­UŒ‚ƒCƒ“ƒ^[ƒoƒ‹")]
        [SerializeField, Min(0.0f)]
        private float strongAttackMaximumWait = 4.0f;

        [Header("‹­UŒ‚FAOEƒIƒuƒWƒFƒNƒg")]
        [Tooltip("Ô‚¢‹­UŒ‚”ÍˆÍ‚Æ‚µ‚Äg—p‚·‚éGameObject")]
        [SerializeField]
        private GameObject strongAttackObject;

        [Header("‹­UŒ‚F—\’›ŠÔ")]
        [Tooltip("AOE‚ª“§–¾‚©‚çŠ®‘S•\¦‚É‚È‚é‚Ü‚Å‚ÌŠÔ")]
        [SerializeField, Min(0.1f)]
        private float strongAttackChargeTime = 2.0f;

        [Header("‹­UŒ‚F“–‚½‚è”»’è")]
        [Tooltip("Ô‚¢‹­UŒ‚”ÍˆÍ‚Ì”¼Œa")]
        [SerializeField, Min(0.01f)]
        private float strongAttackRadius = 3.0f;

        [Header("‹­UŒ‚FUŒ‚Œã‚Ì”æ˜JŠÔ")]
        [Tooltip("‹­UŒ‚I—¹Œã‚ÌƒCƒ“ƒ^[ƒoƒ‹")]
        [SerializeField, Min(0.0f)]
        private float strongAttackFatigueTime = 3.0f;

        // ============================================================
        // “Gƒ‹[ƒeƒBƒ““à•”ó‘Ô
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
        // Œ»İÀs’†‚Ìus“®v‚Ìî•ñ
        // ============================================================
        // Œ»İ‘I‚Î‚ê‚Ä‚¢‚és“®‚ÌPattern”z—ñB
        // —áFs“®1‚È‚ç { 1, 2, 4 }
        private int[] currentActionPatterns;

        // ”z—ñ‚Ì‰½”Ô–Ú‚Ü‚ÅÀs‚µ‚½‚©B
        // —áF
        // 0 ¨ Pattern1
        // 1 ¨ Pattern2
        // 2 ¨ Pattern4
        private int currentActionIndex = 0;

        // ¡”­Ë‚µ‚½Pattern‚Ì”Ô†B
        // I—¹Œã‚ÌƒCƒ“ƒ^[ƒoƒ‹æ“¾‚Ég—p‚·‚éB
        private int lastMainPattern = 0;

        // s“®ŠÔ‘Ò‹@ƒ^ƒCƒ}[
        private float patternIntervalTimer;

        // ’e–‹‚ª”–‚­‚È‚é‚Ì‚ğ‘Ò‚Á‚Ä‚¢‚éŠÔ
        private float bulletClearWaitTimer;

        // ‹­UŒ‚—\’›ŠÔ
        private float strongAttackTimer;

        // ‹­UŒ‚Œã‚Ì”æ˜JŠÔ
        private float fatigueTimer;

        // StrongAttack‚ÌRenderer
        private Renderer strongAttackRenderer;

        // ƒ¿•ÏX—p
        private MaterialPropertyBlock strongAttackProperties;

        // ============================================================
        // Playerƒ_ƒ[ƒWİ’è
        // ============================================================
        [Header("Player Damage Settings")]

        [Tooltip("”í’eŒã‚Ì–³“GŠÔ‚Å‚·B’PˆÊ‚Í•b‚Å‚·B")]
        [SerializeField] private float damageInvincibleTime = 2.0f;

        [Tooltip("”í’e‚Ì“_–ÅŠÔŠu‚Å‚·B’PˆÊ‚Í•b‚Å‚·B")]
        [SerializeField] private float blinkInterval = 0.1f;

        [Tooltip("c‹@UI‚ğStock1AStock2AStock3‚Ì‡‚É“o˜^‚µ‚Ü‚·B")]
        [SerializeField] private GameObject[] stockObjects;

        // ç¾åœ¨ã®æ®‹æ©Ÿæ•°ã¨ã€è¢«å¼¾æ¼”åEã®çŠ¶æ…‹ã€E
        private int currentStock;
        private float damageInvincibleTimer;
        private float blinkTimer;
        private bool isGameOver;

        // æ®‹ã‚Šæ™‚é–“ãŒã‚ã‚‹é–“ã¯ã€ãƒ’ãƒEƒˆã‚¹ãƒˆãƒƒãƒ—ä¸­ã¨ã—ã¦æ‰±ãE€E
        // ã‚­ãƒ¥ãƒ¼ãŒå¼±ç‚¹ã«å½“ãŸã£ãŸç¬é–“ã«ã‚²ãƒ¼ãƒ é€²è¡Œã‚’æ­¢ã‚ã‚‹æ™‚é–“ã€‚å®Ÿæ™‚é–“ãEç§’æ•°ã§æŒE®šã™ã‚‹ã€E
        [Header("ã“_–½’†FƒqƒbƒgƒXƒgƒbƒv")]
        [Tooltip("’â~ŠÔ‚ğ•b‚Åw’è‚µ‚Ü‚·B0‚Å–³Œø‚Å‚·B")]
        [SerializeField, Range(0f, 0.3f)] private float weakPointHitStop = 0.15f;
        // å¼±ç‚¹å‘½ä¸­æ™‚ã«ã€ã‚«ãƒ¡ãƒ©ã‚’ç”»é¢ã®æ¨ªãƒ»ç¸¦æ–¹å‘ã¸æºã‚‰ã™æœ€å¤§è·é›¢ã€E
        [Header("ã“_–½’†FƒJƒƒ‰‚Ì—h‚ê•")]
        [Tooltip("—h‚ê‚Ì‘å‚«‚³‚Å‚·B0‚Å–³Œø‚É‚µ‚Ü‚·B")]
        [SerializeField, Min(0f)] private float cameraShakeStrength = 0.12f;
        // ãƒ’ãƒƒãƒˆã‚¹ãƒˆãƒƒãƒ—ä¸­ã‚‚å®Ÿæ™‚é–“ã§é€²ã‚€ã€ã‚«ãƒ¡ãƒ©ã‚·ã‚§ã‚¤ã‚¯ã®è¡¨ç¤ºæ™‚é–“ã€E
        [Header("ã“_–½’†FƒJƒƒ‰‚ª—h‚ê‚éŠÔ")]
        [Tooltip("—h‚ê‚Ì•\¦ŠÔ‚Å‚·B’PˆÊ‚Í•b‚Å‚·B")]
        [SerializeField, Min(0f)] private float cameraShakeDuration = 0.15f;
        // ã‚«ãƒ¡ãƒ©ã‚·ã‚§ã‚¤ã‚¯ã®1ç§’ã‚ãŸã‚Šã®æŒ¯å‹•å›æ•°ã€E
        [Header("ã“_–½’†FƒJƒƒ‰‚Ì—h‚ê‚é‘¬‚³")]
        [Tooltip("1•b‚ ‚½‚è‚ÌU“®‰ñ”‚Å‚·B")]
        [SerializeField, Min(1f)] private float cameraShakeFrequency = 35f;
        // ã‚«ãƒ¡ãƒ©ã‚·ã‚§ã‚¤ã‚¯ã‚’é–‹å§‹ã—ã¦ã‹ã‚‰ã®å®ŸçµŒéç§’æ•°ã€E
        private float cameraShakeElapsed;
        // å‰å›ã®æç”»ç”¨ã«ã‚«ãƒ¡ãƒ©ã¸åŠ ãˆãŸä½ç½®ã®ãšã‚Œã€‚åEåŠ›åˆ¤å®šå‰ã¨æ¼”åEçµ‚äºE™‚ã«å–ã‚Šé™¤ãã€E
        private Vector3 cameraShakeOffset;
        // å¼±ç‚¹å‘½ä¸­ã®ã‚·ã‚§ã‚¤ã‚¯ãŒé€²è¡Œä¸­ã‹ã€‚ã‚²ãƒ¼ãƒ ã®åœæ­¢ä¸­ã‚‚æºã‚ŒãEæ›´æ–°ã¯ç¶™ç¶šã™ã‚‹ã€E
        public bool IsCameraShaking { get; private set; }
        // ãƒ€ãƒ¡ãƒ¼ã‚¸ãŒåEã‚‰ãªãEƒ´ä½“ã¸ã®å‘½ä¸­ã«ä½¿ãE€çŸ­ã‚ãEåœæ­¢æ™‚é–“ã€E
        [Header("ƒqƒbƒgƒXƒgƒbƒvFƒ{ƒX–{‘Ì–½’†")]
        [Tooltip("–{‘Ì–½’†‚Ì’â~ŠÔ‚Å‚·B’PˆÊ‚Í•b‚Å‚·B")]
        [SerializeField, Range(0f, 0.3f)] private float bodyHitStop = 0.04f;
        // å…¬è»¢ä¸­ãƒ»æŠ•æ“²ä¸­ã®ã‚­ãƒ¥ãƒ¼ãŒæ•µå¼¾ã‚’æ¶ˆã—ãŸã¨ããEåœæ­¢æ™‚é–“ã€E
        [Header("ƒqƒbƒgƒXƒgƒbƒvF“G’eÁ‹")]
        [Tooltip("“G’e‚ğÁ‚µ‚½‚Æ‚«‚Ì’â~ŠÔ‚Å‚·B’PˆÊ‚Í•b‚Å‚·B")]
        [SerializeField, Range(0f, 0.3f)] private float bulletHitStop = 0.025f;
        // æ•µå¼¾ã‚’ã‚­ãƒ¥ãƒ¼ã§æ¶ˆã—ãŸéš›ã€ã‚­ãƒ¥ãƒ¼ã®å¤–å‘¨ã‹ã‚‰åºEŒã‚‹è¼ªã®è‰²ã€E
        [Header("’eÁ‚µ‰‰oF—Ö‚ÌF")]
        [Tooltip("…•—‘D‚ÌŠOü‚É•\¦‚·‚é—Ö‚ÌF‚Å‚·B")]
        [SerializeField] private Color bulletEraseColor = new Color(0.4f, 1f, 1f, 1f);
        // å½“ãŸã‚Šåˆ¤å®šãEå¤–å‘¨ã‹ã‚‰ã€è¼ªã®åŠå¾E‚’è¿½åŠ ã§åºE’ã‚‹è·é›¢ã€E
        [Header("’eÁ‚µ‰‰oF—Ö‚ªL‚ª‚é‹——£")]
        [Tooltip("“–‚½‚è”»’è‚ÌŠO‘¤‚ÖL‚ª‚é‹——£‚Å‚·B")]
        [SerializeField, Min(0f)] private float bulletEraseExpansion = 0.8f;
        // å¼¾æ¶ˆã—ã®è¼ªãŒåEç¾ã—ã¦ã‹ã‚‰æ¶ˆãˆã‚‹ã¾ã§ã®æ™‚é–“ã€‚ãƒ’ãƒEƒˆã‚¹ãƒˆãƒƒãƒ—ä¸­ã¯é€²ã‚ãªãE€E
        [Header("’eÁ‚µ‰‰oF—Ö‚Ì•\¦ŠÔ")]
        [Tooltip("•\¦ŠÔ‚ğ•b‚Åw’è‚µ‚Ü‚·B0‚Å–³Œø‚É‚µ‚Ü‚·B")]
        [SerializeField, Min(0f)] private float bulletEraseDuration = 0.3f;
        // å¼¾æ¶ˆã—ã®è¼ªã‚’æãç·šãEå¤ªã•ã€E
        [Header("’eÁ‚µ‰‰oF—Ö‚Ìü‚Ì‘¾‚³")]
        [Tooltip("—Ö‚ğ•`‚­ü‚Ì‘¾‚³‚Å‚·B")]
        [SerializeField, Min(0.001f)] private float bulletEraseWidth = 0.09f;
        // ç¾åœ¨ãƒ’ãƒƒãƒˆã‚¹ãƒˆãƒƒãƒ—ä¸­ã‹ã€‚åœæ­¢ä¸­ã¯ã‚²ãƒ¼ãƒ é€²è¡Œã¨è¿½åŠ ã®å‘½ä¸­åˆ¤å®šã‚’è¡Œã‚ãªãE€E
        public bool IsHitStopped => hitStopRemaining > 0f;
        private float hitStopRemaining;

        // å‘½ä¸­ã—ãŸæ°´é¢¨èˆ¹ã‚’ã€åœæ­¢çµ‚äºE¾Œã«æ¶ˆè²»ã™ã‚‹ãŸã‚ã®äºˆç´E€E
        private bool consumeBalloonAfterHitStop;

        // ç¾åœ¨ã®ã‚²ãƒ¼ãƒ é€²è¡ŒçŠ¶æ…‹ã€E
        public RoundState State { get; private set; }
        // ãƒœã‚¹ã®æ®‹ã‚ŠHPã€Eã«ãªã‚‹ã¨ã‚¯ãƒªã‚¢ã€E
        public float BossHealth { get; private set; }
        // ã“ãEãƒ©ã‚¦ãƒ³ãƒ‰ã§æˆåŠŸã—ãŸãƒ‘ãƒªã‚£ã®å›æ•°ã€E
        public int ParryCount { get; private set; }
        // ç¾åœ¨ç®¡çE—ã¦ãE‚‹æ•µå¼¾ã®æ•°ã€E
        public int ActiveBulletCount => bullets.Count;
        // ãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ä¸­å¿EEç§»å‹•ç¯E›²ã€‚Rectã®æ¨ªè»¸ã¯ãƒ¯ãƒ¼ãƒ«ãƒ‰Xã€ç¸¦è»¸ã¯ãƒ¯ãƒ¼ãƒ«ãƒ‰Zã«å¯¾å¿œã™ã‚‹ã€E
        public static Rect MovementBounds => Rect.MinMaxRect(-4.1f, -7.5f, 4.1f, 3.7f);
        // ç”»é¢å†E§æ›´æ–°ãƒ»åˆ¤å®šã™ã‚‹æ•µå¼¾ã®ä¸€è¦§ã€E
        private readonly List<Bullet> bullets = new List<Bullet>();
        // è¡¨ç¤ºä¸­ã®å†E½¢ã‚¨ãƒ•ã‚§ã‚¯ãƒˆãEä¸€è¦§ã€E
        private readonly List<Pulse> pulses = new List<Pulse>();
        // ç™ºå°E¾ã§ã®æ®‹ã‚Šç§’æ•°ã€ç„¡æ•µã®æ®‹ã‚Šç§’æ•°ã€ãƒ‘ãƒªã‚£ã®æ®‹ã‚Šç§’æ•°ã€çµ‚äºE¾ŒãEçµŒéç§’æ•°ã€E
        private float shotTimer, // æ¬¡ã®ç™ºå°E¾ã§ã®æ®‹ã‚Šç§’æ•°ã€E
            invincible, // ç„¡æ•µã®æ®‹ã‚Šç§’æ•°ã€E
            parryRemaining, // ãƒ‘ãƒªã‚£å—ä»˜ãEæ®‹ã‚Šç§’æ•°ã€E
            endTimer; // ãƒ©ã‚¦ãƒ³ãƒ‰çµ‚äºE¾ŒãEçµŒéç§’æ•°ã€E
        // ç™ºå°E¸ˆã¿å¼¾å¹•ãEé€šã—ç•ªå·ã€‚å¼·ãEâ†Eâ†Eã®åˆE‚Šæ›¿ãˆã«ä½¿ãE€E
        private int wave;

        // “¯‚É‘¶İ‚Å‚«‚é“G’e‚ÌÅ‘å”B
        private const int MaximumBulletCount = 200; // ’e–‹‚É‚æ‚éGameObject‚Ì–³§ŒÀ¶¬‚ğ–h~‚·‚éB

        // å‰ãƒ•ãƒ¬ãƒ¼ãƒ ã®ãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ä½ç½®ã€‚è¡çªè¨ˆç®—ç”¨ã®ç§»å‹•åŒºé–“ãEå§‹ç‚¹ã€E
        private Vector3 previousPlayerPosition;
        // ä¸€æ™‚çš„ãªå†E½¢ã‚¨ãƒ•ã‚§ã‚¯ãƒˆãEç·šã€çµŒéæ™‚é–“ã€è‰²ã€æœ€çµ‚åŠå¾E‚’ã¾ã¨ã‚ãŸãƒEEã‚¿ã€E
        private sealed class Pulse
        {
            // å†E‚’æç”»ã™ã‚‹ç·šã‚³ãƒ³ãƒãEãƒãƒ³ãƒˆã€E
            public LineRenderer line;
            // å‡ºç¾ã‹ã‚‰ã®çµŒéæ™‚é–“Eˆç§’ï¼‰ã€E
            public float age;
            // å‡ºç¾æ™‚ãEåŸºæœ¬è‰²ã€‚æ™‚é–“çµŒéã§æš—ãã™ã‚‹åŸºæº–ã€E
            public Color color;
            // æ‹¡å¤§ãŒçµ‚ã‚ã£ãŸã¨ããEåŠå¾E¼ˆãƒ¯ãƒ¼ãƒ«ãƒ‰å˜ä½ï¼‰ã€E
            public float radius;
            // å‡ºç¾æ™‚ãEåŠå¾E€‚å¼¾æ¶ˆã—ã§ã¯ã‚­ãƒ¥ãƒ¼ã®å½“ãŸã‚Šåˆ¤å®šãEå¤–å‘¨ã‹ã‚‰æãã€E
            public float startRadius;
            // å†EŒå®ŒåEã«æ¶ˆãˆã‚‹ã¾ã§ã®ç§’æ•°ã€E
            public float duration;
        }

        // Androidã§ã¯ç¸¦ç”»é¢ã«å›ºå®šã—ã€æœ€åˆãEãƒ©ã‚¦ãƒ³ãƒ‰ã‚’é–‹å§‹ã™ã‚‹ã€E
        private void Start()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayBGM("Stage1BGM");
            }
            if (Application.platform == RuntimePlatform.Android) Screen.orientation = ScreenOrientation.Portrait;

            // ============================================================
            // StrongAttack‰Šúİ’è
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
        // Ÿ‚ÉÀs‚·‚és“®‚ğƒ‰ƒ“ƒ_ƒ€’Š‘I
        // ============================================================
        private void SelectRandomAction()
        {
            // Boss‚ÌŒ»İHPŠ„‡B
            float hpRate =
                BossHealth /
                Mathf.Max(bossMaxHealth, 0.01f);

            // Inspector‚Åİ’è‚³‚ê‚½’ÊíƒEƒFƒCƒg‚ğæ“¾B
            float weight1 = action1Weight;
            float weight2 = action2Weight;
            float weight3 = action3Weight;
            float weight4 = action4Weight;

            // --------------------------------------------------------
            // HP‚ªw’èŠ„‡ˆÈ‰º‚È‚çs“®3E4‚ğ‘I‚Î‚ê‚â‚·‚­‚·‚é
            // --------------------------------------------------------
            if (hpRate <= lowHpThreshold)
            {
                weight3 *= action3LowHpMultiplier;
                weight4 *= action4LowHpMultiplier;
            }

            // ‘SƒEƒFƒCƒg‡ŒvB
            float totalWeight =
                weight1 +
                weight2 +
                weight3 +
                weight4;

            // ‘S•”0‚¾‚Æ’Š‘I‚Å‚«‚È‚¢‚½‚ßs“®1‚ğg—pB
            if (totalWeight <= 0.0f)
            {
                currentActionPatterns =
                    new int[] { 1, 2, 4 };

                currentActionIndex = 0;
                return;
            }

            // 0`‡Œv’l‚Ì‚Ç‚±‚©‚ğƒ‰ƒ“ƒ_ƒ€‘I‘ğB
            float randomValue =
                Random.Range(0.0f, totalWeight);

            // --------------------------------------------------------
            // s“®1
            // Pattern1 ¨ Pattern2 ¨ Pattern4
            // --------------------------------------------------------
            if (randomValue < weight1)
            {
                currentActionPatterns =
                    new int[] { 1, 2, 4 };

                Debug.Log(
                    "Enemy Action Selected : Action1 [1 ¨ 2 ¨ 4]");
            }

            // --------------------------------------------------------
            // s“®2
            // Pattern2 ¨ Pattern4
            // --------------------------------------------------------
            else if (randomValue < weight1 + weight2)
            {
                currentActionPatterns =
                    new int[] { 2, 4 };

                Debug.Log(
                    "Enemy Action Selected : Action2 [2 ¨ 4]");
            }

            // --------------------------------------------------------
            // s“®3
            // Pattern3‚Ì‚İ
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
            // s“®4
            // Pattern2 ¨ Pattern3
            // --------------------------------------------------------
            else
            {
                currentActionPatterns =
                    new int[] { 2, 3 };

                Debug.Log(
                    "Enemy Action Selected : Action4 [2 ¨ 3]");
            }

            // V‚µ‚¢s“®‚È‚Ì‚Åæ“ª‚©‚çŠJnB
            currentActionIndex = 0;
        }

        // æ®‹ã£ãŸå¼¾ãƒ»æ¼”åEã‚’ç‰‡ä»˜ã‘ã€HPã€ã‚¿ã‚¤ãƒãEã€ãEãƒ¬ã‚¤ãƒ¤ãƒ¼ã¨é¢¨èˆ¹ã‚’é–‹å§‹çŠ¶æ…‹ã«æˆ»ã™ã€E
        public void ResetRound()
        {
            // å‰ã®ãƒ©ã‚¦ãƒ³ãƒ‰ã§å†ç”Ÿã—ã¦ã„ãŸå¼¾æ¶ˆã—Prefabã‚’å…¨ã¦ç‰‡ä»˜ã‘ã‚‹ã€‚
            BulletEraseEffectPlayer eraseEffects = GetComponent<BulletEraseEffectPlayer>();
            if (eraseEffects != null) eraseEffects.Clear();
            roundElapsed = 0f;
            // ============================================================
            // Player‚ÌStock‚ğ‰Šú‰»
            // ============================================================
            currentStock = stockObjects.Length;
            damageInvincibleTimer = 0.0f;
            blinkTimer = 0.0f;
            isGameOver = false;

            // “Gs“®‚ğ‰Šúó‘Ô‚Ö–ß‚·B
            // Å‰‚ÌUŒ‚‚És“®1`4‚©‚çƒ‰ƒ“ƒ_ƒ€’Š‘I‚³‚ê‚éB
            currentActionPatterns = null;
            currentActionIndex = 0;
            lastMainPattern = 0;

            // Player‚ğ‘€ì‰Â”\ó‘Ô‚É–ß‚·
            player.SetCanMove(true);

            // Stock‚ğ‚·‚×‚Ä•\¦‚·‚é
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
            // pulseEšä¸€è¦§ã‹ã‚‰å–ã‚Šå‡ºã—ãŸã€ä»Šå›å‡¦çE™ã‚‹å¯¾è±¡ã€E
            foreach (var pulse in pulses)
            {
                if (pulse.line == null) continue;
                pulse.line.gameObject.SetActive(false);
                Destroy(pulse.line.gameObject);
            }
            pulses.Clear();
            State = RoundState.Playing;
            BossHealth = bossMaxHealth;
            // ƒ{ƒX‚ÌHP‚ğƒQ[ƒWUI‚É”½‰f
            UpdateBossHPUI();

            ParryCount = wave = 0;
            shotTimer = 2.5f;
            invincible = parryRemaining = endTimer = 0f;

            // ============================================================
            // “GUŒ‚ƒ‹[ƒeƒBƒ“‰Šú‰»
            // ============================================================
            enemyAttackState = EnemyAttackState.PatternInterval;

            // Å‰‚¾‚¯­‚µ‘Ò‚Á‚Ä‚©‚çUŒ‚ŠJn
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
            // ãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ã¨é¢¨èˆ¹ã§æ€§èƒ½ã‚’åEæœ‰ã™ã‚‹ã€‚ãƒªãƒˆãƒ©ã‚¤ã—ã¦ã‚‚è£E‚™ä¸­ã®ã‚¹ã‚­ãƒ«è£œæ­£ã¯ä¿æŒã™ã‚‹ã€E
            balloon.SetPlayerParameters(player.Parameters);
            balloon.ResetBalloon(player.transform.position);
        }

        // æ¯ãƒ•ãƒ¬ãƒ¼ãƒ ã®é€²è¡ŒåEçE€‚ãEãƒ¬ã‚¤ãƒ¤ãƒ¼å…¥åŠ›ãE1å›èª­ã¿ã€ç‰©çEš„ãªç§»å‹•ã¨è¡çªã ã‘ç´°åˆEŒ–ã™ã‚‹ã€E
        private void Update()
        {
            AdvanceFrame(Time.deltaTime, Time.unscaledDeltaTime);
        }

        // å…¥åŠ›ãEç§»å‹•ãEå‡¦çE¾Œã«æç”»ç”¨ã®æºã‚Œã‚’åŠ ãˆã‚‹ã€‚Time.timeScaleã«ã¯ä¾å­˜ã—ãªãE€E
        private void LateUpdate()
        {
            AdvanceCameraShake(Time.unscaledDeltaTime);
        }

        // deltaTimeã¯ã‚²ãƒ¼ãƒ å†EEçµŒéç§’æ•°ã€unscaledDeltaTimeã¯æ™‚é–“å€ç‡ã«ä¾å­˜ã—ãªãE®ŸçµŒéç§’æ•°ã€E
        // å®Ÿéš›ã®Updateã¨æ¤œè¨¼ã§åŒã˜é€²è¡ŒåEçE‚’ä½¿ãE€åœæ­¢ä¸­ã®ç§»å‹•ã‚„å¾©å¸°ã‚’ç¢ºèªã§ãã‚‹ã‚ˆã†ã«ã™ã‚‹ã€E
        public void AdvanceFrame(float deltaTime, float unscaledDeltaTime)
        {
            if (State == RoundState.Playing) roundElapsed += Mathf.Max(0f, deltaTime);
            // æºã‚Œã‚’ã‚¿ãƒEƒåº§æ¨™ãEå¤‰æ›ã«æ··ãœãªãE€‚é™æ­¢ã—ãŸæŒE§ãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ãŒå‹•ããEã‚’é˜²ãã€E
            RestoreCameraOffset();
            // åœæ­¢ä¸­ã‚‚Rã‚­ãƒ¼ã§å³åº§ã«ãƒªãƒˆãƒ©ã‚¤ã§ãã‚‹ã€E
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetRound();
                return;
            }
            if (IsHitStopped)
            {
                // åº§æ¨™ãEåŸºæº–ã ã‘æ›´æ–°ã—ã¦å¾©å¸°æ™‚ãEã‚¸ãƒ£ãƒ³ãƒ—ã‚’é˜²ãã€E›¢ã—ãŸå…¥åŠ›ãEãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼å´ã«ä¿å­˜ã™ã‚‹ã€E
                player.ReadInput(gameCamera, MovementBounds, unscaledDeltaTime, freezeMovement: true);
                AdvanceHitStop(unscaledDeltaTime);
                return;
            }
            // ç”»é¢ãŒç¸¦é•·ã§ã‚‚å·¦å³ã®ãƒ—ãƒ¬ã‚¤é ˜åŸŸã‚’ç¢ºä¿ã™ã‚‹ã‚ˆãE€ã‚«ãƒ¡ãƒ©ã®è¡¨ç¤ºç¯E›²ã‚’åºE’ã‚‹ã€E
            gameCamera.orthographicSize = Mathf.Max(9f, 5f / Mathf.Max(gameCamera.aspect, 0.1f));
            // å‡¦çE½ã¡å¾Œã«ä¸€åº¦ã«å¤§ããå‹•ãã®ã‚’é¿ã‘ã‚‹ãŸã‚ã€Eãƒ•ãƒ¬ãƒ¼ãƒ ã§é€²ã‚ã‚‹æ™‚é–“ã‚E.1ç§’ã¾ã§ã«ã™ã‚‹ã€E
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            
            // è¢«å¼¾å¾ŒãEç„¡æ•µæ™‚é–“ã¨ç‚¹æ»E‚’æ›´æ–°ã™ã‚‹ã€E
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
                // çµ‚äºE‹ã‚E.75ç§’å¾Œã€æ–°ã—ãã‚¿ãƒEEEã‚¯ãƒªãƒE‚¯ã™ã‚‹ã¨å†é–‹ã™ã‚‹ã€‚ãƒ¡ãƒ‹ãƒ¥ãƒ¼æ“ä½œãEä¸è¦ã€E
                if (endTimer > 0.75f && player.PressedThisFrame) ResetRound();
                else player.transform.position = previousPlayerPosition;
                return;
            }
            // bufferedReleaseã¯åœæ­¢ä¸­ã®æŠ•æ“²äºˆç´EEæœ‰ç„¡ã€bufferedFlickã¯ããEç¬é–“ãEé€Ÿåº¦ã€E
            // äºˆç´EŒã‚ã‚Œã°æœ€æ–°ã®å…¥åŠ›ã‚ˆã‚Šå„ªå…ˆã—ã€å¾©å¸°æ™‚ã«1å›ã ã‘æŠ•ã’ã‚‹ã€E
            bool bufferedRelease = player.TryConsumeBufferedRelease(out Vector3 bufferedFlick);
            if ((bufferedRelease || player.ReleasedThisFrame) && balloon.Launch(bufferedRelease ? bufferedFlick : player.FlickVelocity))
                BeginReleaseProtection();
            // ä»Šå›ã®å…¥åŠ›ã‚’åæ˜ ã—ãŸãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ã®åˆ°é”ä½ç½®ã€E
            Vector3 currentPlayerPosition = player.transform.position;
            // 1å›ãEæ›´æ–°ã‚’æœ€å¤§1/120ç§’ã«åˆE‰²ã—ã¦ã°ã­ã®è¨ˆç®—ã‚’å®‰å®šã•ã›ã‚‹ã€E
            // ãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ã®ç§»å‹•ã‚‚åŒºé–“ã”ã¨ã«è£œé–“ã—ã¦ã€ç§»å‹•ä¸­ã®è¡çªã‚’åˆ¤å®šã™ã‚‹ã€E
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
            // ç´°åˆEŒ–ã—ãŸ1å›åEã®ã‚·ãƒŸãƒ¥ãƒ¬ãƒ¼ã‚·ãƒ§ãƒ³æ™‚é–“Eˆç§’ï¼‰ã€E
            float step = dt / steps;
            // iEšã“ã®ç¹°ã‚Šè¿”ã—ã§å‡¦çE™ã‚‹å¯¾è±¡ã®ç•ªå·ã€‚æ¡ä»¶ã‚’æº€ãŸã™é–“ã€E E•ªã«æ›´æ–°ã™ã‚‹ã€E
            for (int i = 0; i < steps && State == RoundState.Playing; i++)
            {
                // ä»Šå›ã®ç´°åˆEŒ–åŒºé–“ãEå§‹ç‚¹ã¨ãªã‚‹ãEãƒ¬ã‚¤ãƒ¤ãƒ¼ä½ç½®ã€E
                Vector3 from = Vector3.Lerp(previousPlayerPosition, currentPlayerPosition, i / (float)steps);
                // ä»Šå›ã®ç´°åˆEŒ–åŒºé–“ãEçµ‚ç‚¹ã¨ãªã‚‹ãEãƒ¬ã‚¤ãƒ¤ãƒ¼ä½ç½®ã€E
                Vector3 to = Vector3.Lerp(previousPlayerPosition, currentPlayerPosition, (i + 1f) / steps);
                balloon.Simulate(to, player.Velocity, player.IsHeld, step);
                TickBullets(from, to, step);
                if (State != RoundState.Playing || IsHitStopped) break;
                CheckBossHit();
                // å‘½ä¸­ã—ãŸãƒ•ãƒ¬ãƒ¼ãƒ ã®æ®‹ã‚Šã®ç´°åˆEŒ–å‡¦çE‚‚æ­¢ã‚ã€ã‚­ãƒ¥ãƒ¼ãŒé€²ã¿ç¶šã‘ã‚‹ãEã‚’é˜²ãã€E
                if (IsHitStopped) break;
                invincible = Mathf.Max(0f, invincible - step);
                parryRemaining = Mathf.Max(0f, parryRemaining - step);
            }
            previousPlayerPosition = currentPlayerPosition;
            if (State != RoundState.Playing || IsHitStopped) return;

            // ”í’e–³“G’†‚Å‚Í‚È‚¢ê‡‚¾‚¯AŠù‘¶‚ÌFˆ—‚ğs‚¤
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
            // “GUŒ‚ƒ‹[ƒeƒBƒ“
            // ============================================================
            UpdateEnemyAttackRoutine(dt);
        }

        public Bullet SpawnBullet(
               Vector3 position,
               Vector3 velocity,
               int power)
        {
            // ’e–‹‚ª‘å—Ê‚É”­¶‚µ‚Ä‚àA
            // –³§ŒÀ‚ÉGameObject‚ğ¶¬‚µ‚È‚¢B
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
        // Player”í’eˆ—
        // ============================================================
        private void DamagePlayer()
        {
            // ‚·‚Å‚ÉGameOver‚È‚ç‰½‚à‚µ‚È‚¢
            if (isGameOver)
            {
                return;
            }

            // –³“GŠÔ’†‚È‚çƒ_ƒ[ƒW‚ğó‚¯‚È‚¢
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
            // Stock‚ªc‚Á‚Ä‚¢‚éê‡
            // ---------------------------------------------------------
            if (currentStock > 0)
            {
                // Stock‚ğ1‚ÂŒ¸‚ç‚·
                currentStock--;

                // Œ¸‚Á‚½Stock‚ÌUI‚ğ”ñ•\¦‚É‚·‚é
                if (currentStock < stockObjects.Length &&
                    stockObjects[currentStock] != null)
                {
                    stockObjects[currentStock].SetActive(false);
                }

                // ”í’eŒã‚Ì–³“GŠÔŠJn
                damageInvincibleTimer = damageInvincibleTime;

                // “_–Åƒ^ƒCƒ}[‚ğ‰Šú‰»
                blinkTimer = 0.0f;

                Debug.Log("Player Hit! Remaining Stock : " + currentStock);

                return;
            }

            // ---------------------------------------------------------
            // Stock‚ª0‚Ìó‘Ô‚Å‚³‚ç‚É”í’e‚µ‚½ê‡
            // ---------------------------------------------------------
            //GameOver();
        }

        // ============================================================
        // ”í’eŒã‚Ì–³“GŠÔE“_–Åˆ—
        // ============================================================
        private void UpdateDamageInvincibility(float dt)
        {
            // –³“GŠÔ‚ªI—¹‚µ‚Ä‚¢‚éê‡
            if (damageInvincibleTimer <= 0.0f)
            {
                // ’ÊíF‚Ö–ß‚·
                player.SetColor(new Color(0.12f, 0.8f, 1.0f));
                return;
            }

            // –³“GŠÔ‚ğŒ¸‚ç‚·
            damageInvincibleTimer -= dt;

            // “_–Åƒ^ƒCƒ}[‚ği‚ß‚é
            blinkTimer += dt;

            // ˆê’èŠÔ‚²‚Æ‚ÉF‚ğØ‚è‘Ö‚¦‚é
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0.0f;

                // c‚èŠÔ‚©‚ç”’‚Æ’ÊíF‚ğŒğŒİ‚ÉØ‚è‘Ö‚¦‚é
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

            // –³“GŠÔI—¹
            if (damageInvincibleTimer <= 0.0f)
            {
                damageInvincibleTimer = 0.0f;
                player.SetColor(new Color(0.12f, 0.8f, 1.0f));
            }
        }

        // ============================================================
        // Pattern”­Ë
        // ============================================================
        // ƒƒCƒ“Pattern‚ğ•K‚¸”­Ë‚·‚éB
        // ‚³‚ç‚ÉInspector‚Åİ’è‚µ‚½Šm—¦‚É¬Œ÷‚µ‚½ê‡A
        // •Ê‚ÌPattern‚ğ1í—Ş’Ç‰Á‚µ‚Ä“¯”­Ë‚·‚éB
        // ============================================================
        private void FireEnemyPattern(
            int mainPattern)
        {
            // ƒƒCƒ“Pattern”­ËB
            bulletPattern.FirePattern(
                mainPattern);

            lastMainPattern =
                mainPattern;

            // --------------------------------------------------------
            // “¯”­“®‚·‚é‚©’Š‘I
            // --------------------------------------------------------
            float simultaneousRoll =
                Random.Range(0.0f, 100.0f);

            // ’Š‘I¸”s‚È‚çƒƒCƒ“Pattern‚¾‚¯B
            if (simultaneousRoll >
                simultaneousPatternChance)
            {
                return;
            }

            // ’Ç‰ÁPattern‚ğ’Š‘IB
            int extraPattern =
                SelectSimultaneousPattern(
                    mainPattern);

            // —LŒø‚ÈPattern‚ª‚È‚¯‚ê‚ÎI—¹B
            if (extraPattern <= 0)
            {
                return;
            }

            // --------------------------------------------------------
            // ’Ç‰ÁPattern‚ğ“¯”­Ë
            // --------------------------------------------------------
            bulletPattern.FirePattern(
                extraPattern);

            Debug.Log(
                $"Simultaneous Pattern : " +
                $"{mainPattern} + {extraPattern}");
        }

        // ============================================================
        // “¯”­“®‚·‚é’Ç‰ÁPattern‚ğ’Š‘I
        // ============================================================
        // mainPattern‚Æ“¯‚¶Pattern‚ÍŒó•â‚©‚çœŠO‚·‚éB
        // ŠePattern‚ÌoŒ»‚µ‚â‚·‚³‚ÍInspector‚©‚ç’²®‰Â”\B
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

            // ƒƒCƒ“‚Æ“¯‚¶Pattern‚Í“¯”­“®‚³‚¹‚È‚¢B
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

            // Œó•â‚ª‘S•”0‚È‚ç’Ç‰ÁPattern‚È‚µB
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
                    // ”z—ñ0`5‚ğPattern1`6‚Ö•ÏŠ·B
                    return i + 1;
                }
            }

            return 0;
        }

        // æŠ•æ“²æˆåŠŸæ™‚ã«ç„¡æ•µã¨ãƒ‘ãƒªã‚£ã®å—ä»˜ã‚’åŒæ™‚ã«é–‹å§‹ã—ã€ç™½ãEEã§åˆå›³ã™ã‚‹ã€E
        public void BeginReleaseProtection()
        {
            invincible = releaseInvulnerability;
            parryRemaining = parryWindow;
            EmitPulse(player.transform.position, Color.white, 0.8f);
        }

        // ============================================================
        // “GUŒ‚ƒ‹[ƒeƒBƒ“XV
        // ============================================================
        // Boss‚ÌUŒ‚‚ğˆÈ‰º‚Ì‡”Ô‚Åis‚³‚¹‚éB
        //
        // Pattern1
        //   «
        // ‘Ò‹@
        //   «
        // Pattern2
        //   «
        // ‘Ò‹@
        //   «
        // Pattern3
        //   «
        // ‘Ò‹@
        //   «
        // Pattern4
        //   «
        // ‘Ò‹@
        //   «
        // Pattern5
        //   «
        // ‘Ò‹@
        //   «
        // Pattern6
        //   «
        // ‘Ò‹@
        //   «
        // ‰æ–Êã‚Ì“G’e‚ª­‚È‚­‚È‚é‚Ü‚Å‘Ò‹@
        //   «
        // ‹­UŒ‚—\’›
        //   «
        // ‹­UŒ‚”»’è
        //   «
        // ”æ˜JŠÔ
        //   «
        // Pattern1‚Ö–ß‚é
        //
        // ÀÛ‚Ì’e¶¬‚ÍBulletPattern‚ª’S“–‚µA
        // PrototypeArenauUŒ‚‘S‘Ì‚Ìisv
        // ============================================================
        private void UpdateEnemyAttackRoutine(float dt)
        {
            // BulletPattern‚ªInspector‚Åİ’è‚³‚ê‚Ä‚¢‚È‚¯‚ê‚Î
            // ’e–‹‚ğ”­Ë‚Å‚«‚È‚¢‚Ì‚Åˆ—‚ğI—¹‚·‚éB
            if (bulletPattern == null)
            {
                return;
            }

            // Œ»İ‚ÌBossUŒ‚ó‘Ô‚É‚æ‚Á‚Äˆ—‚ğØ‚è‘Ö‚¦‚éB
            switch (enemyAttackState)
            {
                // ========================================================
                // ‡@ Pattern”­Ë’†
                // ========================================================
                case EnemyAttackState.PatternFiring:
                    {
                        // “¯”­“®‚µ‚½Pattern‚ğŠÜ‚ßA
                        // ‘SCoroutine‚ªI—¹‚·‚é‚Ü‚Å‘Ò‚ÂB
                        if (bulletPattern.IsFiring)
                        {
                            return;
                        }

                        // ¡”­Ë‚µ‚½ƒƒCƒ“Pattern‚É‘Î‰‚µ‚½
                        // Inspectorİ’è‚ÌƒCƒ“ƒ^[ƒoƒ‹‚ğæ“¾B
                        patternIntervalTimer =
                            GetPatternInterval(
                                lastMainPattern);

                        // Œ»İ‚Ìs“®“à‚ÅŸ‚ÌPattern‚Öi‚ŞB
                        currentActionIndex++;

                        enemyAttackState =
                            EnemyAttackState.PatternInterval;

                        break;
                    }

                // ========================================================
                // ‡A PatternŠÔƒCƒ“ƒ^[ƒoƒ‹
                // ========================================================
                case EnemyAttackState.PatternInterval:
                    {
                        patternIntervalTimer -= dt;

                        // ‚Ü‚¾‘Ò‹@’†B
                        if (patternIntervalTimer > 0.0f)
                        {
                            return;
                        }

                        // ----------------------------------------------------
                        // s“®‚ª‚Ü‚¾‘I‚Î‚ê‚Ä‚¢‚È‚¢ê‡
                        // ----------------------------------------------------
                        if (currentActionPatterns == null)
                        {
                            SelectRandomAction();
                        }

                        // ----------------------------------------------------
                        // Œ»İ‚Ìs“®‚ÉŠÜ‚Ü‚ê‚éPattern‚ğ‘S•”I—¹
                        // ----------------------------------------------------
                        if (currentActionIndex >=
                            currentActionPatterns.Length)
                        {
                            // ¡‰ñ‚Ìs“®I—¹B
                            currentActionPatterns = null;

                            // ‹­UŒ‚‘O‚Ì’eŒ¸­‘Ò‹@‚ÖB
                            bulletClearWaitTimer = 0.0f;

                            enemyAttackState =
                                EnemyAttackState.WaitingForBulletClear;

                            return;
                        }

                        // ----------------------------------------------------
                        // s“®“à‚ÌŸ‚ÌPattern‚ğæ“¾
                        // ----------------------------------------------------
                        int nextPattern =
                            currentActionPatterns[
                                currentActionIndex];

                        // Pattern”­ËB
                        // ‚±‚Ì’†‚Å“¯”­“®’Š‘I‚às‚¤B
                        FireEnemyPattern(
                            nextPattern);

                        enemyAttackState =
                            EnemyAttackState.PatternFiring;

                        break;
                    }

                // ========================================================
                // ‡B Pattern6I—¹Œã
                //    ‰æ–Êã‚Ì“G’e‚ª­‚È‚­‚È‚é‚Ü‚Å‘Ò‹@
                // ========================================================
                case EnemyAttackState.WaitingForBulletClear:
                    {
                        // ‚±‚Ìó‘Ô‚É‚È‚Á‚Ä‚©‚ç‚ÌŒo‰ßŠÔ‚ğ‰ÁZB
                        bulletClearWaitTimer += dt;

                        // ----------------------------------------------------
                        // ğŒAF
                        // c‚Á‚Ä‚¢‚é“G’e‚ªw’è”ˆÈ‰º‚É‚È‚Á‚½
                        // ----------------------------------------------------
                        bool enoughBulletsCleared =
                            bullets.Count <=
                            strongAttackBulletThreshold;

                        // ----------------------------------------------------
                        // ğŒBF
                        // Å‘å‘Ò‹@ŠÔ‚ğ’´‚¦‚½
                        // ----------------------------------------------------
                        //
                        // ’e‚ª‰½‚ç‚©‚Ì——R‚ÅŒ¸‚ç‚È‚¢ê‡‚Å‚àA
                        // Boss‚ª‰i‹v‚É~‚Ü‚ç‚È‚¢‚½‚ß‚Ì•ÛŒ¯B
                        bool waitedTooLong =
                            bulletClearWaitTimer >=
                            strongAttackMaximumWait;

                        // AEB‚Ç‚¿‚ç‚à–‚½‚µ‚Ä‚¢‚È‚¢ê‡‚Í
                        // ‚Ü‚¾‹­UŒ‚‚ğŠJn‚µ‚È‚¢B
                        if (!enoughBulletsCleared &&
                            !waitedTooLong)
                        {
                            return;
                        }

                        // ’e‚ª\•ªŒ¸‚Á‚½A‚Ü‚½‚ÍÅ‘å‘Ò‹@ŠÔ‚ğ’´‚¦‚½‚Ì‚Å
                        // ‹­UŒ‚—\’›‚ğŠJnB
                        BeginStrongAttack();

                        break;
                    }


                // ========================================================
                // ‡C ‹­UŒ‚‚Ì—\’›’†
                // ========================================================
                case EnemyAttackState.StrongCharging:
                    {
                        // ‹­UŒ‚—\’›‚ÌŒo‰ßŠÔB
                        strongAttackTimer += dt;

                        // ----------------------------------------------------
                        // —\’›‚Ìis“x‚ğ0`1‚Ö•ÏŠ·
                        // ----------------------------------------------------
                        //
                        // —áF
                        // strongAttackChargeTime = 2•b
                        //
                        // 0•b ¨ 0.0
                        // 1•b ¨ 0.5
                        // 2•b ¨ 1.0
                        //
                        float progress =
                            Mathf.Clamp01(
                                strongAttackTimer /
                                Mathf.Max(
                                    strongAttackChargeTime,
                                    0.01f));

                        // Ô‚¢‹­UŒ‚”ÍˆÍ‚ğ
                        // “§–¾ ¨ Š®‘S•\¦‚Ö™X‚É•Ï‰»‚³‚¹‚éB
                        SetStrongAttackAlpha(
                            progress);

                        // ‚Ü‚¾100%‚É‚È‚Á‚Ä‚¢‚È‚¯‚ê‚Î
                        // ‹­UŒ‚‚Í”­“®‚µ‚È‚¢B
                        if (progress < 1.0f)
                        {
                            return;
                        }

                        // 100%‚É‚È‚Á‚½uŠÔ‚É‹­UŒ‚”»’èB
                        ExecuteStrongAttack();

                        break;
                    }


                // ========================================================
                // ‡D ‹­UŒ‚I—¹Œã‚Ì”æ˜Jó‘Ô
                // ========================================================
                case EnemyAttackState.Fatigue:
                    {
                        // –ˆƒtƒŒ[ƒ€”æ˜JŠÔ‚ğŒ¸‚ç‚·B
                        fatigueTimer -= dt;

                        // ”æ˜JŠÔ‚ªc‚Á‚Ä‚¢‚éŠÔ‚ÍBoss‚Í‰½‚à‚µ‚È‚¢B
                        if (fatigueTimer > 0.0f)
                        {
                            return;
                        }

                        // ========================================================
                        // UŒ‚ƒ‹[ƒeƒBƒ“1üI—¹
                        // ‹­UŒ‚ŒãAŸ‚Ìs“®‚ğV‚µ‚­ƒ‰ƒ“ƒ_ƒ€’Š‘I‚·‚é
                        // ========================================================
                        // ‘O‰ñ‚Ìs“®‚ğ”jŠüB
                        currentActionPatterns = null;

                        // ”z—ñˆÊ’u‚ğ‰Šú‰»B
                        currentActionIndex = 0;

                        // ‘O‰ñPatternî•ñ‚àƒŠƒZƒbƒgB
                        lastMainPattern = 0;

                        // ŸƒtƒŒ[ƒ€‚©‚çV‚µ‚¢s“®‚ğ’Š‘IB
                        patternIntervalTimer = 0.0f;

                        enemyAttackState =
                            EnemyAttackState.PatternInterval;

                        break;
                        
                    }
            }
        }

        // æ”»æ’Eƒ‘ã‚¿ãƒ¼ãƒ³çµ‚äºE¾ŒãEå¾E¡æ™‚é–“ã‚’è¿”ã™ã€E
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
        // ‹­UŒ‚ŠJn
        // ============================================================
        private void BeginStrongAttack()
        {
            enemyAttackState =
                EnemyAttackState.StrongCharging;

            strongAttackTimer = 0.0f;

            // ’ˆÓF
            // ‚±‚±‚Å‚ÍClearBullets‚µ‚È‚¢B
            // c‚Á‚Ä‚¢‚é’e‚Í‚»‚Ì‚Ü‚Ü‰æ–ÊŠO‚Ö—¬‚·B

            if (strongAttackObject != null)
            {
                strongAttackObject.SetActive(true);

                SetStrongAttackAlpha(0.0f);
            }

            Debug.Log( "Strong Attack Start! Remaining Bullets : " + bullets.Count);
        }

        // ============================================================
        // ‹­UŒ‚”­“®
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
        // ‹­UŒ‚”ÍˆÍ“à‚ÉPlayer‚ª‚¢‚é‚©”»’è
        // ============================================================
        // StrongAttackƒIƒuƒWƒFƒNƒg‚Ì’†SˆÊ’u‚ÆPlayerˆÊ’u‚Ì‹——£‚ğ’²‚×A
        // Inspector‚Åİ’è‚µ‚½strongAttackRadiusˆÈ“à‚È‚çtrue‚ğ•Ô‚·B
        // ‚±‚ÌƒQ[ƒ€‚Íã‚©‚çŒ©‰º‚ë‚·Œ`®‚È‚Ì‚ÅAYi‚‚³j‚Í–³‹‚µ‚Ä
        // XZ•½–Ê‚¾‚¯‚Å‹——£‚ğŒvZ‚·‚éB
        // ============================================================
        private bool IsPlayerInsideStrongAttack()
        {
            // StrongAttack‚Ü‚½‚ÍPlayer‚ª–¢İ’è‚È‚ç
            // ³‚µ‚¢”»’è‚ª‚Å‚«‚È‚¢‚½‚ßUŒ‚”ÍˆÍŠO‚Æ‚µ‚Äˆµ‚¤B
            if (strongAttackObject == null ||
                player == null)
            {
                return false;
            }

            // ‹­UŒ‚ƒIƒuƒWƒFƒNƒg‚Ì’†SˆÊ’u‚ğæ“¾B
            Vector3 attackPosition =
                strongAttackObject.transform.position;

            // Œ»İ‚ÌPlayerˆÊ’u‚ğæ“¾B
            Vector3 playerPosition =
                player.transform.position;

            // --------------------------------------------------------
            // XZ•½–Ê‚Ö•ÏŠ·
            // --------------------------------------------------------
            // ã‚©‚çŒ©‰º‚ë‚·ƒQ[ƒ€‚È‚Ì‚ÅA
            // Y•ûŒüi‚‚³j‚Ì·‚Í“–‚½‚è”»’è‚Ég—p‚µ‚È‚¢B
            Vector2 attackXZ =
                new Vector2(
                    attackPosition.x,
                    attackPosition.z);

            Vector2 playerXZ =
                new Vector2(
                    playerPosition.x,
                    playerPosition.z);

            // ‹­UŒ‚’†S‚©‚çPlayer‚Ü‚Å‚Ì‹——£‚ğŒvZB
            float distance =
                Vector2.Distance(
                    attackXZ,
                    playerXZ);

            // Inspector‚Åİ’è‚µ‚½”¼ŒaˆÈ“à‚È‚ç–½’†B
            // true  = ‹­UŒ‚”ÍˆÍ“à
            // false = ‹­UŒ‚”ÍˆÍŠO
            return distance <=
                strongAttackRadius;
        }
        // ============================================================
        // StrongAttack“§–¾“x•ÏX
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

            // Œ»İ‚Ìƒ}ƒeƒŠƒAƒ‹F‚ğæ“¾
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

        // å¼¾ã‚’é€²ã‚ã¦è¡çªã‚’è§£æ±ºã™ã‚‹ã€‚playerFrom/Toã¯ã“ãEæ™‚é–“åŒºé–“ãEãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ç§»å‹•å‰Eå¾ŒãEä½ç½®ã€E
        public void TickBullets(Vector3 playerFrom, Vector3 playerTo, float dt)
        {
            if (IsHitStopped) return;
            // é€šå¸¸ã®è¢«å¼¾ã‚ˆã‚Šå…ˆã«ãƒ‘ãƒªã‚£ã‚’åˆ¤å®šã™ã‚‹ã€‚å—ä»˜ä¸­ã«å¼·ãEã®å¼¾ã¸æ¥è§¦ã™ã‚‹ã¨å…¨å¼¾ã‚’æ¶ˆã™ã€E
            // iEšã“ã®ç¹°ã‚Šè¿”ã—ã§å‡¦çE™ã‚‹å¯¾è±¡ã®ç•ªå·ã€‚æ¡ä»¶ã‚’æº€ãŸã™é–“ã€E E•ªã«æ›´æ–°ã™ã‚‹ã€E
            for (int i = 0; i < bullets.Count; i++) bullets[i].Simulate(dt);
            if (parryRemaining > 0f)
            {
                // bulletEšä¸€è¦§ã‹ã‚‰å–ã‚Šå‡ºã—ãŸã€ä»Šå›å‡¦çE™ã‚‹å¯¾è±¡ã€E
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
            // é«˜é€ŸæŠ•æ“²ãŒè¤E•°ã®å¼¾ã‚’æ¨ªåˆE‚‹å ´åˆã‚‚æ¥è§¦é E«å‡¦çE™ã‚‹ã€E
            // æ ¼ä¸ŠãEå¼¾ã§ã‚­ãƒ¥ãƒ¼ãŒæ¶ˆæ»E—ãŸå¾Œã€ãã®å¥¥ã®å¼¾ã¾ã§æ¶ˆã—ã¦ã—ã¾ãE“ã¨ã‚’é˜²ãã€E
            if (balloon.CanHit && bullets.Count > 1) bullets.Sort(CompareCueContactOrder);
            // å‰Šé™¤ã§ãƒªã‚¹ãƒˆãEæ·»å­—ãŒãšã‚Œã¦ã‚‚æœªå‡¦çEEå¼¾ã‚’é£›ãEã•ãªãE‚ˆãE€å¾Œã‚ã‹ã‚‰èª¿ã¹ã‚‹ã€E
            // åŒã˜åˆ¤å®šåŒºé–“ã§è¤E•°ã®å¼¾ã‚’æ¶ˆã—ã¦ã‚‚ã€å¤–åEã‚’é‡ã­æãã—ãªãEŸã‚ãEå°ã€E
            bool emittedEraseEffect = false;
            // iEšã“ã®ç¹°ã‚Šè¿”ã—ã§å‡¦çE™ã‚‹å¯¾è±¡ã®ç•ªå·ã€‚æ¡ä»¶ã‚’æº€ãŸã™é–“ã€E E•ªã«æ›´æ–°ã™ã‚‹ã€E
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                // ç”ŸæEã¾ãŸãEåˆ¤å®šå¯¾è±¡ã¨ãªã‚‹æ•µå¼¾ã€E
                var bullet = bullets[i];
                // æ•µå¼¾ãŒã“ã®ç§»å‹•åŒºé–“ã§ãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ã«æ¥è§¦ã™ã‚‹ã‹ã€‚playerTimeã¯æ¥è§¦æ™‚ç‚¹EEEEE‰ã€E
                bool hitsPlayer = Sweep(bullet.PreviousPosition - playerFrom,
                    bullet.transform.position - playerTo, bullet.Radius + DragPlayer.Radius, out float playerTime);
                // æ•µå¼¾ãŒæ°´é¢¨èˆ¹ã¸è§¦ã‚Œã‚‹æ™‚ç‚¹EEEEE‰ã€‚æœªæ¥è§¦ãªã‚‰ç„¡é™å¤§ã®ã¾ã¾ã«ã™ã‚‹ã€E
                float ballTime = float.PositiveInfinity;
                // å¼·ã•ã«é–¢ä¿‚ãªãã‚­ãƒ¥ãƒ¼ã¸ã®æ¥è§¦ã‚’èª¿ã¹ã‚‹ã€‚æ¥è§¦å¾Œã«ãƒ¬ãƒ™ãƒ«ã®å¤§å°ã§çµæœã‚’åEã‘ã‚‹ã€E
                bool hitsBall = balloon.CanHit && Sweep(
                    bullet.PreviousPosition - balloon.PreviousPosition,
                    bullet.transform.position - balloon.transform.position,
                    bullet.Radius + balloon.HitRadius, out ballTime);
                // ç´ãEå¼¾ã‚’æ¶ˆã•ãªãE€‚é¢¨èˆ¹æœ¬ä½“ãŒãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼ã‚ˆã‚Šå…ˆã«å¼¾ã¸å½“ãŸã£ãŸã¨ãã ã‘é˜²ãã€E
                if (hitsBall && (!hitsPlayer || ballTime <= playerTime))
                {
                    if (balloon.Power >= bullet.Power)
                    {
                        // åŒãƒ¬ãƒ™ãƒ«ä»¥ä¸‹ãªã‚‰æ•µå¼¾ã‚’æ¶ˆã—ã€ã‚­ãƒ¥ãƒ¼ã¯å¼·ã•ã¨é£›è¡Œï¼åEè»¢ã‚’ç¶­æŒã—ã¦è²«é€šã™ã‚‹ã€E
                        if (!TryPlayBulletErasePrefab(bullet, ballTime) && !emittedEraseEffect)
                        {
                            EmitBulletEraseEffect();
                            emittedEraseEffect = true;
                        }
                        RemoveBullet(i);
                        BeginHitStop(bulletHitStop);
                        continue;
                    }
                    // æ ¼ä¸ŠãEæ•µå¼¾ã¯æ®‹ã‚Šã€ã‚­ãƒ¥ãƒ¼ã ã‘ãŒæ¶ˆãˆã‚‹ã€‚æ•µå¼¾ã®ãƒ—ãƒ¬ã‚¤ãƒ¤ãƒ¼æ¥è§¦åˆ¤å®šãEç¶™ç¶šã™ã‚‹ã€E
                    balloon.Consume();
                }
                if (hitsPlayer)
                {
                    // Player‚É“–‚½‚Á‚½’e‚ğíœ
                    RemoveBullet(i);

                    // ƒpƒŠƒB‚È‚Ç‚É‚æ‚é–³“GŠÔ’†‚Å‚Í‚È‚¢ê‡
                    if (invincible <= 0.0f)
                    {
                        DamagePlayer();
                    }

                    // GameOver‚É‚È‚Á‚½ê‡‚Íˆ—I—¹
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

        // å¾Œã‚ã‹ã‚‰å‰Šé™¤ã™ã‚‹ãƒ«ãƒ¼ãƒ—ã«åˆã‚ã›ã€æ¥è§¦ãŒé…ãE¼¾ã‹ã‚‰æ—©ãE¼¾ã®é E¸ä¸¦ã¹ã‚‹ã€E
        private int CompareCueContactOrder(Bullet left, Bullet right)
        {
            return CueContactTime(right).CompareTo(CueContactTime(left));
        }

        // ã‚­ãƒ¥ãƒ¼ã¨bulletãŒç§»å‹•åŒºé–“ã§æ¥è§¦ã™ã‚‹æ™‚ç‚¹ã€‚æ¥è§¦ã—ãªãE ´åˆãEæœ€å¾Œã«å‡¦çE™ã‚‹ãŸã‚ç„¡é™å¤§ã‚’è¿”ã™ã€E
        private float CueContactTime(Bullet bullet)
        {
            // æ¥è§¦ãŒèµ·ãã‚‹æ™‚ç‚¹EˆåŒºé–“ãEé–‹å§EEçµ‚äºEE‰ã€‚å¼·ã•ã§ã¯çµã‚Šè¾¼ã¾ãšæ ¼ä¸Šã‚‚å«ã‚ã‚‹ã€E
            bool hits = Sweep(bullet.PreviousPosition - balloon.PreviousPosition,
                bullet.transform.position - balloon.transform.position, bullet.Radius + balloon.HitRadius, out float time);
            return hits ? time : float.PositiveInfinity;
        }

        // ãƒ‘ãƒªã‚£æˆåŠŸã‚’ç¢ºå®šã™ã‚‹ã€‚è·é›¢ã‚E¼¾ã®å¼·ã•ã‚’å•ã‚ãšã€ç®¡çE¸­ã®æ•µå¼¾ã‚’å³åº§ã«å…¨æ¶ˆå»ã™ã‚‹ã€E
        // å—ä»˜ã‚‚çµ‚äºE•ã›ã€åŒã˜æŠ•æ“²ã§ãƒ‘ãƒªã‚£å›æ•°ã‚E¼”åEãŒé‡è¤E—ãªãE‚ˆãE«ã™ã‚‹ã€E
        private void ResolveParry(Vector3 position)
        {
            ParryCount++;
            
            // ãƒ‘ãƒªã‚£æˆåŠŸã®SEã‚’åEç”Ÿã™ã‚‹ã€E
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySE("ParrySE");
            }
            parryRemaining = 0f;
            ClearBullets();
            EmitPulse(position, Color.cyan, 5f, DragPlayer.Radius * player.Parameters.ParryRange);
        }

        // æ¶ˆã—ãŸæ•µå¼¾ã®æ¥è§¦æ™‚ç‚¹ã«ã€ã‚­ãƒ¥ãƒ¼ã®ãƒ¬ãƒ™ãƒ«ã¨é€²è¡Œæ–¹å‘ã«å¿œã˜ãŸPrefabã‚’å†ç”Ÿã™ã‚‹ã€‚
        private bool TryPlayBulletErasePrefab(Bullet bullet, float contactTime)
        {
            // ã“ã®ã‚·ãƒ¼ãƒ³ã«ç™»éŒ²ã•ã‚ŒãŸå¼¾æ¶ˆã—æ¼”å‡ºã®ç®¡ç†ã‚³ãƒ³ãƒãƒ¼ãƒãƒ³ãƒˆã€‚
            BulletEraseEffectPlayer effectPlayer = GetComponent<BulletEraseEffectPlayer>();
            if (effectPlayer == null) return false;
            // é«˜é€Ÿç§»å‹•ã§ã‚‚ãƒ•ãƒ¬ãƒ¼ãƒ çµ‚ç«¯ã§ã¯ãªãã€å®Ÿéš›ã«æ¥è§¦ã—ãŸä½ç½®ã¸æ¼”å‡ºã‚’ç½®ãã€‚
            Vector3 impactPosition = Vector3.Lerp(bullet.PreviousPosition, bullet.transform.position, Mathf.Clamp01(contactTime));
            // å…¬è»¢ä¸­ãƒ»æŠ•æ“²ä¸­ã¨ã‚‚ã«ã€ã‚­ãƒ¥ãƒ¼ãŒæ•µå¼¾ã‚’æ‰“ã¡æ¶ˆã—ãŸé€²è¡Œæ–¹å‘ã¸ç«èŠ±ã‚’é£›ã°ã™ã€‚
            Vector3 impactDirection = BulletEraseEffectPlayer.ResolveDirection(
                balloon.transform.position - balloon.PreviousPosition, balloon.Velocity,
                bullet.Velocity, impactPosition - balloon.transform.position);
            return effectPlayer.TryPlay(balloon.Power, impactPosition, impactDirection);
        }
        // å¼¾ã‚’æ‰“ã¡æ¶ˆã—ãŸä½ç½®ã®ã‚­ãƒ¥ãƒ¼å¤–å‘¨ã«è¼ªã‚’ä½œã‚‹ã€‚å¼·åŒ–ã‚„ãƒ‘ãƒƒã‚·ãƒ–ã«ã‚ˆã‚‹åˆ¤å®šåŠå¾E‚‚åæ˜ ã™ã‚‹ã€E
        private void EmitBulletEraseEffect()
        {
            if (bulletEraseDuration <= 0f) return;
            EmitPulse(balloon.transform.position, bulletEraseColor,
                balloon.HitRadius + Mathf.Max(0f, bulletEraseExpansion), balloon.HitRadius,
                bulletEraseDuration, Mathf.Max(0.001f, bulletEraseWidth), "Bullet erase outer ring");
        }

        // æŠ•æ“²ä¸­ã®é¢¨èˆ¹ã ã‘ãEã‚¹ã«å½“ãŸã‚‹ã€‚å¼±ç‚¹ã¨èƒ´ä½“ãEãE¡å…ˆã«æ¥è§¦ã—ãŸæ–¹ã‚’æ¡ç”¨ã™ã‚‹ã€E
        // å¼±ç‚¹ãªã‚‰ã‚­ãƒ£ãƒ©æ”»æ’EŠ›ã¨ãƒ¬ãƒ™ãƒ«ã«å¿œã˜ãŸãƒ€ãƒ¡ãƒ¼ã‚¸ã€èƒ´ä½“ãªã‚‰ãƒ€ãƒ¡ãƒ¼ã‚¸ãªã—ã§é¢¨èˆ¹ã‚’æ¶ˆè²»ã™ã‚‹ã€E
        public void CheckBossHit()
        {
            if (IsHitStopped) return;
            if (balloon.State != WaterBalloon.MotionState.Flying) return;
            // æ°´é¢¨èˆ¹ãŒå¼±ç‚¹ã«æ¥è§¦ã™ã‚‹ã‹ã€‚weakTimeã¯ç§»å‹•åŒºé–“åEã®æœ€åˆãEæ¥è§¦æ™‚ç‚¹EEEEE‰ã€E
            bool weakHit = Sweep(balloon.PreviousPosition - weakPoint.position,
                balloon.transform.position - weakPoint.position, balloon.HitRadius + 0.55f, out float weakTime);
            // æ°´é¢¨èˆ¹ãŒèƒ´ä½“ã«æ¥è§¦ã™ã‚‹ã‹ã€‚bodyTimeã¯ç§»å‹•åŒºé–“åEã®æœ€åˆãEæ¥è§¦æ™‚ç‚¹EEEEE‰ã€E
            bool bodyHit = Sweep(balloon.PreviousPosition - boss.position,
                balloon.transform.position - boss.position, balloon.HitRadius + 1.15f, out float bodyTime);
            if (weakHit && (!bodyHit || weakTime <= bodyTime))
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySE("BossWeakSE");
                }
                BossHealth = Mathf.Max(0f, BossHealth - balloon.CurrentDamage);
                //ƒ{ƒX‚ÌHP‚ğƒQ[ƒWUI‚É”½‰f
                UpdateBossHPUI();
                if (!TryPlayEnemyHitPrefab(weakHitPrefab, weakTime))
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
                if (!TryPlayEnemyHitPrefab(bodyHitPrefab, bodyTime))
                    EmitPulse(balloon.transform.position, Color.gray, 0.7f);
                StopOnBalloonImpact(bodyHitStop);
            }
        }

        // ç§»å‹•åŒºé–“ã¨çEEè¡çªã‚’èª¿ã¹ã€E«˜é€Ÿãªå¼¾ã‚Eƒ•ãƒªãƒE‚¯ã®ã™ã‚ŠæŠœã‘ã‚’é˜²ãã€E
        // from/toã¯ç›¸æ‰‹ã‹ã‚‰è¦‹ãŸç›¸å¯¾ä½ç½®ã€radiusã¯åŒæ–¹ã®åŠå¾EEåˆè¨ˆã€E
        // timeã¯æœ€åˆã«æ¥è§¦ã™ã‚‹æ™‚ç‚¹EE=åŒºé–“ãEé–‹å§‹ã€E=çµ‚äºE¼‰ã€‚æˆ»ã‚Šå€¤ãŒtrueã®ã¨ãã«ä½¿ãE€E
        // durationã¯åœæ­¢ã•ã›ã‚‹å®Ÿæ™‚é–“ãEç§’æ•°ã€‚åŒæ™‚å‘½ä¸­ã§åœæ­¢æ™‚é–“ãŒç©ã¿ä¸ŠãŒã‚‰ãªãE‚ˆãE•·ãE–¹ã‚’ä½¿ãE€E
        private bool TryPlayEnemyHitPrefab(GameObject prefab, float contactTime)
        {
            var effectPlayer = GetComponent<BulletEraseEffectPlayer>();
            if (prefab == null || effectPlayer == null) return false;
            Vector3 position = Vector3.Lerp(balloon.PreviousPosition, balloon.transform.position, contactTime);
            Vector3 direction = BulletEraseEffectPlayer.ResolveDirection(
                balloon.transform.position - balloon.PreviousPosition, balloon.Velocity,
                Vector3.zero, boss.position - position);
            return effectPlayer.TryPlay(prefab, position, direction);
        }
        private void BeginHitStop(float duration)
        {
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            hitStopRemaining = Mathf.Max(hitStopRemaining, duration);
        }

        // durationç§’ãEå‘½ä¸­æ¼”åEã‚’é–‹å§‹ã™ã‚‹ã€‚åœæ­¢ä¸­ã¯ã‚­ãƒ¥ãƒ¼ã‚’æ®‹ã—ã€åœæ­¢çµ‚äºE™‚ã«å†ç”Ÿç”£å¾E¡ã¸ç§»ã™ã€E
        private void StopOnBalloonImpact(float duration)
        {
            BeginHitStop(duration);
            if (IsHitStopped) consumeBalloonAfterHitStop = true;
            else balloon.Consume();
        }

        // unscaledDeltaTimeç§’ã ã‘åœæ­¢æ™‚é–“ã‚’é€²ã‚ã‚‹ã€‚ã‚²ãƒ¼ãƒ æœ¬ä½“ãEæ™‚é–“å€ç‡ã‚E¸€æ™‚åœæ­¢ã‚’å¤‰æ›´ã—ãªãE€E
        public void AdvanceHitStop(float unscaledDeltaTime)
        {
            if (!IsHitStopped) return;
            hitStopRemaining = Mathf.Max(0f, hitStopRemaining - Mathf.Max(0f, unscaledDeltaTime));
            if (!IsHitStopped) CompleteHitStop();
        }

        // åœæ­¢ã‚’çµ‚äºE—ã€ä¿ç•™ã—ã¦ãEŸå‘½ä¸­æ¸ˆã¿ã‚­ãƒ¥ãƒ¼ã®æ¶ˆè²»ã‚Eå›ã ã‘è¡Œã†ã€E
        private void CompleteHitStop()
        {
            hitStopRemaining = 0f;
            if (consumeBalloonAfterHitStop && balloon != null) balloon.Consume();
            consumeBalloonAfterHitStop = false;
        }

        // ç„¡åŠ¹åŒ–ãEã‚·ãƒ¼ãƒ³ç§»å‹•ã§åœæ­¢ã‚EŠ•æ“²äºˆç´E‚’æŒã¡è¶Šã•ãªãE‚ˆãE«ã™ã‚‹ã€E
        private void OnDisable()
        {
            StopCameraShake();
            CompleteHitStop();
            if (player != null) player.TryConsumeBufferedRelease(out _);
        }

        // ç§»å‹•åŒºé–“ã¨çEEè¡çªã‚’èª¿ã¹ã€E«˜é€Ÿãªå¼¾ã‚Eƒ•ãƒªãƒE‚¯ã®ã™ã‚ŠæŠœã‘ã‚’é˜²ãã€E
        // from/toã¯ç›¸æ‰‹ã‹ã‚‰è¦‹ãŸç›¸å¯¾ä½ç½®ã€radiusã¯åŒæ–¹ã®åŠå¾EEåˆè¨ˆã€E
        // timeã¯æœ€åˆã«æ¥è§¦ã™ã‚‹æ™‚ç‚¹EE=åŒºé–“ãEé–‹å§‹ã€E=çµ‚äºE¼‰ã€‚æˆ»ã‚Šå€¤ãŒtrueã®ã¨ãã«ä½¿ãE€E
        public static bool Sweep(Vector3 from, Vector3 to, float radius, out float time)
        {
            time = 0f;
            // é–‹å§‹æ™‚ç‚¹ã§ã™ã§ã«é‡ãªã£ã¦ãE‚Œã°ã€æ™‚åˆ»0ã§æ¥è§¦ã—ã¦ãE‚‹ã€E
            float c = from.sqrMagnitude - radius * radius;
            if (c <= 0f) return true;
            // ç›¸æ‰‹ã‹ã‚‰è¦‹ãŸã€ç§»å‹•åŒºé–“ãEå§‹ç‚¹ã‹ã‚‰çµ‚ç‚¹ã¸ã®å¤‰åŒ–é‡ã€E
            Vector3 delta = to - from;
            // è¡çªãEäºŒæ¬¡æ–¹ç¨‹å¼ãEä¿‚æ•°ã€‚ç§»å‹•é‡ã®é•·ã•ãE2ä¹—ã€E
            float a = delta.sqrMagnitude;
            if (a < 0.000001f) return false;
            // è¡çªãEäºŒæ¬¡æ–¹ç¨‹å¼ãEä¿‚æ•°ã€‚å§‹ç‚¹ã¨ç§»å‹•æ–¹å‘ãEå†E©ã€E
            float b = Vector3.Dot(from, delta);
            // ç§»å‹•ã™ã‚‹ç‚¹ãŒçƒé¢ã¸åˆ°é”ã™ã‚‹äºŒæ¬¡æ–¹ç¨‹å¼ã‚’è§£ãã€‚åˆ¤åˆ¥å¼ãŒè² ãªã‚‰æ¥è§¦ã—ãªãE€E
            float discriminant = b * b - a * c;
            if (discriminant < 0f) return false;
            time = (-b - Mathf.Sqrt(discriminant)) / a;
            return time >= 0f && time <= 1f;
        }

        // å‹æ•—ã‚’ç¢ºå®šã—ã€è‰²ã¨å†E½¢ã‚¨ãƒ•ã‚§ã‚¯ãƒˆã§çµæœã‚’ç¤ºã—ã¦æ®‹ã‚Šã®å¼¾ã‚’ç‰‡ä»˜ã‘ã‚‹ã€E
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

        // Destroyã¯ãƒ•ãƒ¬ãƒ¼ãƒ æœ«å°¾ã¾ã§éE»¶ã™ã‚‹ãŸã‚ã€åEã«éè¡¨ç¤ºã«ã—ã¦ç®¡çEƒªã‚¹ãƒˆã‹ã‚‰é™¤ãã€E
        private void RemoveBullet(int index)
        {
            bullets[index].gameObject.SetActive(false);
            Destroy(bullets[index].gameObject);
            bullets.RemoveAt(index);
        }
        // ãƒªã‚¹ãƒˆæœ«å°¾ã‹ã‚‰å…¨å¼¾ã‚’å‰Šé™¤ã™ã‚‹ã€‚ãƒªãƒˆãƒ©ã‚¤ã€ãƒ‘ãƒªã‚£ã€ãƒ©ã‚¦ãƒ³ãƒ‰çµ‚äºE™‚ã«ä½¿ç”¨ã™ã‚‹ã€E
        private void ClearBullets()
        {
            // iEšã“ã®ç¹°ã‚Šè¿”ã—ã§å‡¦çE™ã‚‹å¯¾è±¡ã®ç•ªå·ã€‚æ¡ä»¶ã‚’æº€ãŸã™é–“ã€E E•ªã«æ›´æ–°ã™ã‚‹ã€E
            for (int i = bullets.Count - 1; i >= 0; i--) RemoveBullet(i);
        }

        // positionã‚’ä¸­å¿E«startRadiusã‹ã‚‰radiusã¸åºEŒã‚‹åEã‚’ã€durationç§’é–“è¡¨ç¤ºã™ã‚‹ã€E
        // widthã¯ç·šãEå¤ªã•ã€effectNameã¯Hierarchyã§æ¼”åEã‚’è­˜åˆ¥ã™ã‚‹åå‰ã€‚åŒæ™‚è¡¨ç¤ºã¯8å€‹ã¾ã§ã€E
        private void EmitPulse(Vector3 position, Color color, float radius, float startRadius = 0.2f,
            float duration = 0.45f, float width = 0.07f, string effectName = "Gameplay pulse")
        {
            // ä¸Šé™æ™‚ãEå¤ãE¼ªã‚’çµ‚äºE—ã€æ–°ã—ãèµ·ããŸå¼¾æ¶ˆã—ã‚Eƒ‘ãƒªã‚£ã®æ¼”åEã‚’å¿Ešè¡¨ç¤ºã™ã‚‹ã€E
            if (pulses.Count >= 8)
            {
                pulses[0].line.gameObject.SetActive(false);
                Destroy(pulses[0].line.gameObject);
                pulses.RemoveAt(0);
            }
            // å†E½¢ã‚¨ãƒ•ã‚§ã‚¯ãƒˆç”¨ã«ä½œã‚‹ä¸€æ™‚çš„ãªGameObjectã€E
            var go = new GameObject(effectName);
            go.transform.SetParent(transform);
            go.transform.position = position;
            // ç·šã‚’è¡¨ç¤ºã™ã‚‹LineRendererã€E
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = effectMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            // iEšã“ã®ç¹°ã‚Šè¿”ã—ã§å‡¦çE™ã‚‹å¯¾è±¡ã®ç•ªå·ã€‚æ¡ä»¶ã‚’æº€ãŸã™é–“ã€E E•ªã«æ›´æ–°ã™ã‚‹ã€E
            for (int i = 0; i < 40; i++)
            {
                // å†E‘¨ä¸ŠãEé…ç½®ã«ä½¿ç”¨ã™ã‚‹è§’åº¦Eˆãƒ©ã‚¸ã‚¢ãƒ³E‰ã€E
                float angle = i * Mathf.PI * 2f / 40f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * startRadius);
            }
            pulses.Add(new Pulse { line = line, color = color, radius = radius,
                startRadius = startRadius, duration = Mathf.Max(0.001f, duration) });
        }

        // å¼±ç‚¹å‘½ä¸­ã®ç¬é–“ã«æºã‚Œã‚’é–‹å§‹ã™ã‚‹ã€‚å¤ãE½ç½®ã®ãšã‚Œã‚’æ®‹ã•ãšã€E€£ç¶šå‘½ä¸­ã§ã¯æ¼”åEã‚’æ›´æ–°ã™ã‚‹ã€E
        private void BeginCameraShake()
        {
            StopCameraShake();
            if (gameCamera == null || cameraShakeStrength <= 0f || cameraShakeDuration <= 0f) return;
            IsCameraShaking = true;
            cameraShakeElapsed = 0f;
            AdvanceCameraShake(0f);
        }

        // unscaledDeltaTimeç§’ã ã‘æºã‚Œã‚’é€²ã‚ã‚‹ã€‚ãƒ’ãƒEƒˆã‚¹ãƒˆãƒƒãƒ—ä¸­ã‚‚æ¸›è¡°ã—ã€çµ‚äºE™‚ã¯å…EEä½ç½®ã¸æˆ»ã™ã€E
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
            // æ¼”åEçµ‚ç›¤ã»ã©å°ã•ãã™ã‚‹æºã‚Œå¹E€‚ã‚²ãƒ¼ãƒ å†EEåˆ¤å®šä½ç½®ã¯å‹•ã‹ã•ãªãE€E
            float amplitude = cameraShakeStrength * (1f - cameraShakeElapsed / cameraShakeDuration);
            // æ™‚é–“ã‹ã‚‰æ±ºã¾ã‚‹æŒ¯å‹•ãEä½ç›¸ã€‚ä¹±æ•°ã‚’ä½¿ã‚ãšã€æ¤œè¨¼æ™‚ã‚‚åŒã˜æºã‚Œã‚’å†ç¾ã§ãã‚‹ã€E
            float phase = cameraShakeElapsed * Mathf.PI * 2f * cameraShakeFrequency;
            cameraShakeOffset = (gameCamera.transform.right * Mathf.Cos(phase)
                + gameCamera.transform.up * Mathf.Cos(phase * 0.73f + 1f)) * amplitude;
            gameCamera.transform.position += cameraShakeOffset;
        }

        // æœ€å¾Œã«åŠ ãˆãŸæç”»ç”¨ã®ãšã‚Œã ã‘ã‚’å–ã‚Šé™¤ãã€ã‚«ãƒ¡ãƒ©æœ¬æ¥ã®ä½ç½®ã‚’ä¿æŒã™ã‚‹ã€E
        private void RestoreCameraOffset()
        {
            if (gameCamera != null) gameCamera.transform.position -= cameraShakeOffset;
            cameraShakeOffset = Vector3.zero;
        }

        // ãƒªãƒˆãƒ©ã‚¤ã€ç„¡åŠ¹åŒ–ã€æºã‚Œçµ‚äºE§å‘¼ã¶å¾Œç‰‡ä»˜ã‘ã€‚ã‚«ãƒ¡ãƒ©ã®ãšã‚Œã‚’æ¬¡ã®ãƒ©ã‚¦ãƒ³ãƒ‰ã«æŒã¡è¶Šã•ãªãE€E
        private void StopCameraShake()
        {
            RestoreCameraOffset();
            IsCameraShaking = false;
            cameraShakeElapsed = 0f;
        }
        // å†E‚’æŒE®šã—ãŸè¡¨ç¤ºæ™‚é–“ã§æ‹¡å¤§ãƒ»æš—ãã—ã€å¯¿å‘½ã‚’è¿ãˆãŸã‚‰å³åº§ã«éè¡¨ç¤ºã«ã—ã¦å‰Šé™¤ã™ã‚‹ã€E
        private void UpdatePulses(float dt)
        {
            // iEšã“ã®ç¹°ã‚Šè¿”ã—ã§å‡¦çE™ã‚‹å¯¾è±¡ã®ç•ªå·ã€‚æ¡ä»¶ã‚’æº€ãŸã™é–“ã€E E•ªã«æ›´æ–°ã™ã‚‹ã€E
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                // æ›´æ–°ã¾ãŸãEå‰Šé™¤å¯¾è±¡ã®å†E½¢ã‚¨ãƒ•ã‚§ã‚¯ãƒEå€‹åEã®ãƒEEã‚¿ã€E
                var pulse = pulses[i];
                pulse.age += dt;
                // ã‚¨ãƒ•ã‚§ã‚¯ãƒˆãEå¯¿å‘½ã«å¯¾ã™ã‚‹çµŒéå‰²åˆã€Eã«ãªã‚‹ã¨å‰Šé™¤ã™ã‚‹ã€E
                float t = pulse.age / pulse.duration;
                if (t >= 1f)
                {
                    pulse.line.gameObject.SetActive(false);
                    Destroy(pulse.line.gameObject);
                    pulses.RemoveAt(i);
                    continue;
                }
                // å†E½¢ã‚¨ãƒ•ã‚§ã‚¯ãƒˆãEè¡¨ç¤ºè‰²ã€E
                Color color = pulse.color * (1f - t);
                color.a = 1f;
                pulse.line.startColor = pulse.line.endColor = color;
                // jEšã“ã®ç¹°ã‚Šè¿”ã—ã§å‡¦çE™ã‚‹å¯¾è±¡ã®ç•ªå·ã€‚æ¡ä»¶ã‚’æº€ãŸã™é–“ã€E E•ªã«æ›´æ–°ã™ã‚‹ã€E
                for (int j = 0; j < 40; j++)
                {
                    // å†E‘¨ä¸ŠãEé…ç½®ã«ä½¿ç”¨ã™ã‚‹è§’åº¦Eˆãƒ©ã‚¸ã‚¢ãƒ³E‰ã€E
                    float angle = j * Mathf.PI * 2f / 40;
                    pulse.line.SetPosition(j, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Lerp(pulse.startRadius, pulse.radius, t));
                }
            }
        }

        private void UpdateBossHPUI()
        {
            if(bossHpSlider == null)
            {
                return;
            }

            bossHpSlider.minValue = 0.0f;
            bossHpSlider.maxValue = bossMaxHealth;
            bossHpSlider.wholeNumbers = false;
            bossHpSlider.interactable = false;
            bossHpSlider.transition = UnityEngine.UI.Selectable.Transition.None;
            bossHpSlider.value = BossHealth;

        }
    }
}
