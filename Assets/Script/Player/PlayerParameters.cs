using System;
using UnityEngine;

namespace Prototype
{
    // キャラクターごとの基本性能と、将来のパッシブスキルによる補正を管理する。
    // プレイヤーのInspectorに保存する基本倍率 × 実行中のパッシブ倍率で最終性能を求める。
    [Serializable]
    public sealed class PlayerParameters
    {
        // パリィ専用の判定半径の倍率。通常の被弾判定の大きさは変えない。
        [Header("パリィ：成功判定の広さ（倍率）")]
        [Tooltip("1が標準。2ならプレイヤー側のパリィ半径が2倍になります。受付時間は変わりません。")]
        [SerializeField, Min(0.01f)] private float parryRange = 1f;

        // 水風船が回る速さと投げた際の速さに使う基本倍率。
        [Header("キュー：公転・投擲の速さ（倍率）")]
        [Tooltip("1が標準。大きいほど水風船が速く回り、投げた際の速度上限とフリック加速も上がります。")]
        [SerializeField, Min(0.01f)] private float cueSpeed = 1f;

        // 強化用の回転角度を蓄積する速さ。実際の回転速度とは別に調整できる。
        [Header("キュー：強さが育つ速さ（倍率）")]
        [Tooltip("1が標準。2なら同じ実回転数で強化の進捗が2倍です。移動しながら回す必要があります。")]
        [SerializeField, Min(0.01f)] private float cueGrowth = 1f;

        // 弱点に命中した際のダメージに掛ける基本倍率。
        [Header("攻撃力：弱点ダメージ（倍率）")]
        [Tooltip("1が標準。2なら同じ投擲速度でダメージが2倍です。ボス胴体への命中には効果がありません。")]
        [SerializeField, Min(0.01f)] private float attack = 1f;

        // 再出現待ちのタイマーを進める速さ。2倍なら待ち時間は半分になる。
        [Header("キュー：再生産の速さ（倍率）")]
        [Tooltip("1が標準。2なら水風船が戻るまでの待ち時間が半分です。飛行の寿命は変わりません。")]
        [SerializeField, Min(0.01f)] private float cueReproduction = 1f;

        // 敵弾を消す判定とボスへの命中判定に使う水風船の半径の倍率。
        [Header("キュー：当たり範囲の広さ（半径倍率）")]
        [Tooltip("1が標準。2なら水風船の判定半径と見た目の大きさが2倍です。紐には判定を追加しません。")]
        [SerializeField, Min(0.01f)] private float cueHitRange = 1f;

        // パッシブスキルから渡されるパリィ範囲の合計補正。保存する基本値とは分離する。
        [NonSerialized] private float passiveParryRange = 1f;
        // パッシブスキルから渡される水風船速度の合計補正。
        [NonSerialized] private float passiveCueSpeed = 1f;
        // パッシブスキルから渡される強化進捗の合計補正。
        [NonSerialized] private float passiveCueGrowth = 1f;
        // パッシブスキルから渡される攻撃力の合計補正。
        [NonSerialized] private float passiveAttack = 1f;
        // パッシブスキルから渡される再生産速度の合計補正。
        [NonSerialized] private float passiveCueReproduction = 1f;
        // パッシブスキルから渡される水風船の判定半径の合計補正。
        [NonSerialized] private float passiveCueHitRange = 1f;

        // 現在のパリィ範囲倍率。スキル変更時も取得のたびに最新の値を使う。
        public float ParryRange => SafeMultiplier(parryRange) * SafeMultiplier(passiveParryRange);
        // 現在の水風船速度倍率。
        public float CueSpeed => SafeMultiplier(cueSpeed) * SafeMultiplier(passiveCueSpeed);
        // 現在の強化進捗倍率。
        public float CueGrowth => SafeMultiplier(cueGrowth) * SafeMultiplier(passiveCueGrowth);
        // 現在の攻撃力倍率。
        public float Attack => SafeMultiplier(attack) * SafeMultiplier(passiveAttack);
        // 現在の再生産速度倍率。待ち時間には逆数として効く。
        public float CueReproduction => SafeMultiplier(cueReproduction) * SafeMultiplier(passiveCueReproduction);
        // 現在の水風船の当たり判定半径倍率。
        public float CueHitRange => SafeMultiplier(cueHitRange) * SafeMultiplier(passiveCueHitRange);

        // 将来のスキル管理から、全装備スキルを集計した6種類の倍率を渡す入口。
        // 加算は行わず、以前の補正を置き換える。外したスキルの補正が残ることを防ぐ。
        // 各引数は同名の性能への倍率。省略した引数は補正なし（1倍）になる。
        public void SetPassiveMultipliers(
            // パリィ判定の半径へ掛ける補正倍率。
            float parryRange = 1f,
            // 水風船の公転と投擲速度へ掛ける補正倍率。
            float cueSpeed = 1f,
            // 回転で蓄積する強化進捗へ掛ける補正倍率。
            float cueGrowth = 1f,
            // 弱点へのダメージへ掛ける補正倍率。
            float attack = 1f,
            // 再出現待ちのタイマーの進行速度へ掛ける補正倍率。
            float cueReproduction = 1f,
            // 水風船の当たり判定半径へ掛ける補正倍率。
            float cueHitRange = 1f)
        {
            passiveParryRange = SafeMultiplier(parryRange);
            passiveCueSpeed = SafeMultiplier(cueSpeed);
            passiveCueGrowth = SafeMultiplier(cueGrowth);
            passiveAttack = SafeMultiplier(attack);
            passiveCueReproduction = SafeMultiplier(cueReproduction);
            passiveCueHitRange = SafeMultiplier(cueHitRange);
        }

        // パッシブ補正をすべて1倍に戻す。Inspectorで設定したキャラクターの基本性能は保持する。
        public void ResetPassiveMultipliers()
        {
            SetPassiveMultipliers();
        }

        // valueは設定する倍率。無効な数値は1倍、0以下は0.01倍として計算破綻を防ぐ。
        private static float SafeMultiplier(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(0.01f, value);
        }
    }
}
