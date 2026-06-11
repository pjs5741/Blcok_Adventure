using System.Collections.Generic;
using UnityEngine;

public enum NodeType { Start, Battle, Elite, Shop, Boss, Rest }

[System.Serializable]
public class MapNode
{
    public int id;
    public NodeType type;
    public int layer;
    public Vector2 uiPosition;
    public List<int> nextNodeIds = new List<int>();
}
