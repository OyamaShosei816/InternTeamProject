using Prototype;
using System;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class QueIImage : MonoBehaviour
{
    private WaterBalloon balloon;

    [SerializeField] private Sprite[] queImages;

    //[SerializeField] private Sprite targetImage;
    [SerializeField] private Image target;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject targetObj = GameObject.Find("WaterBalloon");
        balloon = targetObj.GetComponent<WaterBalloon>();

        target.sprite = queImages[balloon.Power];


    }

    // Update is called once per frame
    void Update()
    {
        target.sprite = queImages[balloon.Power];
        //power = balloon.Power;
        Debug.Log(balloon.Power);
    }
}
