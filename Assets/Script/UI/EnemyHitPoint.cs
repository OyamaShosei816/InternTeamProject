using UnityEngine;
using UnityEngine.UI;

public class EnemyHitPoint : MonoBehaviour
{
    public Slider hpSlider; // スライダー UI の参照
    [SerializeField]
    private float maxHp = 100; // 体力の最大値
    private float currentHp; // 現在の体力
    public Image fillArea; // スライダーのフィルエリア

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHp = maxHp; // ゲーム開始時に現在の体力を最大値に設定
        hpSlider.maxValue = maxHp; // スライダーの最大値を設定
        hpSlider.value = currentHp; // スライダーの初期値を現在の体力に設定
        //fillArea.color = Color.green; // HPバー初期カラー
    }

    // 攻撃ヒット時にボスのHPを減少
    public void TakeDamage(float damage)
    {
        currentHp -= damage; // 指定されたダメージ分、体力を減少させる
        currentHp = Mathf.Clamp(currentHp, 0, maxHp); // 体力が 0 を下回らず、最大値を超えないように制限
        hpSlider.value = currentHp; // スライダー UI に反映
        //ChangeColor(currentHp); // HPバーの色変更
    }

    // HP残量に応じてバーの色を変更する
    void ChangeColor(float value)
    {
        fillArea.color = Color.Lerp(Color.red, Color.green, value / hpSlider.maxValue);
    }
}
