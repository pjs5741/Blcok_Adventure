using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;

    [Header("UI")]
    public GameObject rewardSelectPanel;
    public GameObject cardSelectPanel;
    public RewardCard[] rewardCards;
    public Button blockCardButton;
    public Button relicButton;
    public Button skipButton;

    [Header("보상 블록 목록 (빈 경우 Resources/Block 전체 사용)")]
    public GameObject[] rewardBlockPrefabs;

    private bool _isProceeded;
    private RelicHolder relicHolder;
    private Relic _pendingRelic;   // 이번 보상에서 줄 유물 (엘리트/보스에서만, 중복 제외하고 미리 뽑음)

    void Awake()
    {
        Instance = this;
        Init();

        blockCardButton.onClick.AddListener(ClickBlockButton);
        relicButton.onClick.AddListener(ClickRelicButton);
        skipButton.onClick.AddListener(ClickSkipButton);

        cardSelectPanel.SetActive(false);
        rewardSelectPanel.SetActive(false);
    }

    private void Init()
    {
        if (cardSelectPanel == null)
            cardSelectPanel = transform.Find("CardSelectPanel").gameObject;
        if (rewardSelectPanel == null)
            rewardSelectPanel = transform.Find("RewardSelectPanel").gameObject;
        if (rewardCards == null || rewardCards.Length == 0)
            rewardCards = cardSelectPanel.GetComponentsInChildren<RewardCard>(true);
        if (rewardBlockPrefabs == null || rewardBlockPrefabs.Length == 0)
            rewardBlockPrefabs = Resources.LoadAll<GameObject>("RewardBlock");
        if (blockCardButton == null)
            blockCardButton = rewardSelectPanel.transform.Find("BlockCardButton").GetComponent<Button>();
        if (relicButton == null)
            relicButton = rewardSelectPanel.transform.Find("RelicButton").GetComponent<Button>();
        if (skipButton == null)
            skipButton = rewardSelectPanel.transform.Find("SkipButton").GetComponent<Button>();
        if (relicHolder == null)
            relicHolder = FindFirstObjectByType<RelicHolder>();
    }

    public IEnumerator ShowReward(Monster monster = null)
    {
        _isProceeded = false;

        //--- 2026-07-06 골드 보상: 눈에 보이게 지급(조용히 안 올림)
        if (monster != null)
        {
            int g = monster.GoldReward;
            Run.stats.gold += g;
            ShowGoldReward(g);
        }

        //--- 2026-06-30 카드마다 모양(몬스터별 가중치 독립추첨) + 색(효과)을 함께 확정. 색은 카드에 고정되어 덱에 들어감.
        var shapes = monster != null ? monster.RewardShapes : null;
        for (int i = 0; i < rewardCards.Length; i++)
        {
            GameObject prefab = RollShape(shapes);
            int colorID = Random.Range(BlockColors.MinEffect, BlockColors.MaxEffect + 1);
            if (prefab != null) rewardCards[i].Setup(prefab, colorID);
        }

        //--- 2026-06-23 유물은 엘리트/보스 노드에서만, 종류당 1개(중복 제외). 다 모았으면 버튼 X
        var node = Run.mapState?.GetNode(Run.mapState.currentNodeId);
        bool relicNode = node != null && (node.type == NodeType.Elite || node.type == NodeType.Boss);
        _pendingRelic = relicNode ? RelicRegistry.GetRandomExcluding(Run.ownedRelics) : null;

        blockCardButton.gameObject.SetActive(true);
        relicButton.gameObject.SetActive(_pendingRelic != null);
        skipButton.gameObject.SetActive(true);
        RealignButtons();

        gameObject.SetActive(true);
        rewardSelectPanel.SetActive(true);

        yield return new WaitUntil(() => _isProceeded);

        cardSelectPanel.SetActive(false);
        rewardSelectPanel.SetActive(false);
        gameObject.SetActive(false);
    }

    //--- 2026-07-06 보상창에 골드 획득 표시
    void ShowGoldReward(int amount)
    {
        if (rewardSelectPanel == null) return;
        var t = rewardSelectPanel.transform.Find("GoldReward")?.GetComponent<Text>();
        if (t == null)
        {
            t = UIBuilder.Text(rewardSelectPanel.transform, "GoldReward", "", 40, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
            UIBuilder.SetAnchors(t.rectTransform, new Vector2(0.2f, 0.8f), new Vector2(0.8f, 0.9f));
        }
        t.text = $"골드 +{amount} 획득!";
    }

    //--- 2026-06-30 모양 가중 추첨(카드 1장 = 1회 독립시행). 가중치 없거나 로드 실패 시 기본 풀로 폴백.
    GameObject RollShape(ShapeDrop[] shapes)
    {
        if (shapes != null && shapes.Length > 0)
        {
            float total = 0f;
            foreach (var s in shapes) total += s.weight;
            float r = Random.value * total;
            foreach (var s in shapes)
            {
                r -= s.weight;
                if (r <= 0f)
                {
                    var p = LoadShape(s.shape);
                    if (p != null) return p;   // 로드 실패(예: 미생성 프리팹)면 폴백으로
                    break;
                }
            }
        }
        if (rewardBlockPrefabs != null && rewardBlockPrefabs.Length > 0)
            return rewardBlockPrefabs[Random.Range(0, rewardBlockPrefabs.Length)];
        return null;
    }

    static GameObject LoadShape(string name)
        => Resources.Load<GameObject>($"Block/{name}") ?? Resources.Load<GameObject>($"RewardBlock/{name}");

    public void OnCardSelected(GameObject blockPrefab, int colorID)
    {
        //--- 2026-07-01 널 가드 (씬 전환/미배치 시 크래시 방지)
        if (GameManager.Instance != null && GameManager.Instance.spawner != null)
            GameManager.Instance.spawner.AddBlockToPool(blockPrefab, colorID);
        cardSelectPanel.SetActive(false);
        blockCardButton.gameObject.SetActive(false);
        RealignButtons();
        rewardSelectPanel.SetActive(true);
    }

    public void ClickBlockButton()
    {
        rewardSelectPanel.SetActive(false);
        cardSelectPanel.SetActive(true);
    }

    public void ClickRelicButton()
    {
        if (_pendingRelic == null) return;
        if (relicHolder == null) relicHolder = FindFirstObjectByType<RelicHolder>();
        if (relicHolder == null) return;   //--- 2026-07-01 널 가드
        relicHolder.AddRelic(_pendingRelic);
        _pendingRelic = null;
        relicButton.gameObject.SetActive(false);
        RealignButtons();
    }

    public void ClickSkipButton()
    {
        _isProceeded = true;
    }

    void RealignButtons()
    {
        // SkipButton은 고정 위치. 나머지는 위 슬롯부터 채움 (크기 고정)
        var active = new List<RectTransform>();
        if (blockCardButton.gameObject.activeSelf) active.Add(blockCardButton.GetComponent<RectTransform>());
        if (relicButton.gameObject.activeSelf) active.Add(relicButton.GetComponent<RectTransform>());

        const float topY = 0.70f;
        const float slotHeight = 0.20f;
        const float spacing = 0.05f;

        for (int i = 0; i < active.Count; i++)
        {
            float top = topY - i * (slotHeight + spacing);
            float bottom = top - slotHeight;
            active[i].anchorMin = new Vector2(0.2f, bottom);
            active[i].anchorMax = new Vector2(0.8f, top);
            active[i].anchoredPosition = Vector2.zero;
            active[i].sizeDelta = Vector2.zero;
        }
    }
}
