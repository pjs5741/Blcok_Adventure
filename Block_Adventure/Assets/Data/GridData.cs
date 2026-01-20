using UnityEngine;

[System.Serializable] // 이게 있어야 인스펙터 창에서 보임!
public class GridData
{
    public int width = 11;
    public int height = 21;
    
    // 2차원 배열은 유니티 인스펙터에 바로 안 보일 수 있어서 
    // 보통은 1차원 배열로 쓰거나 별도 처리를 하지만, 로직상으로는 그대로 둬도 됨
    // (저장을 위해서는 직렬화 가능한 형태로 변형 필요)
    public Transform[,] gridArray; 

    // 생성자 (초기화용)
    public GridData(int w, int h)
    {
        width = w;
        height = h;
        gridArray = new Transform[width, height];
    }
}