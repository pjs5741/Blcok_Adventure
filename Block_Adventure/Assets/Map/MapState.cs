using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapState
{
    public List<MapNode> nodes = new List<MapNode>();
    public int currentNodeId = -1;  // -1 = 아직 아무 노드도 안 들어감
    public HashSet<int> completedNodeIds = new HashSet<int>();
    public int seed;                //--- 2026-07-01 생성 시드(세이브 시 이것만 저장하면 맵 재현 가능)

    public MapNode GetNode(int id) => nodes.Find(n => n.id == id);

    public List<int> GetAvailableNextNodes()
    {
        if (currentNodeId < 0)
        {
            // 시작: layer 0 노드들
            List<int> starts = new List<int>();
            foreach (var n in nodes)
                if (n.layer == 0) starts.Add(n.id);
            return starts;
        }
        return GetNode(currentNodeId)?.nextNodeIds ?? new List<int>();
    }

    public void CompleteCurrentNode()
    {
        if (currentNodeId >= 0) completedNodeIds.Add(currentNodeId);
    }

    public void EnterNode(int id)
    {
        currentNodeId = id;
    }

    // 2026-06-26 고정 9노드 대신 사용. 시드 기반이라 데일리 시드/리더보드에 재활용 가능
    public static MapState GenerateRandom()
    {
        return GenerateRandom(System.Environment.TickCount);
    }

    public static MapState GenerateRandom(int seed)
    {
        var rng = new System.Random(seed);
        var s = new MapState();
        s.seed = seed;   //--- 2026-07-01 세이브 재현용

        const int numLayers = 7;            // 0 = Start, numLayers-1 = Boss
        const float ySpacing = 140f;        // 레이어 간 세로 간격
        const float xSpacing = 200f;        // 같은 레이어 노드 간 가로 간격
        int bossLayer = numLayers - 1;

        // 1) 레이어별 노드 개수 결정
        int[] counts = new int[numLayers];
        counts[0] = 1;                      // 시작 노드 단일
        counts[bossLayer] = 1;              // 보스 노드 단일
        for (int L = 1; L < bossLayer; L++)
            counts[L] = rng.Next(2, 5);     // 중간 레이어 2~4개

        // 2) 노드 생성 (id/layer/uiPosition, 타입은 뒤에서 배정)
        var layerNodeIds = new List<int>[numLayers];
        int nextId = 0;
        for (int L = 0; L < numLayers; L++)
        {
            layerNodeIds[L] = new List<int>();
            int k = counts[L];
            //--- 2026-07-03 가로 배치: 레이어=가로축(좌→우, START 왼쪽 / 보스 오른쪽), 같은 레이어=세로 분산
            float x = (L - (numLayers - 1) / 2f) * xSpacing;
            for (int idx = 0; idx < k; idx++)
            {
                float y = (idx - (k - 1) / 2f) * ySpacing;
                s.nodes.Add(new MapNode
                {
                    id = nextId,
                    type = NodeType.Battle,
                    layer = L,
                    uiPosition = new Vector2(x, y),
                });
                layerNodeIds[L].Add(nextId);
                nextId++;
            }
        }

        // 3) 인접 레이어 간선 연결 (교차 없음 + 전 노드 도달 보장)
        for (int L = 0; L < bossLayer; L++)
            ConnectLayers(s, layerNodeIds[L], layerNodeIds[L + 1], rng);

        // 4) 노드 타입 배정
        AssignTypes(s, layerNodeIds, rng, bossLayer);

        return s;
    }

    // 두 레이어를 교차 없이(monotonic) 잇는다. 모든 부모는 자식 1개 이상, 모든 자식은 부모 1개 이상 보장.
    static void ConnectLayers(MapState s, List<int> parents, List<int> children, System.Random rng)
    {
        int a = parents.Count, b = children.Count;
        int i = 0, j = 0;
        while (true)
        {
            var pn = s.GetNode(parents[i]);
            int childId = children[j];
            if (!pn.nextNodeIds.Contains(childId)) pn.nextNodeIds.Add(childId);

            if (i == a - 1 && j == b - 1) break;
            if (i == a - 1) { j++; continue; }   // 부모 끝 → 남은 자식 전부 마지막 부모에 연결
            if (j == b - 1) { i++; continue; }   // 자식 끝 → 남은 부모 전부 마지막 자식에 연결(수렴)

            int r = rng.Next(4);                 // 대각선(r>=2)에 가중 → 갈라짐/합쳐짐 과하지 않게
            if (r == 0) i++;                     // 분기: 같은 자식에 부모 추가
            else if (r == 1) j++;                // 분기: 같은 부모가 자식 추가
            else { i++; j++; }                   // 대각 진행
        }
    }

    static void AssignTypes(MapState s, List<int>[] layerNodeIds, System.Random rng, int bossLayer)
    {
        foreach (var id in layerNodeIds[0]) s.GetNode(id).type = NodeType.Start;
        foreach (var id in layerNodeIds[bossLayer]) s.GetNode(id).type = NodeType.Boss;

        int preBoss = bossLayer - 1;
        for (int L = 1; L < bossLayer; L++)
        {
            foreach (var id in layerNodeIds[L])
            {
                var node = s.GetNode(id);
                if (L == 1) { node.type = NodeType.Battle; continue; }       // 첫 층은 항상 전투(난이도 적응)
                if (L == preBoss) { node.type = rng.Next(100) < 70 ? NodeType.Rest : NodeType.Battle; continue; } // 보스 전 휴식 우대
                node.type = RollNodeType(rng, L, bossLayer);
            }
        }

        // 최소 보장: 해당 타입이 하나도 없으면 중간 전투 노드 하나를 바꿔준다
        EnsureExists(s, layerNodeIds, bossLayer, rng, NodeType.Shop, 2);
        EnsureExists(s, layerNodeIds, bossLayer, rng, NodeType.Rest, 2);
        EnsureExists(s, layerNodeIds, bossLayer, rng, NodeType.Event, 2);              // 이벤트도 매 런 최소 1개
        EnsureExists(s, layerNodeIds, bossLayer, rng, NodeType.Elite, bossLayer / 2);  // 엘리트는 매 런 반드시 등장(후반부)
    }

    static NodeType RollNodeType(System.Random rng, int layer, int bossLayer)
    {
        bool eliteAllowed = layer >= bossLayer / 2;   // 엘리트는 후반부에만
        int roll = rng.Next(100);
        if (eliteAllowed)
        {
            if (roll < 50) return NodeType.Battle;
            if (roll < 65) return NodeType.Elite;
            if (roll < 78) return NodeType.Shop;
            if (roll < 90) return NodeType.Event;
            return NodeType.Rest;
        }
        if (roll < 62) return NodeType.Battle;
        if (roll < 77) return NodeType.Shop;
        if (roll < 90) return NodeType.Event;
        return NodeType.Rest;
    }

    static void EnsureExists(MapState s, List<int>[] layerNodeIds, int bossLayer, System.Random rng, NodeType type, int minLayer)
    {
        foreach (var node in s.nodes) if (node.type == type) return;   // 이미 존재하면 통과

        // minLayer ~ 보스 전 층 사이의 전투 노드를 후보로 (첫 층/보스 전 층 제외)
        var candidates = new List<MapNode>();
        for (int L = Mathf.Max(minLayer, 2); L < bossLayer - 1; L++)
            foreach (var id in layerNodeIds[L])
            {
                var n = s.GetNode(id);
                if (n.type == NodeType.Battle) candidates.Add(n);
            }
        if (candidates.Count == 0) return;
        candidates[rng.Next(candidates.Count)].type = type;
    }

    public static MapState GenerateFixed()
    {
        // 9-node tree:
        //              [8 Boss]
        //                |
        //          [6 Elite]  [7 Battle]
        //          /     \   /     \
        //      [3 Battle][4 Shop][5 Battle]
        //          |   X   |   X   |
        //      [1 Battle]  [2 Battle]
        //          \         /
        //           [0 Start]
        var s = new MapState();
        s.nodes.Add(new MapNode { id = 0, type = NodeType.Start, layer = 0, uiPosition = new Vector2(0, -300), nextNodeIds = new List<int>{1, 2} });
        s.nodes.Add(new MapNode { id = 1, type = NodeType.Rest, layer = 1, uiPosition = new Vector2(-200, -100), nextNodeIds = new List<int>{3, 4} });
        s.nodes.Add(new MapNode { id = 2, type = NodeType.Battle, layer = 1, uiPosition = new Vector2(200, -100), nextNodeIds = new List<int>{4, 5} });
        s.nodes.Add(new MapNode { id = 3, type = NodeType.Battle, layer = 2, uiPosition = new Vector2(-300, 100), nextNodeIds = new List<int>{6} });
        s.nodes.Add(new MapNode { id = 4, type = NodeType.Shop, layer = 2, uiPosition = new Vector2(0, 100), nextNodeIds = new List<int>{6, 7} });
        s.nodes.Add(new MapNode { id = 5, type = NodeType.Battle, layer = 2, uiPosition = new Vector2(300, 100), nextNodeIds = new List<int>{7} });
        s.nodes.Add(new MapNode { id = 6, type = NodeType.Elite, layer = 3, uiPosition = new Vector2(-150, 300), nextNodeIds = new List<int>{8} });
        s.nodes.Add(new MapNode { id = 7, type = NodeType.Battle, layer = 3, uiPosition = new Vector2(150, 300), nextNodeIds = new List<int>{8} });
        s.nodes.Add(new MapNode { id = 8, type = NodeType.Boss, layer = 4, uiPosition = new Vector2(0, 500), nextNodeIds = new List<int>() });
        return s;
    }
}
