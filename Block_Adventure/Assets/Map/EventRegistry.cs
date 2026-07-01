using System.Collections.Generic;
using UnityEngine;

//--- 2026-06-29 이벤트 노드 데이터 + 효과 적용. 유물/몬스터 레지스트리와 같은 정적 목록 패턴.
// 효과는 전부 Run 상태 조작(상점/보상과 동일). 배경 이미지는 막판 리소스 작업으로 backgroundName에 채울 예정.

public enum EventEffect { None, Gold, Relic, Card, GridClean, GridGarbage, GridGold }

public class EventChoice
{
    public string label;
    public string resultText;
    public EventEffect effect;
    public int amount;        // 골드량 / 정리·쓰레기 줄 수
    public int goldCost;      // 선택 비용(골드). 부족하면 버튼 비활성
    public EventEffect effect2;   // 보조 효과(위험-보상용, 예: 골드+회색줄)
    public int amount2;

    public EventChoice(string label, string resultText, EventEffect effect, int amount = 0, int goldCost = 0,
        EventEffect effect2 = EventEffect.None, int amount2 = 0)
    {
        this.label = label;
        this.resultText = resultText;
        this.effect = effect;
        this.amount = amount;
        this.goldCost = goldCost;
        this.effect2 = effect2;
        this.amount2 = amount2;
    }
}

public class EventDef
{
    public string title;
    public string body;
    public List<EventChoice> choices;
    public string backgroundName;   // 선택: Resources 경로. 지금은 비움(플레이스홀더 배경)

    public EventDef(string title, string body, List<EventChoice> choices, string backgroundName = null)
    {
        this.title = title;
        this.body = body;
        this.choices = choices;
        this.backgroundName = backgroundName;
    }
}

public static class EventRegistry
{
    public static EventDef GetRandom() => All[Random.Range(0, All.Count)];

    public static readonly List<EventDef> All = new List<EventDef>
    {
        new EventDef("낡은 제단", "이끼 낀 돌 제단에 동전 구멍이 보인다. 무언가 응답을 기다리는 듯하다.",
            new List<EventChoice>
            {
                new EventChoice("동전을 바친다 (50골드)", "제단이 빛나고, 낯선 힘이 손에 깃든다.", EventEffect.Relic, goldCost: 50),
                new EventChoice("그냥 지나간다", "제단을 뒤로하고 길을 재촉한다.", EventEffect.None),
            }),

        new EventDef("버려진 보급품", "무너진 천막 아래 먼지 쌓인 상자가 놓여 있다.",
            new List<EventChoice>
            {
                new EventChoice("상자를 연다", "안에서 쓸 만한 블록 하나를 찾아냈다!", EventEffect.Card),
                new EventChoice("주변 금화만 줍는다", "상자는 두고, 바닥에 흩어진 금화만 챙겼다.", EventEffect.Gold, amount: 40),
            }),

        new EventDef("굶주린 떠돌이", "야윈 떠돌이가 길을 막는다. \"가진 걸 조금만 나눠주겠나…?\"",
            new List<EventChoice>
            {
                new EventChoice("골드를 나눠준다 (30골드)", "떠돌이는 미소 지으며 낡은 부적을 건넸다.", EventEffect.Relic, goldCost: 30),
                new EventChoice("매몰차게 지나친다", "떠돌이의 시선을 뒤로하고 떠난다.", EventEffect.None),
            }),

        new EventDef("버려진 정비소", "녹슨 작업대가 놓인 정비소. 연장 몇 개가 아직 쓸 만해 보인다.",
            new List<EventChoice>
            {
                new EventChoice("쌓인 블록을 정리한다", "위쪽에 쌓인 블록을 말끔히 걷어냈다.", EventEffect.GridClean, amount: 2),
                new EventChoice("연장을 팔아 금화를 챙긴다", "쓸 만한 연장을 팔아 금화를 얻었다.", EventEffect.Gold, amount: 35),
            }),

        new EventDef("도박꾼의 제안", "복면을 쓴 자가 동전을 튕긴다. \"운을 시험해보겠나?\"",
            new List<EventChoice>
            {
                new EventChoice("건다 (40골드)", "운이 좋았다 — 희귀한 블록을 손에 넣었다!", EventEffect.Card, goldCost: 40),
                new EventChoice("거절한다", "도박꾼은 어깨를 으쓱하며 사라졌다.", EventEffect.None),
            }),

        new EventDef("버려진 광맥", "동굴 벽에 금빛 광맥이 번뜩인다. 캐내면 그리드에 금괴가 박힐 것이다.",
            new List<EventChoice>
            {
                new EventChoice("한 줄 캐낸다", "금괴 한 줄을 그리드에 실어 담았다. 깨서 챙겨야 한다.", EventEffect.GridGold, amount: 1),
                new EventChoice("그냥 지나간다", "괜한 짐이 될까 봐 손대지 않았다.", EventEffect.None),
            }),

        new EventDef("무너진 보물 창고", "금괴가 천장까지 쌓인 창고. 욕심내면 손이 무거워진다.",
            new List<EventChoice>
            {
                new EventChoice("두 줄 가득 퍼담는다", "금괴 두 줄을 그리드에 실었다. 다 깨면 큰 돈이지만 공간이 빠듯하다.", EventEffect.GridGold, amount: 2),
                new EventChoice("한 줄만 챙긴다", "무리하지 않고 한 줄만 담았다.", EventEffect.GridGold, amount: 1),
            }),

        new EventDef("저주받은 금고", "묵직한 금고가 반쯤 열려 금화가 흘러나온다. 불길한 기운이 감돈다.",
            new List<EventChoice>
            {
                new EventChoice("금화를 움켜쥔다", "금화를 두둑이 챙겼지만, 저주가 깨어나 회색 잔해가 쏟아져 내렸다…",
                    EventEffect.Gold, amount: 90, effect2: EventEffect.GridGarbage, amount2: 2),
                new EventChoice("건드리지 않는다", "불길함을 느끼고 조용히 발길을 돌린다.", EventEffect.None),
            }),
    };

    // 효과(주+보조) 적용 + 결과 보조 텍스트(획득/손실 내역) 반환
    public static string Apply(EventChoice c)
    {
        if (c.goldCost > 0) Run.stats.gold = Mathf.Max(0, Run.stats.gold - c.goldCost);

        string d1 = ApplyOne(c.effect, c.amount);
        string d2 = ApplyOne(c.effect2, c.amount2);
        return string.IsNullOrEmpty(d2) ? d1 : (d1 + " " + d2).Trim();
    }

    static string ApplyOne(EventEffect effect, int amount)
    {
        switch (effect)
        {
            case EventEffect.Gold:
                Run.stats.gold = Mathf.Max(0, Run.stats.gold + amount);
                return amount >= 0 ? $"(골드 +{amount})" : $"(골드 {amount})";

            case EventEffect.Relic:
            {
                var relic = RelicRegistry.GetRandomExcluding(Run.ownedRelics);
                if (relic != null) { Run.ownedRelics.Add(relic); return "(유물 획득)"; }
                return "(하지만 더 얻을 유물이 없었다)";
            }

            case EventEffect.Card:
            {
                var pool = Resources.LoadAll<GameObject>("RewardBlock");
                if (pool != null && pool.Length > 0)
                {
                    var p = pool[Random.Range(0, pool.Length)];
                    int colorID = Random.Range(BlockColors.MinEffect, BlockColors.MaxEffect + 1);
                    Run.deck.Add(new DeckEntry(p.name, colorID));
                    return $"(블록 카드 획득: {p.name} [{BlockColors.Name(colorID)}])";
                }
                return "(하지만 얻을 블록이 없었다)";
            }

            case EventEffect.GridClean:
                return $"(블록 {CleanTopRows(amount)}줄 정리)";

            case EventEffect.GridGarbage:
                AddGarbageRows(amount);
                return $"(회색 줄 {amount}개 추가)";

            case EventEffect.GridGold:
                AddGoldRows(amount);
                return $"(금 블럭 {amount}줄 추가 — 줄/폭탄으로 깨면 골드)";

            default:
                return "";
        }
    }

    // 가장 위쪽(위험한) 차 있는 줄부터 count개 비움. 실제 비운 줄 수 반환.
    static int CleanTopRows(int count)
    {
        var snap = Run.gridSnapshot;
        if (snap == null) return 0;
        int w = snap.GetLength(0), h = snap.GetLength(1), cleared = 0;
        for (int y = h - 1; y >= 0 && cleared < count; y--)
        {
            bool any = false;
            for (int x = 0; x < w; x++) if (snap[x, y] != 0) { any = true; break; }
            if (!any) continue;
            for (int x = 0; x < w; x++) snap[x, y] = 0;
            cleared++;
        }
        return cleared;
    }

    // 바닥에 회색(99) 줄을 count개 추가(전체 위로 밀고 구멍 하나). 전투의 ShiftAndCreateRow와 동일 개념.
    static void AddGarbageRows(int count) => AddBottomRows(count, 99);

    // 바닥에 금 블럭(보물) 줄을 count개 추가. 구멍 하나 — 채워서 줄 클리어(또는 폭탄)로 깨면 골드.
    static void AddGoldRows(int count) => AddBottomRows(count, BlockGrid.GoldBlockID);

    static void AddBottomRows(int count, int colorID)
    {
        if (Run.gridSnapshot == null) Run.gridSnapshot = new int[11, 21];
        var snap = Run.gridSnapshot;
        int w = snap.GetLength(0), h = snap.GetLength(1);
        for (int n = 0; n < count; n++)
        {
            for (int y = h - 2; y >= 0; y--)
                for (int x = 0; x < w; x++)
                    snap[x, y + 1] = snap[x, y];
            int hole = Random.Range(0, w);
            for (int x = 0; x < w; x++) snap[x, 0] = (x == hole) ? 0 : colorID;
        }
    }
}
