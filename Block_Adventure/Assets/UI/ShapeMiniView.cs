using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//--- 2026-07-01 블록 모양을 UI(작은 사각형들)로 그림. 월드 스프라이트를 캔버스에 얹는 문제 회피 → 어느 캔버스든 안정적.
// 프리팹의 셀 로컬좌표를 읽어 색칠된 UI 셀로 재현. 덱 보기/카드 등에서 재사용.
public static class ShapeMiniView
{
    public static void Build(RectTransform host, string shape, int colorID, float cellPx = 22f)
    {
        var prefab = LoadPrefab(shape);
        if (prefab == null) return;

        var offsets = ReadOffsets(prefab);
        if (offsets.Count == 0) return;

        float cx = 0, cy = 0;
        foreach (var o in offsets) { cx += o.x; cy += o.y; }
        cx /= offsets.Count; cy /= offsets.Count;

        Color col = BlockColors.Get(colorID);
        foreach (var o in offsets)
        {
            var img = UIBuilder.Image(host, "Cell", col);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellPx - 3, cellPx - 3);
            rt.anchoredPosition = new Vector2((o.x - cx) * cellPx, (o.y - cy) * cellPx);
            // 아이콘 이미지는 막판 리소스 작업 때 — 지금은 색칠 셀만
        }
    }

    static List<Vector2> ReadOffsets(GameObject prefab)
    {
        var list = new List<Vector2>();
        foreach (Transform c in prefab.transform)
            list.Add(new Vector2(Mathf.Round(c.localPosition.x), Mathf.Round(c.localPosition.y)));
        return list;
    }

    static GameObject LoadPrefab(string name)
        => Resources.Load<GameObject>($"Block/{name}") ?? Resources.Load<GameObject>($"RewardBlock/{name}");
}
