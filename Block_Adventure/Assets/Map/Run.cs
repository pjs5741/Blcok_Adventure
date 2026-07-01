using System.Collections.Generic;

public enum RunResult { None, Victory, GameOver }

//--- 2026-06-29 덱 엔트리: 모양(프리팹 이름) + 색(효과 id). 색은 카드 획득 시 고정(색 덱빌딩).
[System.Serializable]   //--- 2026-07-01 세이브(JsonUtility) 직렬화용
public class DeckEntry
{
    public string blockName;   // Resources의 프리팹 이름 (모양)
    public int colorID;        // 1~5 효과 색 (고정)
    public DeckEntry() { }     // JsonUtility 역직렬화용
    public DeckEntry(string blockName, int colorID) { this.blockName = blockName; this.colorID = colorID; }
}

// 한 라운드(런) 동안 씬 전환 사이에 살아남아야 하는 상태
public static class Run
{
    public static PlayerStats stats;
    public static List<Relic> ownedRelics;
    //--- 2026-06-29 deckBlockNames(모양만) → deck(모양+색)으로 변경. 색이 덱에 고정됨.
    public static List<DeckEntry> deck;
    public static MapState mapState;
    public static int[,] gridSnapshot;  // [x, y] = colorID (0 = 빈칸). 씬 전환 사이 그리드 유지용
    public static RunResult lastResult = RunResult.None;

    public static bool IsInitialized => stats != null;

    public static void StartNew()
    {
        stats = new PlayerStats();
        ownedRelics = new List<Relic>();
        //--- 2026-06-30 시작 덱: 모양 L2·J2(반대L)·Z2·S2(반대Z). 색은 기본공격(칼=1) 절반 + 가드(방패=4) 절반.
        // 화염/독약/폭탄 색은 시작 덱에서 빼고 보상으로만 획득.
        deck = new List<DeckEntry>
        {
            new DeckEntry("Block_Z", 1), new DeckEntry("Block_Z", 4),
            new DeckEntry("Block_S", 1), new DeckEntry("Block_S", 4),
            new DeckEntry("Block_L", 1), new DeckEntry("Block_L", 4),
            new DeckEntry("Block_J", 1), new DeckEntry("Block_J", 4),
        };
        mapState = MapState.GenerateRandom();
        gridSnapshot = null;
    }
}
