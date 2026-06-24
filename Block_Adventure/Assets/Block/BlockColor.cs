using UnityEngine;

public class BlockColor : MonoBehaviour
{
    public int colorID = 0; // 0:비어있음, 1~5: 아이콘 ID, 99:회색 garbage

    // 옛 호환
    public void SetColorInfo(int id, Color color)
    {
        SetIconInfo(id, color, null);
    }

    // 배경색 + 아이콘 오버레이 (P&D 스타일)
    public void SetIconInfo(int id, Color bgColor, Sprite iconSprite)
    {
        this.colorID = id;

        SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
        if (mainSr != null) mainSr.color = bgColor;

        Transform iconChild = transform.Find("IconOverlay");

        if (iconSprite == null)
        {
            if (iconChild != null) Destroy(iconChild.gameObject);
            return;
        }

        if (iconChild == null)
        {
            GameObject iconGo = new GameObject("IconOverlay", typeof(SpriteRenderer));
            iconGo.transform.SetParent(transform, false);
            iconGo.transform.localScale = Vector3.one * 0.75f;
            iconGo.transform.localPosition = new Vector3(0, 0, -0.01f); // 살짝 앞으로
            iconChild = iconGo.transform;
        }

        SpriteRenderer iconSr = iconChild.GetComponent<SpriteRenderer>();
        iconSr.sprite = iconSprite;
        iconSr.color = Color.white;
        if (mainSr != null) iconSr.sortingOrder = mainSr.sortingOrder + 1;
    }
}