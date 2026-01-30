using UnityEngine;

[System.Serializable] // 이게 있어야 인스펙터 창에서 보임! <- ㅋㅋ 아님
public class GridData
{
    public int width = 11;
    public int height = 21;
    
    // 2차원 배열은 유니티 인스펙터에 바로 안 보일 수 있어서 
    // 보통은 1차원 배열로 쓰거나 별도 처리를 하지만, 로직상으로는 그대로 둬도 됨 <- ㅋㅋ 안됨
    // (저장을 위해서는 직렬화 가능한 형태로 변형 필요)
    public Transform[,] gridArray;

    // 생성자 (초기화용)
    public GridData(int width, int height) // 줄여쓰기 금지 ㅋㅋ
    {
        this.width = width;
        this.height = height;
        gridArray = new Transform[width, height];
    } // 가독성이 높아졋어요. 저 안 쓰지않아여?? 에ㅋ이 ㅋ id는  id는  id죠
}