using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    public float time; // 計測時間
    public Text timeText; // 表示する時間

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        time += Time.deltaTime;
        timeText.text = time.ToString("F2");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
