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
