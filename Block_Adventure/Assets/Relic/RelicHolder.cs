using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RelicHolder : MonoBehaviour
{
    public List<Relic> relics = new List<Relic>();
    private PlayerStats stats;
    private RectTransform listPanel;

    void Awake()
    {
        stats = GetComponent<Player>().stats;
        var found = GameObject.Find("RelicListPanel");
        if (found != null) listPanel = found.GetComponent<RectTransform>();
    }

    public void AddRelic(Relic relic)
    {
        relics.Add(relic);
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
        rt.sizeDelta = new Vector2(80, 80);

        Image img = iconGO.GetComponent<Image>();
        img.sprite = relic.GetIcon();
        img.preserveAspect = true;
    }

    // [TEST] R 키로 글라디우스 획득. 나중에 보상 시스템에서 호출하도록 교체
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            AddRelic(new Relic_Gladius());
    }
}
