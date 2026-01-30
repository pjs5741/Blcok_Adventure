using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random; // 리스트, 해시셋 사용을 위해 필수
// zz good
public class BlockGrid : MonoBehaviour
{
    public GridData data;
    public BlockSpawner spawner;

    [Header("Animation Settings")]
    public float dropDuration = 0.2f;
    public float destroyDuration = 0.3f;
    
    [Header("Game Status")]
    public int comboCount = 0;
    public float currentDamageMultiplier = 1f;
    public event Action<float> OnAttackTriggered;
    void Awake()
    {
        data = new GridData(11, 21);
    }

    // =================================================================
    // 🏗️ PART 1. 테트리스 기본 로직 (구축 & 줄 삭제)
    // =================================================================

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
    // 블록 확정 (콜백 포함)
   public void AddToGrid(Transform blockParent, Action onComplete)
    {
        foreach (Transform child in blockParent)
        {
            int x = Mathf.RoundToInt(child.position.x);
            int y = Mathf.RoundToInt(child.position.y);

            // 게임 오버 체크 (맨 위를 넘어가면)
            LoseGame();
            data.gridArray[x, y] = child;
        }
        
        StartCoroutine(ProcessTurn(onComplete));
    }

    // ★ [연쇄 폭발의 심장] 매칭이 없을 때까지 무한 반복하는 코루틴.
    // ★ [핵심] 턴 처리 루프 (모아서 한 번에 터뜨리기)
    IEnumerator ProcessTurn(Action onTurnEnd)
    {
        yield return new WaitForSeconds(0.05f);

        bool hasEvent = false;
        comboCount = 0; // 턴 시작할 때 콤보 초기화 (원하면 누적시켜도 됨)
        currentDamageMultiplier = 1f;

        do
        {
            hasEvent = false;

            // 1. 🕵️ 검거 단계: 터질 놈들 명단만 확보 (아직 안 터뜨림)
            HashSet<Transform> lineBlocks = GetLineClearBlocks();   // 테트리스 줄
            HashSet<Transform> matchBlocks = GetColorMatchBlocks(); // 애니팡 매칭

            // 2. ➕ 합치기 단계: 중복 제거하면서 하나로 합침
            HashSet<Transform> allToDestroy = new HashSet<Transform>(lineBlocks);
            allToDestroy.UnionWith(matchBlocks); // 합집합 (Union)

            // 3. ⚖️ 판정 단계: 뭔가 터질 게 있다면?
            if (allToDestroy.Count > 0)
            {
                hasEvent = true;
                comboCount++; // 콤보 증가!

                // 🔥 [요청하신 기능] 줄 + 색깔 동시에 터졌냐?
                if (lineBlocks.Count > 0 && matchBlocks.Count > 0)
                {
                    currentDamageMultiplier = 2.0f; // 데미지 2배 버프!
                    Debug.Log($"🚀 대박! 줄+색깔 동시 폭발! (데미지 {currentDamageMultiplier}배)");
                    
                    // 여기에 "Excellent!" 같은 특수 UI 이펙트 함수 호출하면 됨
                }
                else
                {
                    currentDamageMultiplier = 1.0f + (comboCount * 0.1f); // 콤보당 10% 증뎀
                    Debug.Log($"💥 {comboCount}콤보! ({allToDestroy.Count}개 파괴)");
                }
                
                OnAttackTriggered?.Invoke(currentDamageMultiplier); 

                Debug.Log($"💥 {comboCount}콤보! 배율: {currentDamageMultiplier}배");

                // 4. 💣 집행 단계: 진짜 파괴 실행
                foreach (Transform t in allToDestroy)
                {
                    if (t == null) continue;

                    // 점수 계산 로직이 있다면 여기서 currentDamageMultiplier 곱해서 적용
                    // GameManager.Score += 100 * currentDamageMultiplier;

                    // 데이터 삭제 (중력 인식을 위해)
                    int tx = Mathf.RoundToInt(t.position.x);
                    int ty = Mathf.RoundToInt(t.position.y);
                    if (IsValidIndex(tx, ty) && data.gridArray[tx, ty] == t) 
                        data.gridArray[tx, ty] = null;

                    // 비주얼 실행 (빛나고 -> 터짐)
                    StartCoroutine(AnimateAndDestroy(t));
                }

                // 터지는 연출 대기
                if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(0.2f, 0.3f);
                yield return new WaitForSeconds(destroyDuration + 0.05f);

                // 빈칸 채우기 (중력)
                if (matchBlocks.Count > 0)
                {
                    ApplyGravity();   
                }
                else
                {
                    ApplyBlockGravity();
                }

                // 떨어지는 시간 대기
                yield return new WaitForSeconds(dropDuration + 0.1f);
            }
            
        } while (hasEvent);

        onTurnEnd?.Invoke();
    }
    
    HashSet<Transform> GetLineClearBlocks()
    {
        HashSet<Transform> targetBlocks = new HashSet<Transform>();

        for (int y = 0; y < data.height; ++y) 
        { 
            if (IsLineFull(y))
            {
                // 이 줄에 있는 모든 블록을 명단에 추가
                for (int x = 0; x < data.width; ++x)
                {
                    if (data.gridArray[x, y] is not null) targetBlocks.Add(data.gridArray[x, y]);
                }
            }
        }
        return targetBlocks;
    }
    
    HashSet<Transform> GetColorMatchBlocks()
    {
        bool[,] visited = new bool[data.width, data.height];
        HashSet<Transform> targetBlocks = new HashSet<Transform>();

        int[] dx = { 0, 0, -1, 1 }; 
        int[] dy = { 1, -1, 0, 0 };
        for (int x = 0; x < data.width; ++x)
        {
            for (int y = 0; y < data.height; ++y)
            {
                if (data.gridArray[x, y] == null || visited[x, y]) continue;
                
                int startColor = GetColorID(data.gridArray[x, y]);
                if (startColor == 0 || startColor == 99) continue;

                List<Transform> currentGroup = new List<Transform>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();

                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;
                currentGroup.Add(data.gridArray[x, y]);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = current.x + dx[i];
                        int ny = current.y + dy[i];
                        if (!IsValidIndex(nx, ny)) continue;
                        if (visited[nx, ny] || data.gridArray[nx, ny] == null) continue;
                        if (GetColorID(data.gridArray[nx, ny]) != startColor) continue;

                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                        currentGroup.Add(data.gridArray[nx, ny]);
                    }
                }

                // 3개 이상이면 명단에 등록
                if (currentGroup.Count >= 5)
                {
                    foreach (Transform t in currentGroup) targetBlocks.Add(t);
                }
            }
        }
        return targetBlocks;
    }

    // =================================================================
    // 🤹 PART 2. 애니팡 로직 (스왑 & 매칭 & 중력)
    // =================================================================

    // 좌표 유효성 검사 헬퍼
    public bool IsValidIndex(int x, int y)
    {
        return x >= 0 && x < data.width && y >= 0 && y < data.height;
    }

    // ★ [사용자 요청] 스왑 함수 구현
    /*public void SwapBlocks(int x1, int y1, int x2, int y2)
    {
        // 둘 다 비어있으면 무시
        if (gridArray[x1, y1] == null && gridArray[x2, y2] == null) return;

        // 1. 데이터 교환
        Transform temp = gridArray[x1, y1];
        gridArray[x1, y1] = gridArray[x2, y2];
        gridArray[x2, y2] = temp;

        // 2. 눈에 보이는 위치 교환 (즉시 이동)
        if (gridArray[x1, y1] != null) gridArray[x1, y1].position = new Vector3(x1, y1, 0);
        if (gridArray[x2, y2] != null) gridArray[x2, y2].position = new Vector3(x2, y2, 0);

        // 3. 매칭 검사 및 실패 시 복구 (애니팡 룰)
        /*bool hasMatch = CheckForMatches();

        if (!hasMatch)
        {
            // 꽝! 원상복구
            // 다시 데이터 교환
            temp = gridArray[x1, y1];
            gridArray[x1, y1] = gridArray[x2, y2];
            gridArray[x2, y2] = temp;

            // 다시 위치 교환
            if (gridArray[x1, y1] != null) gridArray[x1, y1].position = new Vector3(x1, y1, 0);
            if (gridArray[x2, y2] != null) gridArray[x2, y2].position = new Vector3(x2, y2, 0);

            Debug.Log("❌ 매칭 실패! 제자리로.");
        }#1#
    }*/

    // N개 이상 매칭 검사 (성공하면 true 리턴)
    // ==========================================
    // 🧠 BFS 기반 매칭 (연결된 덩어리 찾기)
    // ==========================================
    // 중력 (빈칸 채우기)
    void ApplyGravity()
    {
        for (int x = 0; x < data.width; x++)
        {
            int writeY = 0;
            for (int y = 0; y < data.height; y++)
            {
                if (data.gridArray[x, y] is not null)
                {
                    if (y != writeY)
                    {
                        data.gridArray[x, writeY] = data.gridArray[x, y];
                        data.gridArray[x, y] = null;

                        // 부드럽게 떨어지기
                        StartCoroutine(SmoothMove(data.gridArray[x, writeY], new Vector3(x, writeY, 0)));
                    }
                    writeY++;
                }
            }
        }
    }

    // 색깔 ID 가져오기
    int GetColorID(Transform block)
    {
        BlockColor info = block.GetComponent<BlockColor>();
        if (info != null) return info.colorID;
        return 0;
    }

    // ✨ 블록 파괴 연출 코루틴 (하얗게 빛나다가 터짐)
    IEnumerator AnimateAndDestroy(Transform blockTransform)
    {
        if (blockTransform == null) yield break;

        SpriteRenderer sr = blockTransform.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(1f, 1f, 1f, 1f); // 하얗게 질리기
        }

        // ★ 설정한 시간만큼 정확히 대기 (이 동안은 절대 안 움직임)
        yield return new WaitForSeconds(destroyDuration);

        if (blockTransform != null)
        {
            Destroy(blockTransform.gameObject);
        }
    }
    // =================================================================
    // 🛠️ 공통 유틸리티
    // =================================================================

    bool IsLineFull(int y)
    {
        for (int x = 0; x < data.width; ++x) if (data.gridArray[x, y] is null) return false;
        return true;
    }

    void DecreaseRowsAboveDataOnly(int startY)
    {
        for (int y = startY; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                if (data.gridArray[x, y] != null)
                {
                    data.gridArray[x, y - 1] = data.gridArray[x, y];
                    data.gridArray[x, y] = null;
                }
            }
        }
    }

    void SyncVisualPositions()
    {
        for (int x = 0; x < data.width; x++)
        {
            for (int y = 0; y < data.height; y++)
            {
                if (data.gridArray[x, y] != null)
                {
                    Transform block = data.gridArray[x, y];
                    Vector3 correctPos = new Vector3(x, y, 0);
                    if (Vector3.Distance(block.position, correctPos) > 0.01f)
                    {
                        StartCoroutine(SmoothMove(block, correctPos));
                    }
                }
            }
        }
    }

    IEnumerator WaitAnimations(Action onComplete)
    {
        yield return new WaitForSeconds(dropDuration + 0.05f);
        onComplete?.Invoke();
    }

    IEnumerator SmoothMove(Transform block, Vector3 targetPos)
    {
        Vector3 startPos = block.position;
        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            if (block == null) yield break;
            block.position = Vector3.Lerp(startPos, targetPos, elapsed / dropDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (block != null) block.position = targetPos;
    }
    
    // 테트리스 식: 윗줄을 통째로 당겨 내리기
    void ApplyBlockGravity()
    {
        int writeY = 0; 

        // 2. 바닥(0)부터 꼭대기까지 훑어 올라감
        for (int y = 0; y < data.height; y++)
        {
            // 이 줄이 "살아있는 줄"인가? (빈 줄이 아님)
            if (!IsLineEmpty(y))
            {
                // 현재 줄(y)을 목적지(writeY)로 이동해야 한다면?
                if (y != writeY)
                {
                    // 데이터 복사 (y -> writeY)
                    CopyRow(y, writeY);

                    // ★ [핵심] 이동한 줄에 있는 모든 블록들에게 "SmoothMove" 명령
                    for (int x = 0; x < data.width; x++)
                    {
                        Transform block = data.gridArray[x, writeY];
                        if (block != null)
                        {
                            // 목표 위치: (x, writeY)
                            StartCoroutine(SmoothMove(block, new Vector3(x, writeY, 0)));
                        }
                    }
                }
                
                // 다음 이사 갈 층수 한 칸 올림
                writeY++; 
            }
            // 만약 빈 줄(IsLineEmpty)이라면? 
            // writeY는 안 올라가고 y만 올라감 -> 자연스럽게 그 줄은 씹히고 윗줄이 당겨짐
        }

        // 3. 이사가 다 끝났으니, writeY 위쪽(남은 윗부분)은 싹 청소 (null 처리)
        for (int y = writeY; y < data.height; y++)
        {
            ClearRow(y);
        }
    }
    
    bool IsLineEmpty(int y)
    {
        for (int x = 0; x < data.width; x++)
            if (data.gridArray[x, y] != null) return false;
        return true;
    }

    // [보조 함수] 줄 복사 (단, 원본 삭제는 안 함 - 나중에 ClearRow로 한방에 처리)
    void CopyRow(int sourceY, int targetY)
    {
        for (int x = 0; x < data.width; x++)
        {
            data.gridArray[x, targetY] = data.gridArray[x, sourceY];
            // 주의: 여기서 sourceY를 null로 만들지 않음 (덮어쓰기 방식)
        }
    }

    // [보조 함수] 줄 비우기
    void ClearRow(int y)
    {
        for (int x = 0; x < data.width; x++) data.gridArray[x, y] = null;
    }
    
    //몬스터 공격용 함수
    public void ShiftAllRowsUp()
    {
        for (int y = data.height - 2; y >= 0; y--)
        {
            for (int x = 0; x < data.width; x++)
            {
                Transform block = data.gridArray[x, y];
                if (block is not null)
                {
                    data.gridArray[x, y + 1] = block;
                    data.gridArray[x, y] = null;
                    block.position += new Vector3(0, 1, 0); 
                }
            }
        }
    }

    private void LoseGame()
    {
        for (int x = 0; x < data.width; x++)
        {
            if (data.gridArray[x, data.height - 1] != null)
            {
                Debug.Log("💀 게임 오버! (블록이 천장에 닿았습니다)");
                // GameManager.Instance.GameOver(); // 실제 게임오버 처리는 여기서 호출
                return;
            }
        }
    }
    public void ShiftAndCreateRow(int grayColorID, Color grayColor)
    {
        // 1. 먼저 죽는지 확인 (올리기 전에 검사)
        LoseGame(); 

        // 2. 전체 블록 위로 한 칸씩 이사 (Shift)
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

        // 3. 아랫줄 채우기 (Create with Spawner)
        int holeIndex = Random.Range(0, data.width); // 구멍 뚫을 위치

        for (int x = 0; x < data.width; x++)
        {
            if (x == holeIndex) continue; // 구멍은 비워둠

            // ★ 스포너한테 "회색 블록 하나 만들어줘" 요청
            GameObject newBlockObj = spawner.SpawnStaticBlock(x, 0, grayColorID, grayColor);
            
            // 정리 정돈 (Grid 자식으로, 데이터 등록)
            newBlockObj.transform.SetParent(this.transform);
            data.gridArray[x, 0] = newBlockObj.transform;
        }
        
        // 다 올리고 나서도 혹시 삐져나갔는지 체크
        LoseGame();
    }
}
