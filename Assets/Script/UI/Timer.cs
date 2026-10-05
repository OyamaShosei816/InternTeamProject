using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    public float time; // 計測時間
    public Text timeText; // 表示する時間

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        time = 0; // 開始時間
    }

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime; // 時間を加算
        timeText.text = time.ToString("F2"); // 時間を画面に表示
    }
}
