using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ShopManager : MonoBehaviour
{
    [Header("Shop Settings")]
    public int cardCount = 2;
    public int relicCount = 2;
    public int cardPrice = 50;
    public int relicPrice = 100;

    private Text goldText;
    private Transform itemContainer;
    private List<GameObject> cardOffers = new List<GameObject>();
    private List<Relic> relicOffers = new List<Relic>();
    private List<GameObject> cardSlots = new List<GameObject>();
    private List<GameObject> relicSlots = new List<GameObject>();

    void Start()
    {
        if (!Run.IsInitialized) Run.StartNew();
        SetupCanvas();
        GenerateOffers();
        BuildUI();
    }

    void SetupCanvas()
    {
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("ShopScene Canvas 없음"); return; }
        Transform canvas = canvasGO.transform;

        var title = FindOrCreateText(canvas, "Title", new Vector2(0.1f, 0.9f), new Vector2(0.9f, 1f), 70, TextAnchor.MiddleCenter);
        title.text = "상점";
        title.color = new Color(1f, 0.85f, 0.3f);

        goldText = FindOrCreateText(canvas, "GoldText", new Vector2(0.75f, 0.92f), new Vector2(0.98f, 0.99f), 50, TextAnchor.MiddleRight);
        goldText.color = new Color(1f, 0.85f, 0.3f);

        itemContainer = FindOrCreateRect(canvas, "ItemContainer", new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.85f));

        CreateLeaveButton(canvas);
    }

    void GenerateOffers()
    {
        var blockPool = Resources.LoadAll<GameObject>("RewardBlock");
        if (blockPool.Length > 0)
            for (int i = 0; i < cardCount; i++)
                cardOffers.Add(blockPool[Random.Range(0, blockPool.Length)]);

        for (int i = 0; i < relicCount; i++)
            relicOffers.Add(RelicRegistry.GetRandom());
    }

    void BuildUI()
    {
        UpdateGoldUI();

        foreach (Transform c in itemContainer) Destroy(c.gameObject);
        cardSlots.Clear();
        relicSlots.Clear();

        int total = cardOffers.Count + relicOffers.Count;
        float slotW = 1f / total;

        int idx = 0;
        for (int i = 0; i < cardOffers.Count; i++, idx++)
        {
            int captured = i;
            GameObject slot = CreateItemSlot(idx * slotW, (idx + 1) * slotW,
                cardOffers[i] != null ? cardOffers[i].name : "(판매됨)",
                cardOffers[i] != null ? cardPrice : 0,
                cardOffers[i] != null,
                () => BuyCard(captured),
                new Color(0.25f, 0.35f, 0.55f));
            cardSlots.Add(slot);
        }
        for (int i = 0; i < relicOffers.Count; i++, idx++)
        {
            int captured = i;
            GameObject slot = CreateItemSlot(idx * slotW, (idx + 1) * slotW,
                relicOffers[i] != null ? relicOffers[i].Name : "(판매됨)",
                relicOffers[i] != null ? relicPrice : 0,
                relicOffers[i] != null,
                () => BuyRelic(captured),
                new Color(0.55f, 0.4f, 0.18f));
            relicSlots.Add(slot);
        }
    }

    GameObject CreateItemSlot(float anchorMinX, float anchorMaxX, string itemName, int price, bool available, System.Action onBuy, Color bgColor)
    {
        GameObject slot = new GameObject($"Slot_{itemName}", typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(itemContainer, false);
        slot.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = slot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorMinX + 0.01f, 0.05f);
        rt.anchorMax = new Vector2(anchorMaxX - 0.01f, 0.95f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        Image img = slot.GetComponent<Image>();
        img.color = available ? bgColor : new Color(0.2f, 0.2f, 0.2f);

        Text nameText = CreateChildText(slot.transform, "Name", new Vector2(0, 0.55f), new Vector2(1, 0.85f), itemName, 36, Color.white);

        if (price > 0)
        {
            Text priceText = CreateChildText(slot.transform, "Price", new Vector2(0, 0.3f), new Vector2(1, 0.55f), $"{price} 골드", 32, new Color(1f, 0.85f, 0.3f));
        }

        if (available && Run.stats.gold >= price)
        {
            GameObject btnGO = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(slot.transform, false);
            btnGO.layer = LayerMask.NameToLayer("UI");
            RectTransform brt = btnGO.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.15f, 0.05f);
            brt.anchorMax = new Vector2(0.85f, 0.25f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = Vector2.zero;

            btnGO.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.3f);
            btnGO.GetComponent<Button>().onClick.AddListener(() => onBuy());

            CreateChildText(btnGO.transform, "Label", Vector2.zero, Vector2.one, "구매", 30, Color.white);
        }
        else if (available)
        {
            CreateChildText(slot.transform, "NotEnough", new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.25f), "골드 부족", 26, Color.gray);
        }

        return slot;
    }

    void BuyCard(int index)
    {
        if (cardOffers[index] == null) return;
        if (Run.stats.gold < cardPrice) return;

        Run.stats.gold -= cardPrice;
        Run.deckBlockNames.Add(cardOffers[index].name);
        Debug.Log($"카드 구매: {cardOffers[index].name} (-{cardPrice}골드)");
        cardOffers[index] = null;
        BuildUI();
    }

    void BuyRelic(int index)
    {
        if (relicOffers[index] == null) return;
        if (Run.stats.gold < relicPrice) return;

        Run.stats.gold -= relicPrice;
        Relic relic = relicOffers[index];
        Run.ownedRelics.Add(relic);
        relic.OnAcquire(Run.stats);
        Debug.Log($"유물 구매: {relic.Name} (-{relicPrice}골드)");
        relicOffers[index] = null;
        BuildUI();
    }

    void CreateLeaveButton(Transform parent)
    {
        if (parent.Find("LeaveButton") != null) return;
        GameObject go = new GameObject("LeaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.4f, 0.02f);
        rt.anchorMax = new Vector2(0.6f, 0.1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f);
        go.GetComponent<Button>().onClick.AddListener(() => SceneManager.LoadScene("MapScene"));
        CreateChildText(go.transform, "Label", Vector2.zero, Vector2.one, "나가기", 40, Color.white);
    }

    void UpdateGoldUI()
    {
        if (goldText != null) goldText.text = $"골드 {Run.stats.gold}";
    }

    RectTransform FindOrCreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<RectTransform>();
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    Text FindOrCreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, TextAnchor align)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Text>();
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        Text t = go.GetComponent<Text>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    Text CreateChildText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        Text t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }
}
