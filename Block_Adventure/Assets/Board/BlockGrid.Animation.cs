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
