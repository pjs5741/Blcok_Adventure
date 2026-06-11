using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapState
{
    public List<MapNode> nodes = new List<MapNode>();
    public int currentNodeId = -1;  // -1 = 아직 아무 노드도 안 들어감
    public HashSet<int> completedNodeIds = new HashSet<int>();

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
