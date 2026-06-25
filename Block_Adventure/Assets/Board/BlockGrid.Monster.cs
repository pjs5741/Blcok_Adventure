using UnityEngine;
using System.Collections;
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

    //--- 2026-06-24 [몬스터 패턴] 중력방향 전환: 한쪽으로 한 번 밀고 끝 (지속 없음). toLeft=true 왼쪽, false 오른쪽
    public void ApplyGravityShift(bool toLeft) => StartCoroutine(GravityShiftRoutine(toLeft));

    IEnumerator GravityShiftRoutine(bool toLeft)
    {
        _gravityShifting = true;
        ApplyHorizontalGravity(toLeft);
        Debug.Log(toLeft ? "⬅ 중력 왼쪽으로 밀기" : "➡ 중력 오른쪽으로 밀기");
        yield return new WaitForSeconds(dropDuration + 0.1f);   // 보간 이동 끝까지 대기
        _gravityShifting = false;
    }

    //--- 2026-06-24 [몬스터 패턴] 피벗: 판 90도 회전, N턴 후 원복
    private int _pivotTurns = 0;
    private bool _pivotAnimating = false;
    private bool _gravityShifting = false;
    public bool IsPivotAnimating => _pivotAnimating;
    public bool IsBusy => _pivotAnimating || _gravityShifting;   // 그리드 변형 연출 중 다음 단계(스폰) 침범 금지용

    public void ApplyPivot(int turns)
    {
        if (_pivotTurns > 0) return;   // 이미 피벗 중이면 무시
        _pivotTurns = turns;
        StartCoroutine(PivotRoutine(ccw: true));    // 반시계 — 판이 플레이어 쪽으로 쓰러지는 느낌
        Debug.Log($"🔄 피벗 90도 회전 {turns}턴");
    }

    void TickPivot()
    {
        if (_pivotTurns <= 0) return;
        _pivotTurns--;
        if (_pivotTurns <= 0)
        {
            StartCoroutine(PivotRoutine(ccw: false));   // 원복 (시계로 되돌림)
            Debug.Log("🔄 피벗 원복");
        }
    }

    [Header("Pivot")]
    public float pivotDuration = 0.4f;

    // 전체 블록을 임시 부모에 묶어 부모를 90도 회전(+중심 이동) 보간, 카메라도 동시에. 끝나면 데이터 회전 확정.
    IEnumerator PivotRoutine(bool ccw)
    {
        _pivotAnimating = true;
        float oldCx = (data.width - 1) / 2f, oldCy = (data.height - 1) / 2f;

        GameObject root = new GameObject("PivotRoot");
        root.transform.position = new Vector3(oldCx, oldCy, 0);
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null) data.gridArray[x, y].SetParent(root.transform);

        // 배경도 같이 회전시키려고 임시로 root에 묶음 (끝나면 복원)
        var bgComp = FindFirstObjectByType<Background>();
        Transform bgT = bgComp != null ? bgComp.transform : null;
        Transform bgParent = bgT != null ? bgT.parent : null;
        if (bgT != null) bgT.SetParent(root.transform);

        // 회전 후(width/height 스왑) 새 판 중심
        int newW = data.height, newH = data.width;
        float newCx = (newW - 1) / 2f, newCy = (newH - 1) / 2f;

        Vector3 sPos = root.transform.position, ePos = new Vector3(newCx, newCy, 0);
        float eAng = ccw ? 90f : -90f;

        // 카메라는 놔둔다 — 회전 전 위치/줌을 기억했다가 끝에 그대로 복원
        Camera cam = Camera.main;
        Vector3 camKeep = cam != null ? cam.transform.position : Vector3.zero;
        float camSizeKeep = cam != null ? cam.orthographicSize : 0f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(pivotDuration, 0.01f);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            root.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, eAng, e));
            root.transform.position = Vector3.Lerp(sPos, ePos, e);
            yield return null;
        }

        // 데이터 회전 확정 + 블록을 정수 좌표로 snap
        RotateGridData(ccw);
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                var c = data.gridArray[x, y];
                if (c == null) continue;
                c.SetParent(this.transform);
                c.position = new Vector3(x, y, 0);
                c.rotation = Quaternion.identity;
            }

        // 배경 복원 (회전 풀고 원래 부모로) → Destroy(root) 전에 빼야 같이 파괴 안 됨
        if (bgT != null) { bgT.SetParent(bgParent); bgT.rotation = Quaternion.identity; }
        Destroy(root);

        //--- 2026-06-25 회전된 배치 그대로 유지 (중력 적용 안 함). 게임오버가 테트리스식(밖 넘침)이라 천장 닿아도 안전
        // ApplyGravity();

        if (bgComp != null) bgComp.ResizeAndReposition();

        // 카메라는 놔둠 — ResizeAndReposition이 옮긴 걸 회전 전 상태로 되돌림
        if (cam != null) { cam.transform.position = camKeep; cam.orthographicSize = camSizeKeep; }

        // 회전으로 그리드 크기가 바뀌었으니 미리보기/홀드(고스트블럭) 위치 갱신
        if (spawner != null) spawner.RefreshVisuals();

        _pivotAnimating = false;
    }

    void RotateGridData(bool ccw)
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
                if (ccw) { nx = oldH - 1 - y; ny = x; }       // 반시계
                else     { nx = y; ny = oldW - 1 - x; }       // 시계(원복)
                rot[nx, ny] = old[x, y];
            }

        data.gridArray = rot;
        data.width = newW;
        data.height = newH;
    }
}
