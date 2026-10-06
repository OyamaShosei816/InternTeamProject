using UnityEngine;
using UnityEngine.InputSystem;

namespace KazumaPrototype
{
    // 謖・・繝槭え繧ｹ縺ｮ遘ｻ蜍暮㍼縺縺代・繝ｬ繧､繝､繝ｼ繧貞虚縺九☆縲よ款縺励◆菴咲ｽｮ縺ｸ縺ｮ迸ｬ髢鍋ｧｻ蜍輔・縺励↑縺・・
    // Arena 縺・ReadInput 繧貞他縺ｳ縲∫ｧｻ蜍暮溷ｺｦ繧・款縺呻ｼ城屬縺咏椪髢薙ｒ豌ｴ鬚ｨ闊ｹ縺ｮ蛻ｶ蠕｡縺ｫ貂｡縺吶・
    public sealed class KazumaDragPlayer : MonoBehaviour
    {
        // 繧ｭ繝｣繝ｩ繧ｯ繧ｿ繝ｼ縺ｮ蝓ｺ譛ｬ諤ｧ閭ｽ縲ょｮ溯｡梧凾縺ｮ繝代ャ繧ｷ繝冶｣懈ｭ｣繧ゅ％縺ｮ繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｫ菫晄戟縺吶ｋ縲・
        [Header("キャラクター性能：パリィ・キュー・攻撃力")]
        [Tooltip("6種類の基本倍率を調整します。すべて1が標準性能です。")]
        [SerializeField] private KazumaPlayerParameters parameters = new KazumaPlayerParameters();

        // Arena縺ｨ豌ｴ鬚ｨ闊ｹ縺悟盾辣ｧ縺吶ｋ蜈ｱ騾壹ヱ繝ｩ繝｡繝ｼ繧ｿ繝ｼ縲よ立Prefab縺ｫ繧ょ・譛溷､縺ｧ蟇ｾ蠢懊☆繧九・
        public KazumaPlayerParameters Parameters => parameters ?? (parameters = new KazumaPlayerParameters());

        // 繝峨Λ繝・げ遘ｻ蜍暮㍼縺ｮ蛟咲紫縲・縺ｪ繧臥判髱｢荳翫・謖・・遘ｻ蜍輔↓蟇ｾ蠢懊＠縺溯ｷ晞屬縺縺大虚縺上・
        [Header("操作：ドラッグへの反応倍率")]
        [Tooltip("1が標準です。大きいほど同じドラッグ量で遠くへ移動します。")]
        [SerializeField] private float dragSensitivity = 1f;

        // 豌ｴ鬚ｨ闊ｹ縺ｸ貂｡縺咎溷ｺｦ縺ｮ荳企剞・医Ρ繝ｼ繝ｫ繝牙腰菴搾ｼ冗ｧ抵ｼ峨ゅ・繝ｬ繧､繝､繝ｼ縺ｮ遘ｻ蜍戊ｷ晞屬閾ｪ菴薙・蛻ｶ髯舌＠縺ｪ縺・・
        [Header("投げ操作：引き継ぐ速度の上限")]
        [Tooltip("水風船へ渡す移動速度の上限です。単位はワールド単位毎秒です。")]
        [SerializeField] private float maximumFlickSpeed = 18f;

        // 濶ｲ繧・ｽ｢繧定｡ｨ遉ｺ縺吶ｋ譛ｬ菴薙・Renderer縲・
        [Header("プレイヤーの見た目：表示用Renderer")]
        [Tooltip("プレイヤーPrefab内の表示用Rendererを設定します。")]
        [SerializeField] private Renderer body;

        // 繝励Ξ繧､繝､繝ｼ縺ｮ蠖薙◆繧雁愛螳壼濠蠕・りｦ九◆逶ｮ縺ｮ諡｡邵ｮ縺ｨ縺ｯ迢ｬ遶九＠縺ｦ縺・ｋ縲・
        public const float Radius = 0.34f;
        // 迴ｾ蝨ｨ謚ｼ縺励※縺・ｋ縺九∽ｻ雁屓縺ｮ遘ｻ蜍暮溷ｺｦ縲∵兜縺偵ｋ縺ｨ縺阪↓菴ｿ縺・峩霑代・遘ｻ蜍暮溷ｺｦ縲・
        public bool IsHeld { get; private set; }
        // 1遘貞ｽ薙◆繧翫・遘ｻ蜍暮㍼繧定｡ｨ縺咎溷ｺｦ縲・
        public Vector3 Velocity { get; private set; }
        // 謚輔￡繧狗椪髢薙↓豌ｴ鬚ｨ闊ｹ縺ｸ蜉縺医ｋ縲∫峩霑代・繝励Ξ繧､繝､繝ｼ遘ｻ蜍暮溷ｺｦ縲・
        public Vector3 FlickVelocity { get; private set; }
        // 謚ｼ縺怜ｧ九ａ・城屬縺励◆迸ｬ髢薙□縺・true縲ょ・蜉帶峩譁ｰ縺ｮ縺溘・縺ｫ繝ｪ繧ｻ繝・ヨ縺吶ｋ縲・
        public bool PressedThisFrame { get; private set; }
        // 莉雁屓縺ｮ蜈･蜉帶峩譁ｰ縺ｧ縲∵款縺励※縺・◆謖・・繝懊ち繝ｳ繧帝屬縺励◆蝣ｴ蜷医□縺奏rue縲・
        public bool ReleasedThisFrame { get; private set; }
        // 蜑榊屓縺ｮ謖・・繝槭え繧ｹ縺ｮ逕ｻ髱｢蠎ｧ讓呻ｼ医ヴ繧ｯ繧ｻ繝ｫ・峨・
        private Vector2 previousPointer;
        // 謫堺ｽ應ｸｭ縺ｮ謖・ｒ隴伜挨縺吶ｋID縲・1縺ｯ繝槭え繧ｹ縲√∪縺溘・謫堺ｽ懊＠縺ｦ縺・↑縺・憾諷九・
        private int pointerId = -1;
        // 繧｢繝励Μ蠕ｩ蟶ｰ譎ゅ↑縺ｩ縺ｫ縲∵款縺励▲縺ｱ縺ｪ縺励・蜈･蜉帙ｒ荳蠎ｦ髮｢縺吶∪縺ｧ辟｡隕悶☆繧九・
        private bool ignoreUntilRelease;
        // 譛蠕後↓蜍輔＞縺滓凾蛻ｻ・医ご繝ｼ繝縺ｮ譎る俣蛟咲紫縺ｫ蠖ｱ髻ｿ縺輔ｌ縺ｪ縺・ｧ呈焚・峨・
        private float lastMovementTime;
        // 停止中に指を離した入力を、再開まで一度だけ保持する。
        private bool bufferedRelease;
        // 指を離した瞬間の速度。停止中の後続入力で上書きしない。
        private Vector3 bufferedFlickVelocity;
        // 蜈ｱ譛峨・繝・Μ繧｢繝ｫ繧貞､峨∴縺壹∝ｯｾ雎｡縺縺代・濶ｲ繧呈欠螳壹☆繧九◆繧√・繝・・繧ｿ縲・
        private MaterialPropertyBlock properties;

        // false縺ｮ蝣ｴ蜷医√・繝ｬ繧､繝､繝ｼ縺ｮ謫堺ｽ懊ｒ蜿励￠莉倥￠縺ｪ縺・
        // GameOver縺ｪ縺ｩ縺ｧ菴ｿ逕ｨ
        public bool CanMove { get; private set; } = true; // Playerの操作状態

        // タッチまたはマウス入力を読む。freezeMovement=trueでは座標だけ追跡し、停止中に移動しない。
        public void ReadInput(Camera camera, Rect bounds, float dt, bool freezeMovement = false)
        {
            // 操作禁止状態なら入力を受け付けない
            if (!CanMove)
            {
                bufferedRelease = false;
                bufferedFlickVelocity = Vector3.zero;
                Velocity = Vector3.zero;
                FlickVelocity = Vector3.zero;
                IsHeld = false;
                PressedThisFrame = false;
                ReleasedThisFrame = false;
                return;
            }

            // 莉雁屓縲∵桃菴懃畑縺ｮ謖・∪縺溘・繝槭え繧ｹ繝懊ち繝ｳ縺梧款縺輔ｌ縺ｦ縺・ｋ縺九・
            bool held = false;
            // 莉雁屓縺ｮ謖・・繝槭え繧ｹ縺ｮ逕ｻ髱｢蠎ｧ讓呻ｼ医ヴ繧ｯ繧ｻ繝ｫ・峨・
            Vector2 position = default;
            // 蜈･蜉帛・繧定ｭ伜挨縺吶ｋID縲ゅち繝・メ縺ｯ謖・・ID縲√・繧ｦ繧ｹ縺ｯ-1縲・
            int id = -1;
            // 迴ｾ蝨ｨ蛻ｩ逕ｨ縺ｧ縺阪ｋ繧ｿ繝・メ蜈･蜉帙ョ繝舌う繧ｹ縲ょｭ伜惠縺励↑縺・ｴ蜷医・null縲・
            var touchscreen = Touchscreen.current;
            // 譛蛻昴↓隗ｦ繧後◆謖・ｒ霑ｽ霍｡縺礼ｶ壹￠繧九る比ｸｭ縺ｧ蛻･縺ｮ謖・ｒ霑ｽ蜉縺励※繧ゆｽ咲ｽｮ縺碁｣帙・縺ｪ縺・ｈ縺・↓縺吶ｋ縲・
            if (touchscreen != null)
            {
                // touch・壻ｸ隕ｧ縺九ｉ蜿悶ｊ蜃ｺ縺励◆縲∽ｻ雁屓蜃ｦ逅・☆繧句ｯｾ雎｡縲・
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
            FeedPointer(held, position, id, camera, bounds, dt, freezeMovement);
        }

        // ============================================================
        // Player縺ｮ謫堺ｽ懷庄蜷ｦ繧貞､画峩
        // ============================================================
        public void SetCanMove(bool canMove)
        {
            CanMove = canMove;

            // 謫堺ｽ懃ｦ∵ｭ｢縺ｫ縺ｪ縺｣縺滓凾轤ｹ縺ｧ縲∝・蜉帛・逅・→遘ｻ蜍暮溷ｺｦ繧偵Μ繧ｻ繝・ヨ
            if (!CanMove)
            {
                bufferedRelease = false;
                bufferedFlickVelocity = Vector3.zero;
                IsHeld = false;
                PressedThisFrame = false;
                ReleasedThisFrame = false;
                Velocity = Vector3.zero;
                FlickVelocity = Vector3.zero;
                pointerId = -1;
            }
        }
        // 押下状態と画面座標から移動を求める。停止中は基準座標を更新し、指離しを復帰時まで保持する。
        public void FeedPointer(bool held, Vector2 position, int id, Camera camera, Rect bounds, float dt, bool freezeMovement = false)
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
                // 豁｢繧√※縺九ｉ髮｢縺励◆蝣ｴ蜷医・縲∝商縺・ｧｻ蜍暮溷ｺｦ縺ｧ謚輔￡縺ｪ縺・ｈ縺・↓縺吶ｋ・育幻莠・.1遘抵ｼ峨・
                if (!freezeMovement && Time.unscaledTime - lastMovementTime > 0.1f) FlickVelocity = Vector3.zero;
                // 止めている間の指離しは、復帰直後の投擲に引き継ぐ。
                if (freezeMovement && ReleasedThisFrame)
                {
                    bufferedRelease = true;
                    bufferedFlickVelocity = FlickVelocity;
                }
                return;
            }
            // 謚ｼ縺励◆譛蛻昴・繝輔Ξ繝ｼ繝縺ｯ蝓ｺ貅門ｺｧ讓吶・險倬鹸縺縺代ｒ陦後＞縲√・繝ｬ繧､繝､繝ｼ繧貞虚縺九＆縺ｪ縺・・
            if (!IsHeld)
            {
                IsHeld = true;
                PressedThisFrame = true;
                previousPointer = position;
                pointerId = id;
                FlickVelocity = Vector3.zero;
                return;
            }
            // 蜑榊屓・丈ｻ雁屓縺ｮ逕ｻ髱｢蠎ｧ讓吶°繧峨√・繝ｬ繧､繝､繝ｼ縺ｨ蜷後§鬮倥＆縺ｮ豌ｴ蟷ｳ髱｢縺ｸ繝ｬ繧､繧帝｣帙・縺吶・
            // 莠､轤ｹ縺ｮ蟾ｮ蛻・ｒ菴ｿ縺・◆繧√√き繝｡繝ｩ縺ｮ諡｡螟ｧ邇・ｄ逕ｻ髱｢繧ｵ繧､繧ｺ繧堤ｧｻ蜍暮㍼縺ｫ蜿肴丐縺ｧ縺阪ｋ縲・
            // 停止中は移動せず、復帰後の基準位置だけ追従させて座標の飛びを防ぐ。
            if (freezeMovement)
            {
                previousPointer = position;
                lastMovementTime = Time.unscaledTime;
                return;
            }
            var plane = new Plane(Vector3.up, transform.position);
            // 蜑榊屓縺ｮ逕ｻ髱｢蠎ｧ讓吶°繧画ｰｴ蟷ｳ髱｢縺ｸ鬟帙・縺吶Ξ繧､縲・
            Ray before = camera.ScreenPointToRay(previousPointer);
            // 莉雁屓縺ｮ逕ｻ髱｢蠎ｧ讓吶°繧画ｰｴ蟷ｳ髱｢縺ｸ鬟帙・縺吶Ξ繧､縲・
            Ray after = camera.ScreenPointToRay(position);
            previousPointer = position;
            // a/b縺ｯ蜑榊屓・丈ｻ雁屓縺ｮ繝ｬ繧､縺梧ｰｴ蟷ｳ髱｢縺ｫ螻翫￥縺ｾ縺ｧ縺ｮ霍晞屬縲ゆｺ､轤ｹ繧呈ｱゅａ繧九◆繧√↓菴ｿ縺・・
            if (!plane.Raycast(before, out float a) || !plane.Raycast(after, out float b)) return;
            // 遘ｻ蜍募・逅・ｒ蟋九ａ繧句燕縺ｮ繝励Ξ繧､繝､繝ｼ菴咲ｽｮ縲・
            Vector3 oldPosition = transform.position;
            // 繝峨Λ繝・げ驥上ｒ蜉縺医◆遘ｻ蜍募・縲ょｾ後〒遘ｻ蜍募庄閭ｽ遽・峇縺ｫ蜿弱ａ繧九・
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

        // 菴咲ｽｮ縺ｨ蜈･蜉帛ｱ･豁ｴ繧貞・譛溷喧縺吶ｋ縲ＸaitForRelease=true縺ｪ繧峨∵ｬ｡縺ｮ謚ｼ縺礼峩縺励∪縺ｧ謫堺ｽ懊ｒ蜿励￠莉倥￠縺ｪ縺・・
        public void ResetPlayer(Vector3 position, bool waitForRelease = false)
        {
            bufferedRelease = false;
            bufferedFlickVelocity = Vector3.zero;
            transform.position = position;
            IsHeld = PressedThisFrame = ReleasedThisFrame = false;
            Velocity = FlickVelocity = Vector3.zero;
            pointerId = -1;
            ignoreUntilRelease = waitForRelease;
            SetColor(new Color(0.12f, 0.8f, 1f));
        }

        // 蜈ｱ譛峨・繝・Μ繧｢繝ｫ繧定､・｣ｽ縺帙★縲√％縺ｮ繝励Ξ繧､繝､繝ｼ縺縺代・陦ｨ遉ｺ濶ｲ繧貞､峨∴繧九・
        public void SetColor(Color color)
        {
            if (body == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            body.SetPropertyBlock(properties);
        }

        // 蛻･繧｢繝励Μ縺ｸ縺ｮ蛻・ｊ譖ｿ縺域凾縺ｫ蜈･蜉帙ｒ隗｣髯､縺励∝ｾｩ蟶ｰ蠕後・隱､遘ｻ蜍輔・隱､謚墓憧繧帝亟縺舌・
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ResetPlayer(transform.position, true);
        }
        // 繧ｹ繝槭・繝医ヵ繧ｩ繝ｳ縺ｮ荳譎ょ●豁｢縺ｧ繧ょ酔讒倥↓蜈･蜉帛ｱ･豁ｴ繧堤ｴ譽・☆繧九・
        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetPlayer(transform.position, true);
        }
        // 停止中の投擲予約を取り出す。一度取り出した予約は次のフレームに持ち越さない。
        public bool TryConsumeBufferedRelease(out Vector3 flickVelocity)
        {
            flickVelocity = bufferedFlickVelocity;
            // 消去前の予約状態を戻り値として保存する。
            bool hadRelease = bufferedRelease;
            bufferedRelease = false;
            return hadRelease;
        }
    }
}
