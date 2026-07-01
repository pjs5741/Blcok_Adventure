using UnityEngine;
using System;
using System.Collections;
using Random = UnityEngine.Random;

public partial class BlockGrid : MonoBehaviour
{
    public GridData data;
    public BlockSpawner spawner;

    [Header("Animation Settings")]
    public float dropDuration = 0.2f;
    public float destroyDuration = 0.3f;
    //--- 2026-06-30 고정 대기(attackAnimDuration) 폐기 → 실제 모션 완료 대기(BattleManager.AttackRoutine)로 대체
    // public float attackAnimDuration = 0.45f;

    [Header("Game Status")]
    public int comboCount = 0;
    public float currentDamageMultiplier = 1f;
    //--- 2026-06-30 OnMatchCompleted 이벤트 폐기 → BlockGrid가 BattleManager.AttackRoutine 직접 호출(모션 완료 대기)
    // public event Action<AttackContext> OnMatchCompleted;

    private PlayerStats playerStats;

    void Awake()
    {
        data = new GridData(11, 21);
        spawner = FindFirstObjectByType<BlockSpawner>();
        //--- 2026-07-01 Player 미배치 시 크래시 방지 (없으면 기본 스탯으로 폴백)
        var player = FindFirstObjectByType<Player>();
        playerStats = player != null ? player.stats : (Run.IsInitialized ? Run.stats : new PlayerStats());

        //--- 2026-06-30 블록 호버 툴팁 부착 (마우스 올린 칸의 색/효과 표시)
        var hover = GetComponent<BlockHoverTooltip>() ?? gameObject.AddComponent<BlockHoverTooltip>();
        hover.grid = this;
    }

    void Start()
    {
        LoadSnapshot();
    }

    public void SaveSnapshot()
    {
        int[,] snap = new int[data.width, data.height];
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null)
                {
                    var bc = data.gridArray[x, y].GetComponent<BlockColor>();
                    snap[x, y] = bc != null ? bc.colorID : 0;
                }
        Run.gridSnapshot = snap;
    }

    //--- 2026-07-01 협동: 그리드 색을 1차원(row-major, [x + y*width]) 배열로 평탄화 — 상대 미니뷰 전송용
    public int[] FlattenColors()
    {
        int[] flat = new int[data.width * data.height];
        for (int y = 0; y < data.height; y++)
            for (int x = 0; x < data.width; x++)
            {
                var cell = data.gridArray[x, y];
                if (cell == null) continue;
                var bc = cell.GetComponent<BlockColor>();
                flat[x + y * data.width] = bc != null ? bc.colorID : 0;
            }
        return flat;
    }

    public void LoadSnapshot()
    {
        if (Run.gridSnapshot == null) return;
        int[,] snap = Run.gridSnapshot;
        if (snap.GetLength(0) != data.width || snap.GetLength(1) != data.height) return;

        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                int colorID = snap[x, y];
                if (colorID == 0) continue;
                Color color = spawner.GetColorByID(colorID);
                GameObject block = spawner.SpawnStaticBlock(x, y, colorID, color);
                block.transform.SetParent(transform);
                data.gridArray[x, y] = block.transform;
            }
    }

    public bool IsValidPosition(Transform blockParent)
    {
        foreach (Transform child in blockParent)
        {
            Vector2 pos = child.position;
            int x = Mathf.RoundToInt(pos.x);
            int y = Mathf.RoundToInt(pos.y);

            if (x < 0 || x >= data.width || y < 0) return false;
            if (y >= data.height) continue;
            if (data.gridArray[x, y] != null) return false;
        }
        return true;
    }

    public void AddToGrid(Transform blockParent)
    {
        foreach (Transform child in blockParent)
        {
            int x = Mathf.RoundToInt(child.position.x);
            int y = Mathf.RoundToInt(child.position.y);

            if (x < 0 || x >= data.width || y < 0) continue;
            //--- 2026-06-25 테트리스식 게임오버: 블록이 그리드 천장 위(height 이상)로 넘쳐 락되면 오버플로
            if (y >= data.height) { _overflowed = true; continue; }
            data.gridArray[x, y] = child;

            // 은폐 중이면 새로 들어온 블록도 가림
            if (IsBlind)
            {
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
            }
        }
    }

    private bool _overflowed = false;

    //--- 2026-06-25 테트리스식: 블록이 그리드 밖(천장 위)으로 넘쳐 락됐을 때만 게임오버
    // 기존: 맨 윗줄(height-1)에 블록 닿으면 게임오버 → 너무 빡빡해서 변경
    // public bool IsGameOver() { for (int x=0; x<data.width; x++) if (data.gridArray[x, data.height-1] != null) return true; return false; }
    public bool IsGameOver() => _overflowed;

    public bool IsValidIndex(int x, int y)
    {
        return x >= 0 && x < data.width && y >= 0 && y < data.height;
    }
}
