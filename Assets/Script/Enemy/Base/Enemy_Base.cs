using UnityEngine;
using UnityEngine.UI;

public class Enemy_Base : MonoBehaviour
{
    [Header("HP")]
    [SerializeField, Min(1f)] private float maxHP = 100.0f;

    [Header("UI")]
    [SerializeField] private UnityEngine.UI.Slider hpSlider;

    // 現在のHP
    private float currentHP;
    // 死亡判定
    private bool isDead;

    public float CurrentHP => currentHP;

    protected virtual void Awake()
    {
        currentHP = maxHP;
        isDead = false;

        if(hpSlider != null)
        {
            hpSlider.minValue = 0.0f;
            hpSlider.maxValue = maxHP;
            hpSlider.wholeNumbers = false;

            hpSlider.interactable = false;
            hpSlider.transition = Selectable.Transition.None;
        }

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // 何ダメージ受けたかを引数で渡して
    // エネミーのHPに反映させる
    public void TakeDamage(float damage)
    {
        if(isDead || damage <= 0.0f)
        {
            return;
        }

        currentHP = Mathf.Clamp(currentHP - damage, 0.0f, maxHP);

        // 受けたダメージをUIに反映させる
        UpdateHPUI();

        if(currentHP <= 0.0f)
        {
            isDead = true;
            Die();
        }
    }

    // エネミーの状態をUIに反映する
    private void UpdateHPUI()
    {
        if (hpSlider == null)
        {
            return;
        }

        // UI更新時に、ゲージの範囲もEnemyのHPとそろえる。
        hpSlider.minValue = 0.0f;
        hpSlider.maxValue = maxHP;
        hpSlider.value = currentHP;
    }

    // 死亡処理
    protected virtual void Die()
    {
        Destroy(gameObject);
    }

    // ダメ確認用関数
    [ContextMenu("確認用：10ダメージ")]
    private void TestDamage()
    {

        if (!Application.isPlaying)
        {
            return;
        }

        float beforeHP = currentHP;
        TakeDamage(10.0f);

    }

}
