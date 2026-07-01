using System.Collections.Generic;
using UnityEngine;

//--- 2026-07-01 이어하기(세이브/로드). 저장 단위 = 맵 노드(전투 사이). PlayerPrefs에 JSON으로 보관.
// 맵은 시드로 재현, 유물은 레지스트리 인덱스로 저장, 유물 스탯효과는 저장된 stats에 이미 반영(로드 시 이벤트 구독만 OnLoad로 복구).
public static class SaveSystem
{
    const string Key = "BlockAdventure_Save";

    [System.Serializable]
    class SaveData
    {
        public PlayerStats stats;
        public List<DeckEntry> deck;
        public int mapSeed;
        public int currentNodeId;
        public List<int> completedNodeIds;
        public List<int> relicIndices;
        public int lastResult;
    }

    public static bool HasSave()
        => PlayerPrefs.HasKey(Key) && !string.IsNullOrEmpty(PlayerPrefs.GetString(Key, ""));

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    public static void Save()
    {
        if (!Run.IsInitialized || Run.mapState == null) return;

        var data = new SaveData
        {
            stats = Run.stats,
            deck = Run.deck,
            mapSeed = Run.mapState.seed,
            currentNodeId = Run.mapState.currentNodeId,
            completedNodeIds = new List<int>(Run.mapState.completedNodeIds),
            relicIndices = new List<int>(),
            lastResult = (int)Run.lastResult,
        };

        if (Run.ownedRelics != null)
            foreach (var r in Run.ownedRelics)
            {
                int idx = RelicRegistry.IndexOf(r);
                if (idx >= 0) data.relicIndices.Add(idx);
            }

        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log($"💾 저장 완료 (노드 {data.currentNodeId}, 덱 {data.deck?.Count}, 유물 {data.relicIndices.Count})");
    }

    public static bool Load()
    {
        if (!HasSave()) return false;
        var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key, ""));
        if (data == null || data.stats == null) return false;

        Run.stats = data.stats;
        Run.deck = data.deck ?? new List<DeckEntry>();
        Run.mapState = MapState.GenerateRandom(data.mapSeed);         // 시드로 맵 재현
        Run.mapState.currentNodeId = data.currentNodeId;
        Run.mapState.completedNodeIds = new HashSet<int>(data.completedNodeIds ?? new List<int>());
        Run.lastResult = (RunResult)data.lastResult;
        Run.gridSnapshot = null;                                      // 노드 단위 저장 → 전투는 새로 시작

        Run.ownedRelics = new List<Relic>();
        if (data.relicIndices != null)
            foreach (int idx in data.relicIndices)
            {
                var r = RelicRegistry.Create(idx);
                if (r != null) { Run.ownedRelics.Add(r); r.OnLoad(Run.stats); }   // 이벤트 재구독(스탯 재적용 X)
            }

        Debug.Log($"📂 불러오기 완료 (노드 {Run.mapState.currentNodeId})");
        return true;
    }
}
