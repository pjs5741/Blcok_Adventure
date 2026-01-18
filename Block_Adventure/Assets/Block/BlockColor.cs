using UnityEngine;

public class BlockColor : MonoBehaviour
{
    public int colorID = 0; // 0:무속성, 1~N: 색상 ID

    // 외부(스포너)에서 "너 이 색깔 해!" 라고 명령 내리는 함수
    public void SetColorInfo(int id, Color color)
    {
        this.colorID = id;

        // 내 몸뚱이(스프라이트) 색깔도 실제로 바꿈
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = color;
        }
    }
}