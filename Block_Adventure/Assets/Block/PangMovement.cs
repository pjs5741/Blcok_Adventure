using UnityEngine;

public class PangMovement : MonoBehaviour
{
	[Header("References")]
	public BlockGrid myGrid;

	[Header("Settings")]
	public float swipeThreshold = 0.5f; // 스와이프 민감도

	// 내부 변수
	private int startX, startY;
	private bool isDragging = false;
	private Vector2 startScreenPos;

	void Update()
	{
		// 1. 클릭 (터치 시작)
		if (Input.GetMouseButtonDown(0))
		{
			Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			startX = Mathf.RoundToInt(worldPos.x);
			startY = Mathf.RoundToInt(worldPos.y);

			// 맵 안쪽을 찍었을 때만 드래그 시작
			if (myGrid.IsValidIndex(startX, startY))
			{
				isDragging = true;
				startScreenPos = Input.mousePosition;
			}
		}

		// 2. 뗌 (스와이프 방향 계산)
		if (Input.GetMouseButtonUp(0) && isDragging)
		{
			isDragging = false;
			Vector2 endScreenPos = Input.mousePosition;
			Vector2 direction = endScreenPos - startScreenPos;

			// 너무 살짝 움직인 건 무시
			if (direction.magnitude < swipeThreshold) return;

			// 가로 vs 세로 판정
			if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
			{
				// ↔ 좌우
				if (direction.x > 0) AttemptSwap(1, 0); // 우
				else AttemptSwap(-1, 0); // 좌
			}
			else
			{
				// ↕ 상하
				if (direction.y > 0) AttemptSwap(0, 1); // 상
				else AttemptSwap(0, -1); // 하
			}
		}
	}

	void AttemptSwap(int dx, int dy)
	{
		int targetX = startX + dx;
		int targetY = startY + dy;

		// 목표 지점도 맵 안이어야 스왑 실행
		if (myGrid.IsValidIndex(targetX, targetY))
		{
			myGrid.SwapBlocks(startX, startY, targetX, targetY);
		}
	}
}