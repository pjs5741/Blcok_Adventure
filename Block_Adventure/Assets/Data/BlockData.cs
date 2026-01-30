using UnityEngine;

// ��Ŭ�� �޴��� 'Tetris > Block Data' �޴��� �߰���
[CreateAssetMenu(fileName = "NewBlockData", menuName = "BlockData")]
public class BlockData : ScriptableObject
{
    [Header("�⺻ ����")]
    public string blockName;        // ��� �̸� (�ĺ���)
    public bool allowRotation = true; // ȸ�� ���� ����

    [Header("���� ����")]
    public float movingAlpha = 0.5f; // �̵� �� ����
    public float lockedAlpha = 1.0f; // ���� �� ����

    [Header("Match-3 Info")]
    // 0:None, 1:Red, 2:Blue, 3:Green, 4:Yellow ...
    public int colorID;
}
// wow encoding utf-8 please

//ㅋㅋㅋㅋ 나 지금봣음 이렇게되어있네

// why? what?
// what dirty? no blockdata yes blockgrid