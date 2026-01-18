using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic; // 리스트, 해시셋 사용을 위해 필수

public class BlockGrid : MonoBehaviour
{
    [Header("Grid Info")]
    public int width = 11;
    public int height = 21;
    public Transform[,] gridArray;

    [Header("Animation Settings")]
    public float dropDuration = 0.2f;
    public float shakeTime = 0.15f;
    public float shakePower = 0.2f;

    void Awake()
    {
        gridArray = new Transform[width, height];
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

            if (x < 0 || x >= width || y < 0) return false;
            if (y >= height) continue;
            if (gridArray[x, y] != null) return false;
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

            if (y >= height) { Debug.Log("💀 GAME OVER"); return; }
            gridArray[x, y] = child;
        }

        // 1. 테트리스 줄 삭제 검사
        bool linesCleared = CheckAndClearLines();

        // ★ [추가] 2. 색깔 매칭 검사 (착지하면서 바로 터지는 콤보!)
        // 줄 삭제가 안 일어났을 때만 검사하거나, 둘 다 하거나 기획 나름이지만
        // 일단은 둘 다 체크해서 터뜨립니다.
        bool matchCleared = CheckForMatches();

        // 줄이 지워지거나 색깔이 터졌으면 애니메이션 대기
        if (linesCleared || matchCleared)
        {
            StartCoroutine(WaitAnimations(onComplete));
        }
        else
        {
            onComplete?.Invoke();
        }
    }
    bool CheckAndClearLines()
    {
        bool anyLineCleared = false;
        for (int y = 0; y < height; y++)
        {
            if (IsLineFull(y))
            {
                anyLineCleared = true;
                DeleteLine(y);
                DecreaseRowsAboveDataOnly(y + 1);
                y--;
            }
        }

        if (anyLineCleared)
        {
            SyncVisualPositions();
            if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(shakeTime, shakePower);
        }
        return anyLineCleared;
    }

    // =================================================================
    // 🤹 PART 2. 애니팡 로직 (스왑 & 매칭 & 중력)
    // =================================================================

    // 좌표 유효성 검사 헬퍼
    public bool IsValidIndex(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // ★ [사용자 요청] 스왑 함수 구현
    public void SwapBlocks(int x1, int y1, int x2, int y2)
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
        bool hasMatch = CheckForMatches();

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
        }
    }

    // N개 이상 매칭 검사 (성공하면 true 리턴)
    // ==========================================
    // 🧠 BFS 기반 매칭 (연결된 덩어리 찾기)
    // ==========================================

    bool CheckForMatches()
    {
        bool[,] visited = new bool[width, height]; // 방문 체크 (중복 검사 방지)
        HashSet<Transform> blocksToDestroy = new HashSet<Transform>(); // 삭제할 놈들 모음
        bool hasMatch = false;

        // 상하좌우 탐색용
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { 1, -1, 0, 0 };

        // 전체 그리드를 돌면서 탐색
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 빈칸이거나, 이미 검사한 곳이거나, 색깔이 없으면 패스
                if (gridArray[x, y] == null || visited[x, y]) continue;

                int startColor = GetColorID(gridArray[x, y]);
                if (startColor == 0) continue;

                // --- BFS 시작 (Flood Fill) ---
                List<Transform> currentGroup = new List<Transform>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();

                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;
                currentGroup.Add(gridArray[x, y]);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();

                    // 4방향(상하좌우) 확인
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = current.x + dx[i];
                        int ny = current.y + dy[i];

                        // 맵 밖이면 패스
                        if (!IsValidIndex(nx, ny)) continue;

                        // 이미 방문했거나, 블록이 없거나, 색이 다르면 패스
                        if (visited[nx, ny] || gridArray[nx, ny] == null) continue;
                        if (GetColorID(gridArray[nx, ny]) != startColor) continue;

                        // 같은 색 덩어리 발견!
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                        currentGroup.Add(gridArray[nx, ny]);
                    }
                }

                // --- BFS 끝 ---

                // 뭉친 개수가 3개 이상이면(원하면 5개로 수정) 파괴 리스트에 등록
                if (currentGroup.Count >= 5)
                {
                    foreach (Transform t in currentGroup)
                    {
                        blocksToDestroy.Add(t);
                    }
                    hasMatch = true;
                }
            }
        }

        // 찾은 놈들 일괄 삭제
        if (hasMatch)
        {
            foreach (Transform t in blocksToDestroy)
            {
                if (t == null) continue;
                int tx = Mathf.RoundToInt(t.position.x);
                int ty = Mathf.RoundToInt(t.position.y);

                if (IsValidIndex(tx, ty) && gridArray[tx, ty] == t)
                {
                    gridArray[tx, ty] = null;
                }
                Destroy(t.gameObject);
            }

            Debug.Log($"🔥 BFS 완료: {blocksToDestroy.Count}개 블록 삭제됨!");

            if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(0.15f, 0.2f);
            ApplyGravity(); // 삭제했으니 빈칸 채우기
        }

        return hasMatch;
    }

    // 중력 (빈칸 채우기)
    void ApplyGravity()
    {
        for (int x = 0; x < width; x++)
        {
            int writeY = 0;
            for (int y = 0; y < height; y++)
            {
                if (gridArray[x, y] != null)
                {
                    if (y != writeY)
                    {
                        gridArray[x, writeY] = gridArray[x, y];
                        gridArray[x, y] = null;

                        // 부드럽게 떨어지기
                        StartCoroutine(SmoothMove(gridArray[x, writeY], new Vector3(x, writeY, 0)));
                    }
                    writeY++;
                }
            }
        }
        // 연쇄 폭발 체크 (0.4초 뒤)
        Invoke("CheckForMatchesDelayed", 0.4f);
    }

    // Invoke용 래퍼 함수 (Invoke는 반환형 있는 함수 못 부름)
    void CheckForMatchesDelayed() { CheckForMatches(); }

    // 색깔 ID 가져오기
    int GetColorID(Transform block)
    {
        BlockColor info = block.GetComponent<BlockColor>();
        if (info != null) return info.colorID;
        return 0;
    }

    // =================================================================
    // 🛠️ 공통 유틸리티
    // =================================================================

    bool IsLineFull(int y)
    {
        for (int x = 0; x < width; x++) if (gridArray[x, y] == null) return false;
        return true;
    }

    void DeleteLine(int y)
    {
        for (int x = 0; x < width; x++)
        {
            if (gridArray[x, y] != null)
            {
                Destroy(gridArray[x, y].gameObject);
                gridArray[x, y] = null;
            }
        }
    }

    void DecreaseRowsAboveDataOnly(int startY)
    {
        for (int y = startY; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (gridArray[x, y] != null)
                {
                    gridArray[x, y - 1] = gridArray[x, y];
                    gridArray[x, y] = null;
                }
            }
        }
    }

    void SyncVisualPositions()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (gridArray[x, y] != null)
                {
                    Transform block = gridArray[x, y];
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
}