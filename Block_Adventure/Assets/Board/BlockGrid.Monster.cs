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

    //--- 2026-07-01 씬 직렬화 값(0.4)이 Tuning 기본값(0.6)을 덮어쓰던 문제 → Tuning 파생으로 고정(직렬화 무시)
    private float pivotDuration => Tuning.PivotDuration;   // 판 90도 회전 시간

    // 전체 블록을 임시 부모(모서리축)에 묶어 90도 회전 보간 + 카메라가 판을 따라감. 끝나면 데이터 회전 확정 후 새 중심으로 재정렬.
    IEnumerator PivotRoutine(bool ccw)
    {
        _pivotAnimating = true;

        //--- 2026-06-26 단순·확실 버전: 고정점 회전(미끄러짐/재정렬 +20 없이 제자리서 90도 돌아 원점 안착)
        // + 카메라는 시작↔끝 프레임 부드러운 보간 + 액터는 회전 후 보드 폭 기준 간격으로 이동.
        // (이전 "왼쪽 쿵 + 카메라 호 추적 + 재정렬" 방식은 액터 휩쓸림/타이밍 충돌로 폐기)
        float pc = (ccw ? data.height - 1 : data.width - 1) / 2f;   // 고정점: 슬라이드 없이 새 격자에 정확히 안착
        Vector3 pivot = new Vector3(pc, pc, 0);

        //--- [회피] 판 회전 동안 플레이어 뒤로 물러남 / 복귀
        var player = FindFirstObjectByType<Player>();
        if (player != null) { if (ccw) player.Dodge(); else player.DodgeReturn(); }

        //--- 2026-06-29 액터 간격 이동을 회전과 동시에 (회전 후가 아니라) → "판에 합쳐졌다 물러남" 2단계 제거.
        // 회전 후 보드 폭 = 현재 data.height(스왑됨). 왼쪽(플레이어)은 안 움직이고, 가로판서 오른쪽(몬스터)만 비켜남.
        int newWidth = data.height;
        foreach (var a in FindObjectsByType<PivotActor>(FindObjectsSortMode.None)) a.MoveToGap(pivotDuration, newWidth);

        GameObject root = new GameObject("PivotRoot");
        root.transform.position = pivot;
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null) data.gridArray[x, y].SetParent(root.transform);

        // 배경도 같이 회전 (끝나면 복원)
        var bgComp = FindFirstObjectByType<Background>();
        Transform bgT = bgComp != null ? bgComp.transform : null;
        Transform bgParent = bgT != null ? bgT.parent : null;
        if (bgT != null) bgT.SetParent(root.transform);

        float eAng = ccw ? 90f : -90f;

        // 카메라: 현재 → 회전 후 그리드 중심(width/height 스왑)으로 부드럽게 보간
        Camera cam = Camera.main;
        Vector3 camStart = cam != null ? cam.transform.position : Vector3.zero;
        float camZ = camStart.z;
        Vector3 camEnd = new Vector3((data.height - 1) / 2f, (data.width - 1) / 2f, camZ);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(pivotDuration, 0.01f);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            root.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, eAng, e));
            if (cam != null) cam.transform.position = Vector3.Lerp(camStart, camEnd, e);
            yield return null;
        }

        // 데이터 회전 확정 + 블록 정수좌표 snap (고정점 회전이라 이미 데이터 위치)
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

        // 배경 복원 후 root 파괴
        if (bgT != null) { bgT.SetParent(bgParent); bgT.rotation = Quaternion.identity; }
        Destroy(root);

        //--- 2026-06-25 회전 배치 유지 (중력 적용 안 함)
        // ApplyGravity();

        // 배경/카메라 최종 정렬 (camEnd와 동일 위치라 점프 없음)
        if (bgComp != null) bgComp.ResizeAndReposition();

        // 미리보기/홀드 갱신
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
