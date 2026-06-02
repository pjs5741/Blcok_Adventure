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
    public Button skipButton;

    [Header("보상 블록 목록 (빈 경우 Resources/Block 전체 사용)")]
    public GameObject[] rewardBlockPrefabs;

    private bool _isProceeded;

    void Awake()
    {
        Instance = this;
        Init();

        blockCardButton.onClick.AddListener(ClickBlockButton);
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
        if (skipButton == null)
            skipButton = cardSelectPanel.transform.Find("SkipButton").GetComponent<Button>();
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
        _isProceeded = true;
    }

    public void ClickBlockButton()
    {
        rewardSelectPanel.SetActive(false);
        cardSelectPanel.SetActive(true);
    }

    public void ClickSkipButton()
    {
        _isProceeded = true;
    }
}
