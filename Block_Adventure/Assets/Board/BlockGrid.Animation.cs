using UnityEngine;
using System;
using System.Collections;

public partial class BlockGrid
{
    IEnumerator AnimateAndDestroy(Transform blockTransform)
    {
        if (blockTransform == null) yield break;

        SpriteRenderer sr = blockTransform.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = new Color(1f, 1f, 1f, 1f);

        yield return new WaitForSeconds(destroyDuration);

        if (blockTransform != null)
            Destroy(blockTransform.gameObject);
    }

    //--- 2026-07-09 이동 보간 세대 카운터. 피벗 등 판 전체 연출 시작 시 +1 → 진행 중이던 SmoothMove 전부 중단(회전과 충돌해 블록 튀는 프레임 방지)
    int _moveGen;

    IEnumerator SmoothMove(Transform block, Vector3 targetPos)
    {
        int gen = _moveGen;   // 시작 시점 세대 기억 — 판 연출이 시작되면 즉시 손 뗌
        Vector3 startPos = block.position;
        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            if (block == null || gen != _moveGen) yield break;
            block.position = Vector3.Lerp(startPos, targetPos, elapsed / dropDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (block != null && gen == _moveGen) block.position = targetPos;
    }

    //--- 2026-07-09 진행 중인 낙하/이동 보간 전부 취소하고 모든 블록을 데이터 좌표에 즉시 스냅 (피벗 시작 전 호출)
    void CancelBlockMoves()
    {
        _moveGen++;
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null)
                    data.gridArray[x, y].position = new Vector3(x, y, 0);
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
                        StartCoroutine(SmoothMove(block, correctPos));
                }
            }
        }
    }

    IEnumerator WaitAnimations(Action onComplete)
    {
        yield return new WaitForSeconds(dropDuration + 0.05f);
        onComplete?.Invoke();
    }
}
