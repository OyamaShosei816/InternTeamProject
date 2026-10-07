using UnityEngine;
using UnityEngine.UI;

public class OptionButton : MonoBehaviour
{
    public GameObject optionScreen; // オプション画面オブジェクト

    public Button optionButton; // ボタンイベント取得

    bool optionActive = false; // オプション画面 表示非表示フラグ

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 開始時はオプション画面非表示
        optionScreen.SetActive(optionActive);
    }

    // オプション画面の表示非表示を切り替えるメソッド
    public void OptionOpen()
    {
        // オプション画面のアクティブ状態を取得して表示状態を切り替え
        optionActive = optionScreen.activeSelf ? false : true;
        optionScreen.SetActive(optionActive);
    }
}
