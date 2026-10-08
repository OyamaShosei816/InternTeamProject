using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // Effectシーン内の演出を、ドット処理後に描画する専用レイヤーへ振り分ける。
    [DefaultExecutionOrder(10000)]
    public sealed class EffectLayerScope : MonoBehaviour
    {
        // Tags and Layersに登録する、ドット化しない演出用レイヤー名。
        public const string LayerName = "Effect";

        // メッシュなど、自動判別できない演出の親オブジェクト一覧。
        [Header("追加のエフェクト：強攻撃・光・メッシュ演出の親を登録")]
        [Tooltip("ここに登録したオブジェクトと子のRendererをドット化から除外します。プレイヤー本体や背景は登録しないでください。パーティクル・軌跡・線は自動で対象になります。")]
        [SerializeField] private GameObject[] additionalEffects = new GameObject[0];

        // シーンのルートを再利用して取得し、毎フレームの配列生成を避ける。
        private readonly List<GameObject> roots = new List<GameObject>();
        // 各階層の描画コンポーネントを集める再利用バッファ。
        private readonly List<Renderer> renderers = new List<Renderer>();

        // 有効化時に、非表示の演出も含めて最初のレイヤー設定を行う。
        private void OnEnable() => RefreshLayers();

        // 通常のゲーム更新後に、新しく生成された演出やプールから戻った演出も分類する。
        private void LateUpdate() => RefreshLayers();

        // このコンポーネントが置かれたシーンだけを調べ、演出の描画オブジェクトを分類する。
        public void RefreshLayers()
        {
            // 未登録のレイヤー番号をGameObjectへ設定しないための存在確認。
            int effectLayer = LayerMask.NameToLayer(LayerName);
            if (effectLayer < 0 || !gameObject.scene.IsValid()) return;
            gameObject.scene.GetRootGameObjects(roots);
            // 別シーンのモデルや演出には設定を波及させない。
            foreach (GameObject root in roots)
            {
                root.GetComponentsInChildren(true, renderers);
                // 種類が明確なパーティクル・軌跡・線だけを自動分類する。
                foreach (Renderer visual in renderers)
                    if (visual is ParticleSystemRenderer || visual is TrailRenderer || visual is LineRenderer)
                        visual.gameObject.layer = effectLayer;
            }
            // メッシュ演出は、プランナーが明示的に登録した階層だけを対象にする。
            foreach (GameObject effect in additionalEffects)
            {
                if (effect == null || effect.scene != gameObject.scene) continue;
                effect.GetComponentsInChildren(true, renderers);
                // 子のモデルまで一律に変更せず、指定した演出階層の描画物だけを変更する。
                foreach (Renderer visual in renderers) visual.gameObject.layer = effectLayer;
            }
        }
    }
}
