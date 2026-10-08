using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype
{
    // キューのレベルに応じた弾消しPrefabを、衝突位置と進行方向に合わせて再生する。
    public sealed class BulletEraseEffectPlayer : MonoBehaviour
    {
        // レベル1のキューで敵弾を消したときに使う演出。
        [Header("弾消し：キューレベル1のエフェクト")]
        [Tooltip("EF_Bullet_Dest/fixのLv1 Prefabを指定します。敵弾のレベルではなくキューのレベルで選びます。")]
        [SerializeField] private GameObject level1Prefab;
        // レベル2のキューで敵弾を消したときに使う演出。
        [Header("弾消し：キューレベル2のエフェクト")]
        [Tooltip("EF_Bullet_Dest/fixのLv2 Prefabを指定します。")]
        [SerializeField] private GameObject level2Prefab;
        // レベル3のキューで敵弾を消したときに使う演出。
        [Header("弾消し：キューレベル3のエフェクト")]
        [Tooltip("EF_Bullet_Dest/fixのLv3 Prefabを指定します。")]
        [SerializeField] private GameObject level3Prefab;
        // 元のPrefabに掛ける表示倍率。火花・閃光をまとめて調整する。
        [Header("弾消し：エフェクト全体の大きさ")]
        [Tooltip("1で元のPrefabと同じ大きさです。")]
        [SerializeField, Min(0.01f)] private float sizeMultiplier = 1f;
        // 同時に表示できる弾消しの数。大量の弾を消しても負荷が増え続けないようにする。
        [Header("弾消し：同時表示数の上限")]
        [Tooltip("上限を超えたら古い演出を消し、新しい命中を優先して表示します。")]
        [SerializeField, Min(1)] private int maximumEffects = 24;

        // 再生したPrefabと、その子パーティクルをまとめて管理する。
        private sealed class PlayingEffect
        {
            // シーンに生成した演出のルート。
            public GameObject root;
            // 再生終了を判定する、全ての子パーティクル。
            public ParticleSystem[] particles;
            // 不正なPrefab設定でも残り続けないようにする経過時間。
            public float elapsed;
        }
        // 表示中の演出を古い順に保持する。
        private readonly List<PlayingEffect> playing = new List<PlayingEffect>();

        // 位置と方向はワールド座標。Prefab未設定時だけfalseを返し、既存演出へ戻せるようにする。
        public bool TryPlay(int cueLevel, Vector3 position, Vector3 direction)
        {
            // キューのレベルで演出を選択し、範囲外は1～3へ丸める。
            GameObject prefab = Mathf.Clamp(cueLevel, 1, 3) == 1 ? level1Prefab : cueLevel == 2 ? level2Prefab : level3Prefab;
            if (prefab == null || !isActiveAndEnabled) return false;
            while (playing.Count >= Mathf.Max(1, maximumEffects)) RemoveAt(0);
            // 提供Prefabの火花はローカル+Z方向へ放射されるため、+Zを打ち消し方向に合わせる。
            Vector3 forward = ResolveDirection(direction, Vector3.zero, Vector3.zero, Vector3.zero);
            // キューの移動に追従させず、衝突した場所に演出を残す。
            GameObject instance = Instantiate(prefab, position, Quaternion.LookRotation(forward, Vector3.up));
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
            instance.transform.localScale *= Mathf.Max(0.01f, sizeMultiplier);
            // 非表示の子も含めて設定し、最初から再生し直す。
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            // 生成直後からドット化対象外にするため、描画オブジェクトにレイヤーを設定する。
            int effectLayer = LayerMask.NameToLayer(EffectLayerScope.LayerName);
            // メッシュで作られた子演出もまとめて対象にする。
            foreach (Renderer visual in instance.GetComponentsInChildren<Renderer>(true))
                if (effectLayer >= 0) visual.gameObject.layer = effectLayer;
            // 元Prefabは変更せず、今回生成したパーティクルの再生設定だけを調整する。
            foreach (ParticleSystem system in systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                // 火花が発射後も同じワールド方向へ飛ぶようにする。
                ParticleSystem.MainModule main = system.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.None;
                // 拡大率を全パーティクルへ揃えて反映する。
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            // 親のPlayによる二重再生を避け、各システムを一度ずつ開始する。
            foreach (ParticleSystem system in systems)
                if (system.gameObject.activeInHierarchy) system.Play(false);
            playing.Add(new PlayingEffect { root = instance, particles = systems });
            return true;
        }

        // キューの実移動を優先する。静止時は速度、敵弾の逆向き、接触方向の順で補う。
        public static Vector3 ResolveDirection(Vector3 cueMovement, Vector3 cueVelocity, Vector3 bulletVelocity, Vector3 contactOffset)
        {
            cueMovement.y = cueVelocity.y = bulletVelocity.y = contactOffset.y = 0f;
            if (cueMovement.sqrMagnitude > 0.000001f) return cueMovement.normalized;
            if (cueVelocity.sqrMagnitude > 0.000001f) return cueVelocity.normalized;
            if (bulletVelocity.sqrMagnitude > 0.000001f) return -bulletVelocity.normalized;
            return contactOffset.sqrMagnitude > 0.000001f ? contactOffset.normalized : Vector3.forward;
        }

        // 火花と残光が全て終了した演出を片付ける。
        private void Update()
        {
            // 削除しても未処理の要素を飛ばさないよう、後ろから確認する。
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                // 今回確認する演出と、再生中の粒子が残っているかの印。
                PlayingEffect effect = playing[i];
                // 異常な長時間設定への安全上限は30秒とする。
                bool alive = false;
                effect.elapsed += Time.deltaTime;
                // 遅延再生や子の残光も含めて終了を判定する。
                foreach (ParticleSystem system in effect.particles)
                    if (system != null && system.IsAlive(false)) alive = true;
                if (!alive || effect.root == null || effect.elapsed > 30f) RemoveAt(i);
            }
        }

        // 指定した演出を即座に非表示にしてから破棄する。
        private void RemoveAt(int index)
        {
            if (playing[index].root != null) { playing[index].root.SetActive(false); Destroy(playing[index].root); }
            playing.RemoveAt(index);
        }

        // リトライやシーン終了時に、前のラウンドの演出を残さない。
        public void Clear() { while (playing.Count > 0) RemoveAt(playing.Count - 1); }

        // 管理元が無効になった際も、独立した演出オブジェクトを片付ける。
        private void OnDisable() => Clear();
    }
}
