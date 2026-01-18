using UnityEngine;

// 우클릭 메뉴에 'Tetris > Block Data' 메뉴를 추가함
[CreateAssetMenu(fileName = "NewBlockData", menuName = "BlockData")]
public class BlockData : ScriptableObject
{
    [Header("기본 스탯")]
    public string blockName;        // 블록 이름 (식별용)
    public bool allowRotation = true; // 회전 가능 여부

    [Header("투명도 설정")]
    public float movingAlpha = 0.5f; // 이동 중 투명도
    public float lockedAlpha = 1.0f; // 안착 후 투명도

    [Header("Match-3 Info")]
    // 0:None, 1:Red, 2:Blue, 3:Green, 4:Yellow ...
    public int colorID;
}