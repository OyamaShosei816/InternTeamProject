using UnityEngine;
using UnityEngine.UI;

public class ContinueButton : MonoBehaviour
{
    // オプション画面オブジェクト
    public GameObject optionScreen;

    // オプション画面の表示非表示を切り替えるメソッド
    public void GameContinue()
    {
        // オプション画面を非表示
        optionScreen.SetActive(false);
    }
}
