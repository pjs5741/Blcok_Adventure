using UnityEngine;
using UnityEngine.UI;

//--- 2026-07-01 보상 카드 = 유희왕풍. 위: 색 입힌 블록 모양(UI 셀, 천천히 90도 회전) / 아래: 효과 이름.
// (구) 월드 프리팹을 UI에 얹던 방식은 단위 혼용으로 설명칸을 침범 → ShapeMiniView(UI 셀, 앵커 영역 고정)로 교체.
// 아이콘 이미지 미사용(막판 리소스 작업). 카드 위 호버 시 효과 툴팁. 색은 카드에서 확정되어 덱에 들어감.
public class RewardCard : MonoBehaviour
{
    public Text blockNameText;

    [Header("연출")]
    public float rotateInterval = Tuning.RewardRotateInterval;  // 90도씩 돌리는 간격(초)

    private GameObject _blockPrefab;
    private int _colorID;
    private RectTransform _artHost;      // 블록 모양(UI 셀)을 담는 영역 — 설명칸 위에 고정
    private Text _descName;
    private TooltipTarget _tooltip;
    private int _quarter;                // 현재 90도 단계
    private float _rotTimer;

    void OnEnable()
    {
        if (blockNameText == null)
            blockNameText = GetComponentInChildren<Text>();
        GetComponent<Button>().onClick.RemoveListener(OnClick);
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    public void Setup(GameObject prefab, int colorID)
    {
        _blockPrefab = prefab;
        _colorID = colorID;
        if (blockNameText != null) blockNameText.text = "";

        BuildUI();

        // 아트: 블록 모양을 UI 셀로 그림 (앵커 영역 안에 고정 → 설명칸 침범 불가, 이미지 미사용)
        foreach (Transform c in _artHost) Destroy(c.gameObject);
        ShapeMiniView.Build(_artHost, prefab.name, colorID, 95f);
        _quarter = 0; _rotTimer = 0f;
        _artHost.localRotation = Quaternion.identity;

        _descName.text = BlockColors.Name(colorID);
        _tooltip.title = $"{BlockColors.Name(colorID)} 블록";
        _tooltip.body = BlockColors.Desc(colorID);
    }

    // 아트 영역(상단) + 설명칸(하단) 1회 생성
    void BuildUI()
    {
        if (_tooltip == null) _tooltip = gameObject.GetComponent<TooltipTarget>() ?? gameObject.AddComponent<TooltipTarget>();
        if (_descName != null) return;

        // 블록 모양 영역 (설명칸 위쪽, 겹치지 않게 분리)
        var artGO = NewUI("Art", transform, typeof(RectTransform));
        _artHost = (RectTransform)artGO.transform;
        _artHost.anchorMin = new Vector2(0.12f, 0.42f);
        _artHost.anchorMax = new Vector2(0.88f, 0.95f);
        _artHost.offsetMin = Vector2.zero; _artHost.offsetMax = Vector2.zero;

        // 설명칸 배경 (하단)
        var box = NewUI("DescBox", transform, typeof(RectTransform), typeof(Image));
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = new Vector2(0.06f, 0.04f);
        boxRt.anchorMax = new Vector2(0.94f, 0.34f);
        boxRt.offsetMin = Vector2.zero; boxRt.offsetMax = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 0.95f);

        // 효과 이름 (설명칸 전체 폭 — 아이콘 이미지 제거)
        var nameGO = NewUI("EffectName", box.transform, typeof(RectTransform), typeof(Text));
        var nameRt = (RectTransform)nameGO.transform;
        nameRt.anchorMin = Vector2.zero; nameRt.anchorMax = Vector2.one;
        nameRt.offsetMin = Vector2.zero; nameRt.offsetMax = Vector2.zero;
        _descName = nameGO.GetComponent<Text>();
        _descName.font = UIFont.Regular;
        _descName.fontSize = 40;
        _descName.alignment = TextAnchor.MiddleCenter;
        _descName.color = Color.white;
        _descName.horizontalOverflow = HorizontalWrapMode.Wrap;
        _descName.raycastTarget = false;
    }

    GameObject NewUI(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, comps);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    void Update()
    {
        if (_artHost == null || _artHost.childCount == 0) return;
        _rotTimer += Time.deltaTime;
        if (_rotTimer >= rotateInterval)
        {
            _rotTimer -= rotateInterval;
            _quarter = (_quarter + 1) % 4;
            _artHost.localRotation = Quaternion.Euler(0f, 0f, 90f * _quarter);   // 영역 중심 기준 90도 회전(영역 안 유지)
        }
    }

    void OnClick()
    {
        RewardManager.Instance.OnCardSelected(_blockPrefab, _colorID);
    }
}
