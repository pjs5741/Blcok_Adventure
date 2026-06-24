using UnityEngine;
using System.Collections;

public partial class BlockGrid
{
    void ApplyGravity()
    {
        for (int x = 0; x < data.width; x++)
        {
            int writeY = 0;
            for (int y = 0; y < data.height; y++)
            {
                if (data.gridArray[x, y] != null)
                {
                    if (y != writeY)
                    {
                        data.gridArray[x, writeY] = data.gridArray[x, y];
                        data.gridArray[x, y] = null;
                        StartCoroutine(SmoothMove(data.gridArray[x, writeY], new Vector3(x, writeY, 0)));
                    }
                    writeY++;
                }
            }
        }
    }

    void ApplyBlockGravity()
    {
        int writeY = 0;

        for (int y = 0; y < data.height; y++)
        {
            if (!IsLineEmpty(y))
            {
                if (y != writeY)
                {
                    CopyRow(y, writeY);

                    for (int x = 0; x < data.width; x++)
                    {
                        Transform block = data.gridArray[x, writeY];
                        if (block != null)
                            StartCoroutine(SmoothMove(block, new Vector3(x, writeY, 0)));
                    }
                }
                writeY++;
            }
        }

        for (int y = writeY; y < data.height; y++)
        {
            ClearRow(y);
        }
    }

    bool IsLineFull(int y)
    {
        for (int x = 0; x < data.width; ++x) if (data.gridArray[x, y] == null) return false;
        return true;
    }

    //--- 2026-06-23 줄클리어 가능 줄: 꽉 찼고 + 회색(99)이 아닌 블록이 1개 이상.
    // 컬러가 하나라도 섞인 꽉 찬 줄은 회색까지 통째로 제거(테트리스 성립).
    // 회색으로만 꽉 찬 줄은 제외 — 중력으로 우연히 모인 것이므로 공짜로 안 터짐 (폭탄/회복실로만).
    bool IsLineClearable(int y)
    {
        bool hasNonGray = false;
        for (int x = 0; x < data.width; x++)
        {
            if (data.gridArray[x, y] == null) return false;
            if (GetColorID(data.gridArray[x, y]) != 99) hasNonGray = true;
        }
        return hasNonGray;
    }

    bool IsLineEmpty(int y)
    {
        for (int x = 0; x < data.width; x++)
            if (data.gridArray[x, y] != null) return false;
        return true;
    }

    void CopyRow(int sourceY, int targetY)
    {
        for (int x = 0; x < data.width; x++)
            data.gridArray[x, targetY] = data.gridArray[x, sourceY];
    }

    void ClearRow(int y)
    {
        for (int x = 0; x < data.width; x++) data.gridArray[x, y] = null;
    }
}
