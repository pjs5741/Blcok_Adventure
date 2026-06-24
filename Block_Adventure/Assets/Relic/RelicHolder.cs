using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class RelicHolder : MonoBehaviour
{
    private PlayerStats stats;
    private RectTransform listPanel;

    void Start()
    {
        if (!Run.IsInitialized) Run.StartNew();
        stats = Run.stats;

        var found = GameObject.Find("RelicListPanel");
        if (found != null) listPanel = found.GetComponent<RectTransform>();

        // 씬 재진입 시 보유한 유물 아이콘 다시 그림 (OnAcquire는 호출하지 않음)
        foreach (var relic in Run.ownedRelics)
            SpawnIcon(relic);
    }

    public void AddRelic(Relic relic)
    {
        Run.ownedRelics.Add(relic);
        relic.OnAcquire(stats);
        SpawnIcon(relic);
        Debug.Log($"⚔ 유물 획득: {relic.Name} - {relic.Description}");
    }

    void SpawnIcon(Relic relic)
    {
        if (listPanel == null) return;

        GameObject iconGO = new GameObject(relic.Name, typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(listPanel, false);

        RectTransform rt = iconGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(56, 56);

        Image img = iconGO.GetComponent<Image>();
        img.sprite = relic.GetIcon();
        img.preserveAspect = true;
    }

    // [TEST] 디버그 키. R = 랜덤 유물, P = 몬스터에 독 10스택
    void Update()
    {
        //--- 2026-06-23 [TEST] R = 천리안+홀드 강제 지급 (미리보기/홀드 테스트용). 기존: 랜덤 유물
        // if (Input.GetKeyDown(KeyCode.R)) AddRelic(RelicRegistry.GetRandom());
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!Run.ownedRelics.Any(r => r is Relic_Clairvoyance)) AddRelic(new Relic_Clairvoyance());
            if (!Run.ownedRelics.Any(r => r is Relic_Hold)) AddRelic(new Relic_Hold());
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            var monster = GameManager.Instance?.battleManager?.currentMonster;
            if (monster != null) monster.ApplyPoison(10);
        }
    }
}
