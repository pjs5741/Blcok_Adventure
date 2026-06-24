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

        var monsterPool = monster?.GetRewardPool();
        var pool = (monsterPool != null && monsterPool.Count > 0)
            ? monsterPool.ToArray()
            : rewardBlockPrefabs;

        for (int i = 0; i < rewardCards.Length; i++)
            rewardCards[i].Setup(pool[Random.Range(0, pool.Length)]);

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

    public void OnCardSelected(GameObject blockPrefab)
    {
        GameManager.Instance.spawner.AddBlockToPool(blockPrefab);
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
