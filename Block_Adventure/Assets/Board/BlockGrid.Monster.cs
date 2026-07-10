using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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

        //--- 2026-07-09 컨텍스트 튜토리얼: 첫 회색 줄 생성 순간
        ContextTutorial.Show("gray", new Vector3((data.width - 1) / 2f, 0f, 0f), 3.2f, "회색 블록",
            "몬스터가 만든 회색 블록은 색깔 매칭이 안 됩니다.\n컬러 블록과 함께 가로 한 줄을 꽉 채워야 지워져요.");
    }

    //--- 2026-06-24 [몬스터 패턴] 은폐: N턴 동안 쌓인 블록을 안 보이게. 안경 유물로 무효.
    private int _blindTurns = 0;
    public bool IsBlind => _blindTurns > 0;

    void OnEnable()
    {
        GameEvents.OnTurnStart += TickBlind; GameEvents.OnTurnStart += TickPivot;
        GameEvents.OnTurnStart += TickFreeze; GameEvents.OnTurnStart += TickTimeBombs;   //--- 2026-07-09 얼림/시한폭탄
    }
    void OnDisable()
    {
        GameEvents.OnTurnStart -= TickBlind; GameEvents.OnTurnStart -= TickPivot;
        GameEvents.OnTurnStart -= TickFreeze; GameEvents.OnTurnStart -= TickTimeBombs;
    }

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

    //--- 2026-07-09 확 사라지던 은폐(sr.enabled 토글)가 버그로 오해받음 → 알파 페이드 보간으로 변경
    // void SetGridVisible(bool visible)
    // {
    //     for (int x = 0; x < data.width; x++)
    //         for (int y = 0; y < data.height; y++)
    //         {
    //             Transform cell = data.gridArray[x, y];
    //             if (cell == null) continue;
    //             var sr = cell.GetComponent<SpriteRenderer>();
    //             if (sr != null) sr.enabled = visible;
    //         }
    // }
    Coroutine _gridFadeCo;

    void SetGridVisible(bool visible)
    {
        if (_gridFadeCo != null) StopCoroutine(_gridFadeCo);
        _gridFadeCo = StartCoroutine(FadeGridAlpha(visible ? 1f : 0f));
    }

    // 그리드에 놓인 모든 블록(아이콘 오버레이 포함)의 알파를 부드럽게 목표치로 (rgb는 안 건드림 — 얼음 틴트 등과 공존)
    IEnumerator FadeGridAlpha(float targetA)
    {
        var srs = new List<SpriteRenderer>();
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                Transform cell = data.gridArray[x, y];
                if (cell == null) continue;
                cell.GetComponentsInChildren(true, _srBuf);
                foreach (var sr in _srBuf) { sr.enabled = true; srs.Add(sr); }   // 구버전 enabled=false 잔재 복구
            }

        var startA = new float[srs.Count];
        for (int i = 0; i < srs.Count; i++) startA[i] = srs[i] != null ? srs[i].color.a : targetA;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(FxTuning.I.blindFadeDuration, 0.01f);
            float lin = Mathf.Clamp01(t);
            for (int i = 0; i < srs.Count; i++)
            {
                if (srs[i] == null) continue;   // 페이드 중 파괴된 블록
                var c = srs[i].color;
                c.a = Mathf.Lerp(startA[i], targetA, lin);
                srs[i].color = c;
            }
            yield return null;
        }
        _gridFadeCo = null;
    }
    static readonly List<SpriteRenderer> _srBuf = new List<SpriteRenderer>();

    //--- 2026-07-09 [몬스터 패턴] 얼림: 임의 색깔 블록 N개 빙결 — 색은 보이되 매칭 불가(줄클리어로만), turns턴 후 해동
    public void ApplyFreeze(int count, int turns)
    {
        var cands = new List<BlockColor>();
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                var cell = data.gridArray[x, y];
                if (cell == null) continue;
                var bc = cell.GetComponent<BlockColor>();
                if (bc != null && bc.colorID >= 1 && bc.colorID <= 5 && !bc.IsFrozen) cands.Add(bc);
            }
        int n = Mathf.Min(count, cands.Count);
        Vector3 firstPos = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            int pick = Random.Range(0, cands.Count);
            if (i == 0) firstPos = cands[pick].transform.position;
            cands[pick].Freeze(turns);
            cands.RemoveAt(pick);
        }
        Debug.Log($"🧊 블록 {n}개 빙결 ({turns}턴)");

        //--- 2026-07-09 컨텍스트 튜토리얼: 첫 빙결
        if (n > 0)
            ContextTutorial.Show("freeze", firstPos, 2.2f, "얼림!",
                $"언 블록은 색깔 매칭이 안 됩니다.\n줄 클리어로 지우거나, {turns}턴 뒤 녹을 때까지 기다리세요.");
    }

    void TickFreeze()
    {
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                var cell = data.gridArray[x, y];
                if (cell == null) continue;
                var bc = cell.GetComponent<BlockColor>();
                if (bc != null && bc.TickFrozen()) Debug.Log("🧊 해동");
            }
    }

    //--- 2026-07-09 [몬스터 패턴] 시한폭탄: 랜덤 열 스택 맨 위에 설치. N턴 내 제거(줄클리어/폭탄) 못 하면 폭발 → 주변 회색화.
    // 플레이어 체력이 없는 게임이라 "그리드 압박"형 페널티. 은폐 중에도 폭탄은 보임(카운터플레이 보장).
    public void ApplyTimeBomb(int turns)
    {
        // 랜덤 순서로 열을 훑어 첫 번째로 자리가 있는 열에 설치 (전부 꽉 찼으면 스킵)
        var cols = Enumerable.Range(0, data.width).OrderBy(_ => Random.value).ToList();
        foreach (int x in cols)
        {
            int y = 0;
            while (y < data.height && data.gridArray[x, y] != null) y++;
            if (y >= data.height) continue;   // 이 열은 꽉 참

            GameObject go = spawner.SpawnStaticBlock(x, y, TimeBombID, FxTuning.I.bombColor);
            go.transform.SetParent(this.transform);
            data.gridArray[x, y] = go.transform;
            go.AddComponent<TimeBombBlock>().Init(turns);
            Debug.Log($"💣 시한폭탄 설치 ({x},{y}) — {turns}턴 내 제거!");

            //--- 2026-07-09 컨텍스트 튜토리얼: 첫 시한폭탄
            ContextTutorial.Show("timebomb", new Vector3(x, y, 0f), 2.2f, "시한폭탄!",
                $"숫자가 0이 되기 전에 줄 클리어 등으로 없애세요.\n못 없애면 터져서 주변 블록이 회색이 됩니다.");
            return;
        }
        Debug.Log("💣 시한폭탄 설치 실패 — 빈 칸 없음");
    }

    void TickTimeBombs()
    {
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                var cell = data.gridArray[x, y];
                if (cell == null) continue;
                var tb = cell.GetComponent<TimeBombBlock>();
                if (tb != null && tb.Tick()) ExplodeBomb(x, y);
            }
    }

    // 폭발: 반경 내 색깔 블록 회색화(금은 남김) + 폭탄 소멸 + 셰이크
    void ExplodeBomb(int bx, int by)
    {
        var fx = FxTuning.I;
        int r = Tuning.TimeBombRadius;
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                int nx = bx + dx, ny = by + dy;
                if ((dx == 0 && dy == 0) || !IsValidIndex(nx, ny)) continue;
                var cell = data.gridArray[nx, ny];
                if (cell == null) continue;
                var bc = cell.GetComponent<BlockColor>();
                if (bc == null || bc.colorID == GoldBlockID || bc.colorID == 99) continue;
                bc.SetColorInfo(99, Color.gray);   // 회색화
            }

        var bombT = data.gridArray[bx, by];
        data.gridArray[bx, by] = null;
        if (bombT != null) Destroy(bombT.gameObject);
        if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(fx.bombExplodeShakeDuration, fx.bombExplodeShakeStrength);
        Debug.Log($"💥 시한폭탄 폭발 ({bx},{by}) — 주변 회색화");
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

    //--- 2026-07-01 씬 직렬화 값(0.4)이 Tuning 기본값(0.6)을 덮어쓰던 문제 → 파생 프로퍼티로 고정(직렬화 무시)
    //--- 2026-07-09 연출 값 FxTuning 에셋으로 이동 (실시간 튜닝)
    private float pivotDuration => FxTuning.I.pivotDuration;   // 판 90도 회전 시간

    //--- 2026-07-09 A안 연출로 개편: ① 예고 삐걱 흔들림 → ② 가속(EaseIn) 넘어짐 → ③ 착지 쿵(셰이크+스쿼시).
    // 배경은 더 이상 PivotRoot에 안 붙임(고정점 호 궤적으로 도는 위화감 제거) — 카메라만 보간.
    // 시작 전 CancelBlockMoves()로 진행 중 낙하 보간 강제 완료(회전 중 블록 튀는 프레임 방지).
    // 전체 블록을 임시 부모(모서리축)에 묶어 90도 회전 보간 + 카메라가 판을 따라감. 끝나면 데이터 회전 확정 후 새 중심으로 재정렬.
    IEnumerator PivotRoutine(bool ccw)
    {
        _pivotAnimating = true;

        CancelBlockMoves();   //--- 2026-07-09 낙하 보간과 회전 충돌 방지

        //--- 2026-06-26 고정점 회전(미끄러짐/재정렬 +20 없이 제자리서 90도 돌아 원점 안착)
        float pc = (ccw ? data.height - 1 : data.width - 1) / 2f;   // 고정점: 슬라이드 없이 새 격자에 정확히 안착
        Vector3 pivot = new Vector3(pc, pc, 0);

        //--- [회피] 판 회전 동안 플레이어 뒤로 물러남 / 복귀
        var player = FindFirstObjectByType<Player>();
        if (player != null) { if (ccw) player.Dodge(); else player.DodgeReturn(); }

        //--- 2026-06-29 액터 간격 이동을 회전과 동시에 (회전 후가 아니라) → "판에 합쳐졌다 물러남" 2단계 제거.
        // 회전 후 보드 폭 = 현재 data.height(스왑됨). 왼쪽(플레이어)은 안 움직이고, 가로판서 오른쪽(몬스터)만 비켜남.
        int newWidth = data.height;
        float actorDur = pivotDuration + (ccw ? FxTuning.I.pivotShakeDuration : 0f);   // 흔들림 포함 총 시간에 맞춤
        foreach (var a in FindObjectsByType<PivotActor>(FindObjectsSortMode.None)) a.MoveToGap(actorDur, newWidth);

        GameObject root = new GameObject("PivotRoot");
        root.transform.position = pivot;
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null) data.gridArray[x, y].SetParent(root.transform);

        //--- 2026-07-09 배경 회전 동참 폐기(호 궤적 위화감) → 카메라 보간만
        // var bgComp = FindFirstObjectByType<Background>();
        // Transform bgT = bgComp != null ? bgComp.transform : null;
        // Transform bgParent = bgT != null ? bgT.parent : null;
        // if (bgT != null) bgT.SetParent(root.transform);
        var bgComp = FindFirstObjectByType<Background>();

        float eAng = ccw ? 90f : -90f;

        // 카메라: 현재 → 회전 후 그리드 중심(width/height 스왑)으로 부드럽게 보간
        Camera cam = Camera.main;
        Vector3 camStart = cam != null ? cam.transform.position : Vector3.zero;
        float camZ = camStart.z;
        Vector3 camEnd = new Vector3((data.height - 1) / 2f, (data.width - 1) / 2f, camZ);

        // ① 예고: 삐걱 흔들림 (넘어질 때만 — 원복은 예고 없이 부드럽게 일어남)
        var fx = FxTuning.I;
        if (ccw)
        {
            float st = 0f;
            while (st < fx.pivotShakeDuration)
            {
                st += Time.deltaTime;
                float grow = st / fx.pivotShakeDuration;   // 점점 크게 삐걱
                float ang = Mathf.Sin(st * fx.pivotShakeFreq) * fx.pivotShakeAngle * grow;
                root.transform.rotation = Quaternion.Euler(0, 0, ang);
                yield return null;
            }
        }

        // ② 회전: 넘어짐은 가속 커브(무게감), 일어서기는 감속 커브. 커브는 FxTuning 에셋에서 조정. 카메라는 균일하게 SmoothStep.
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(pivotDuration, 0.01f);
            float lin = Mathf.Clamp01(t);
            float e = Mathf.Clamp01((ccw ? fx.pivotFallCurve : fx.pivotRiseCurve).Evaluate(lin));
            root.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, eAng, e));
            if (cam != null) cam.transform.position = Vector3.Lerp(camStart, camEnd, Mathf.SmoothStep(0f, 1f, lin));
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

        //--- 2026-07-09 배경 복원 불필요(회전 안 시킴)
        // if (bgT != null) { bgT.SetParent(bgParent); bgT.rotation = Quaternion.identity; }
        Destroy(root);

        //--- 2026-06-25 회전 배치 유지 (중력 적용 안 함)
        // ApplyGravity();

        // 배경/카메라 최종 정렬 (camEnd와 동일 위치라 점프 없음)
        if (bgComp != null) bgComp.ResizeAndReposition();

        // 미리보기/홀드 갱신
        if (spawner != null) spawner.RefreshVisuals();

        // ③ 쿵: 착지 카메라 셰이크 + 판 스쿼시(바닥 기준 살짝 눌렸다 복원) — 넘어질 때만
        if (ccw)
        {
            if (CameraShake.Instance != null)
                CameraShake.Instance.TriggerShake(fx.pivotLandShakeDuration, fx.pivotLandShakeStrength);
            yield return LandBounce();

            //--- 2026-07-09 컨텍스트 튜토리얼: 첫 판 회전 (연출 다 끝난 뒤 — 회전 중 멈추면 이상함)
            ContextTutorial.Show("pivot", new Vector3((data.width - 1) / 2f, (data.height - 1) / 2f, 0f),
                Mathf.Max(data.width, data.height) * 0.45f, "판 회전!",
                "몬스터가 판을 90도 넘어뜨렸어요!\n몇 턴이 지나면 판이 다시 일어납니다.");
        }

        _pivotAnimating = false;
    }

    //--- 2026-07-09 착지 스쿼시: 그리드 전체(원점=바닥 기준)를 세로로 살짝 눌렀다 복원 — "쿵" 무게감
    IEnumerator LandBounce()
    {
        var fx = FxTuning.I;
        Vector3 baseScale = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(fx.pivotBounceDuration, 0.01f);
            float lin = Mathf.Clamp01(t);
            // 눌림(squash)에서 출발해 1로 복원
            float y = Mathf.Lerp(fx.pivotBounceSquash, 1f, Mathf.SmoothStep(0f, 1f, lin));
            transform.localScale = new Vector3(baseScale.x, baseScale.y * y, baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
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
