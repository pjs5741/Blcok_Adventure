using System.Collections.Generic;

public enum RunResult { None, Victory, GameOver }

// 한 라운드(런) 동안 씬 전환 사이에 살아남아야 하는 상태
public static class Run
{
    public static PlayerStats stats;
    public static List<Relic> ownedRelics;
    public static List<string> deckBlockNames;  // 프리팹 이름 (Resources.Load로 복원)
    public static MapState mapState;
    public static int[,] gridSnapshot;  // [x, y] = colorID (0 = 빈칸). 씬 전환 사이 그리드 유지용
    public static RunResult lastResult = RunResult.None;

    public static bool IsInitialized => stats != null;

    public static void StartNew()
    {
        stats = new PlayerStats();
        ownedRelics = new List<Relic>();
        deckBlockNames = new List<string>
        {
            "Block_Z", "Block_Z",
            "Block_S", "Block_S",
            "Block_L", "Block_L",
            "Block_J", "Block_J"
        };
        mapState = MapState.GenerateFixed();
        gridSnapshot = null;
    }
}
