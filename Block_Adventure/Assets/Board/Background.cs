using UnityEngine;

public class Background : MonoBehaviour
{
    [Header("Target")]
    public BlockGrid targetGrid;

    [Header("Camera Setting")]
    public bool autoCenterCamera = true;
    public float padding = 5f;
    public int cameraDepth = -10;

    void Start()
    {
        if (targetGrid == null) return;
        ResizeAndReposition();
    }

    [ContextMenu("Fit to Grid & Center Camera")]
    public void ResizeAndReposition()
    {
        if (targetGrid == null)
        {
            Debug.LogError("Grid�� �������ּ���!");
            return;
        }

        // �� [���� ����] (���� - 1) / 2f
        // Width 10 -> (9 / 2) = 4.5 (��Ȯ�� �߾�)
        // Width 11 -> (10 / 2) = 5.0 (��Ȯ�� �߾�)
        float centerX = (targetGrid.data.width - 1) / 2f;
        float centerY = (targetGrid.data.height - 1) / 2f;

        // ��� ũ��� �׸��� ũ�� �״��
        transform.localScale = new Vector3(targetGrid.data.width, targetGrid.data.height, 1);

        // ��� ��ġ�� ���� �߾Ӱ�(�Ǽ� ����)���� ��ġ
        transform.localPosition = new Vector3(centerX, centerY, 1);

        if (autoCenterCamera)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // ī�޶� ���� �߾��� �ٶ�
                Vector3 worldCenter = transform.parent.TransformPoint(new Vector3(centerX, centerY, cameraDepth));
                mainCam.transform.position = new Vector3(worldCenter.x, worldCenter.y, cameraDepth);
                //--- 2026-06-24 긴 변 기준으로 줌 고정 → 회전 전후 줌 안 변함(확대 X), 위치만 판 중심 따라감
                // 기존: height만 기준 → 회전 시 height(세로) 줄면 카메라가 당겨져 확대돼버림
                // mainCam.orthographicSize = (targetGrid.data.height / 2f) + padding;
                float maxDim = Mathf.Max(targetGrid.data.width, targetGrid.data.height);
                mainCam.orthographicSize = maxDim / 2f + padding;
            }
        }
    }
}