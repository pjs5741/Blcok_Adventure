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

    void OnEnable()  { GameEvents.OnTurnStart += TickBlind; }
    void OnDisable() { GameEvents.OnTurnStart -= TickBlind; }

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
}
