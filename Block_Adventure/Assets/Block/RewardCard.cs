using UnityEngine;
using UnityEngine.UI;

public class RewardCard : MonoBehaviour
{
    public Text blockNameText;
    private GameObject _blockPrefab;
    private GameObject _preview;

    void OnEnable()
    {
        if (blockNameText == null)
            blockNameText = GetComponentInChildren<Text>();
        GetComponent<Button>().onClick.RemoveListener(OnClick);
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    public void Setup(GameObject prefab)
    {
        _blockPrefab = prefab;
        if (blockNameText != null)
            blockNameText.text = "";

        DestroyPreview();

        _preview = Instantiate(prefab, transform);
        float compensated = 1.2f / transform.lossyScale.x;
        _preview.transform.localScale = Vector3.one * compensated;

        // 블록 셀 평균 위치로 중앙 정렬
        Vector3 childCenter = Vector3.zero;
        int count = 0;
        foreach (Transform child in _preview.transform) { childCenter += child.localPosition; count++; }
        if (count > 0) childCenter /= count;
        _preview.transform.localPosition = new Vector3(-childCenter.x * compensated, -childCenter.y * compensated, -0.1f);

        if (_preview.TryGetComponent(out BlockMovement bm)) Destroy(bm);
    }

    void OnDisable() => DestroyPreview();

    void DestroyPreview()
    {
        if (_preview != null) { Destroy(_preview); _preview = null; }
    }

    void OnClick()
    {
        RewardManager.Instance.OnCardSelected(_blockPrefab);
    }
}
