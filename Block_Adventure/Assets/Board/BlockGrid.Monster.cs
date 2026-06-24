using UnityEngine;
using System.Linq;
using Random = UnityEngine.Random;

public partial class BlockGrid
{
    public void ShiftAllRowsUp()
    {
        for (int y = data.height - 2; y >= 0; y--)
        {
            for (int x = 0; x < data.width; x++)
            {
                Transform block = data.gridArray[x, y];
                if (block != null)
                {
                    data.gridArray[x, y + 1] = block;
                    data.gridArray[x, y] = null;
                    block.position += new Vector3(0, 1, 0);
                }
            }
        }
    }

    public void ShiftAndCreateRow(int grayColorID, Color grayColor)
    {
        for (int y = data.height - 2; y >= 0; y--)
        {
            for (int x = 0; x < data.width; x++)
            {
                Transform block = data.gridArray[x, y];
                if (block != null)
                {
                    data.gridArray[x, y + 1] = block;
                    data.gridArray[x, y] = null;
                    block.position += new Vector3(0, 1, 0);
                }
            }
        }

        int holeIndex = Random.Range(0, data.width);

        for (int x = 0; x < data.width; x++)
        {
            if (x == holeIndex) continue;

            GameObject newBlockObj = spawner.SpawnStaticBlock(x, 0, grayColorID, grayColor);
            newBlockObj.transform.SetParent(this.transform);
            data.gridArray[x, 0] = newBlockObj.transform;
        }

        if (IsBlind) SetGridVisible(false);   // 은폐 중이면 새로 추가된 줄도 가림
    }

    //--- 2026-06-24 [몬스터 패턴] 은폐: N턴 동안 쌓인 블록을 안 보이게. 안경 유물로 무효.
    private int _blindTurns = 0;
    public bool IsBlind => _blindTurns > 0;

    void OnEnable()  { GameEvents.OnTurnStart += TickBlind; GameEvents.OnTurnStart += TickPivot; }
    void OnDisable() { GameEvents.OnTurnStart -= TickBlind; GameEvents.OnTurnStart -= TickPivot; }

    public void ApplyBlind(int turns)
    {
        if (Run.ownedRelics != null && Run.ownedRelics.Any(r => r is Relic_Glasses))
        {
            Debug.Log("🔍 안경 유물 — 은폐 무효");
            return;
        }
        _blindTurns = turns;
        SetGridVisible(false);
        Debug.Log($"🌑 블록 은폐 {turns}턴");
    }

    void TickBlind()
    {
        if (_blindTurns <= 0) return;
        _blindTurns--;
        if (_blindTurns <= 0)
        {
            SetGridVisible(true);
            Debug.Log("🌑 은폐 해제");
        }
    }

    void SetGridVisible(bool visible)
    {
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                Transform cell = data.gridArray[x, y];
                if (cell == null) continue;
                var sr = cell.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }
    }

    //--- 2026-06-24 [몬스터 패턴] 중력방향 전환: 왼쪽으로 한 번 밀고 끝 (지속 없음)
    public void ApplyGravityShift()
    {
        ApplyHorizontalGravity();
        Debug.Log("⬅ 중력 왼쪽으로 한 번 밀기");
    }

    //--- 2026-06-24 [몬스터 패턴] 피벗: 판 90도 회전, N턴 후 원복
    private int _pivotTurns = 0;

    public void ApplyPivot(int turns)
    {
        if (_pivotTurns > 0) return;   // 이미 피벗 중이면 무시
        RotateGrid(clockwise: false);   // 반시계 — 판이 플레이어 쪽으로 쓰러지는 느낌
        _pivotTurns = turns;
        Debug.Log($"🔄 피벗 90도 회전 {turns}턴");
    }

    void TickPivot()
    {
        if (_pivotTurns <= 0) return;
        _pivotTurns--;
        if (_pivotTurns <= 0)
        {
            RotateGrid(clockwise: true);   // 원복 (반시계로 돌렸으니 시계로)
            Debug.Log("🔄 피벗 원복");
        }
    }

    void RotateGrid(bool clockwise)
    {
        int oldW = data.width, oldH = data.height;
        Transform[,] old = data.gridArray;
        int newW = oldH, newH = oldW;
        Transform[,] rot = new Transform[newW, newH];

        for (int x = 0; x < oldW; x++)
            for (int y = 0; y < oldH; y++)
            {
                if (old[x, y] == null) continue;
                int nx, ny;
                if (clockwise) { nx = y; ny = oldW - 1 - x; }   // 시계 90도
                else           { nx = oldH - 1 - y; ny = x; }   // 반시계(원복)
                rot[nx, ny] = old[x, y];
                old[x, y].position = new Vector3(nx, ny, 0);     // 회전은 즉시 이동
            }

        data.gridArray = rot;
        data.width = newW;
        data.height = newH;

        ApplyGravity();   // 회전 후 새 바닥으로 정렬(보간) — 천장 즉시 게임오버 방지

        // 배경+카메라는 Background.cs가 그리드 width/height로 잡아둠 → 회전 후 재호출하면 자동으로 따라옴
        var bg = FindFirstObjectByType<Background>();
        if (bg != null) bg.ResizeAndReposition();
    }
}
