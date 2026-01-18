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
            Debug.LogError("Grid를 연결해주세요!");
            return;
        }

        // ★ [만능 공식] (길이 - 1) / 2f
        // Width 10 -> (9 / 2) = 4.5 (정확한 중앙)
        // Width 11 -> (10 / 2) = 5.0 (정확한 중앙)
        float centerX = (targetGrid.width - 1) / 2f;
        float centerY = (targetGrid.height - 1) / 2f;

        // 배경 크기는 그리드 크기 그대로
        transform.localScale = new Vector3(targetGrid.width, targetGrid.height, 1);

        // 배경 위치는 계산된 중앙값(실수 포함)으로 배치
        transform.localPosition = new Vector3(centerX, centerY, 1);

        if (autoCenterCamera)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // 카메라도 계산된 중앙을 바라봄
                Vector3 worldCenter = transform.parent.TransformPoint(new Vector3(centerX, centerY, cameraDepth));
                mainCam.transform.position = new Vector3(worldCenter.x, worldCenter.y, cameraDepth);
                mainCam.orthographicSize = (targetGrid.height / 2f) + padding;
            }
        }
    }
}