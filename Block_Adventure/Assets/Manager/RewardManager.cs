using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;

    [Header("UI")]
    public GameObject rewardPanel;
    public RewardCard[] rewardCards;

    [Header("보상 블록 목록 (빈 경우 Resources/Block 전체 사용)")]
    public GameObject[] rewardBlockPrefabs;

    private bool _isSelected;

    void Awake()
    {
        Instance = this;
        if (rewardPanel == null)
            rewardPanel = transform.Find("RewardPanel").gameObject;
        if (rewardCards == null || rewardCards.Length == 0)
            rewardCards = rewardPanel.GetComponentsInChildren<RewardCard>(true);
        if (rewardBlockPrefabs == null || rewardBlockPrefabs.Length == 0)
            rewardBlockPrefabs = Resources.LoadAll<GameObject>("RewardBlock");
        rewardPanel.SetActive(false);
    }

    public IEnumerator ShowReward(Monster monster = null)
    {
        _isSelected = false;

        var monsterPool = monster?.GetRewardPool();
        var pool = (monsterPool != null && monsterPool.Count > 0)
            ? monsterPool.ToArray()
            : rewardBlockPrefabs;

        // var picked = new List<GameObject>();
        for (int i = 0; i < rewardCards.Length; i++)
        {
            // 이미 나온 거 제외 버전
            // var available = System.Array.FindAll(pool, p => !picked.Contains(p));
            // if (available.Length == 0) available = pool;
            // var chosen = available[Random.Range(0, available.Length)];
            // picked.Add(chosen);
            // rewardCards[i].Setup(chosen);

            rewardCards[i].Setup(pool[Random.Range(0, pool.Length)]);
        }

        gameObject.SetActive(true);
        rewardPanel.SetActive(true);
        yield return new WaitUntil(() => _isSelected);
        rewardPanel.SetActive(false);
        gameObject.SetActive(false);
    }

    public void OnCardSelected(GameObject blockPrefab)
    {
        GameManager.Instance.spawner.AddBlockToPool(blockPrefab);
        _isSelected = true;
    }
}
