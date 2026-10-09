using Prototype;
using UnityEngine;

public class PlayerStock : MonoBehaviour
{
    public PrototypeArena prototypeArena;
    [SerializeField] public GameObject[] stockObjects;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //prototypeArena.DamagePlayer();
    }

    // Update is called once per frame
    void Update()
    {
        stockObjects[0].SetActive(false);
    }
}
