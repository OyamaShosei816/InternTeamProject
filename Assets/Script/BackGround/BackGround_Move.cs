using UnityEngine;


[RequireComponent (typeof(MeshRenderer))]
public class BackGround_Move : MonoBehaviour
{

    [Header("スクロール速度")]
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.0f, 0.1f);

    // 背景のマテリアル
    private Material backGroundMaterial;
    // 動かす際のオフセット
    private Vector2 offset;

    private void Awake()
    {
        // 背景のマテリアルを取得
        backGroundMaterial = GetComponent<MeshRenderer>().material;
        // マテリアルのOffsetを取得
        offset = backGroundMaterial.mainTextureOffset;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // スクロール速度*デルタタイムで制御する
        offset += scrollSpeed * Time.deltaTime;

        // Offset値が増大しないよう、1.0fに戻す
        offset.x = Mathf.Repeat(offset.x, 1.0f);
        offset.y = Mathf.Repeat(offset.y, 1.0f);

        // 作ったOffset値をマテリアルに適用
        backGroundMaterial.mainTextureOffset = offset;
    }

    private void OnDestroy()
    {
        // 専用マテリアルの解放
        if (backGroundMaterial != null)
        {
            Destroy(backGroundMaterial);
        }
    }
}
