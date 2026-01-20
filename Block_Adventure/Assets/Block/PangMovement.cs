/*using UnityEngine;

public class PangMovement : MonoBehaviour
{
	[Header("References")]
	public BlockGrid myGrid;

	[Header("Settings")]
	public float swipeThreshold = 0.5f; // �������� �ΰ���

	// ���� ����
	private int startX, startY;
	private bool isDragging = false;
	private Vector2 startScreenPos;

	void Update()
	{
		// 1. Ŭ�� (��ġ ����)
		if (Input.GetMouseButtonDown(0))
		{
			Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			startX = Mathf.RoundToInt(worldPos.x);
			startY = Mathf.RoundToInt(worldPos.y);

			// �� ������ ����� ���� �巡�� ����
			if (myGrid.IsValidIndex(startX, startY))
			{
				isDragging = true;
				startScreenPos = Input.mousePosition;
			}
		}

		// 2. �� (�������� ���� ���)
		if (Input.GetMouseButtonUp(0) && isDragging)
		{
			isDragging = false;
			Vector2 endScreenPos = Input.mousePosition;
			Vector2 direction = endScreenPos - startScreenPos;

			// �ʹ� ��¦ ������ �� ����
			if (direction.magnitude < swipeThreshold) return;

			// ���� vs ���� ����
			if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
			{
				// �� �¿�
				if (direction.x > 0) AttemptSwap(1, 0); // ��
				else AttemptSwap(-1, 0); // ��
			}
			else
			{
				// �� ����
				if (direction.y > 0) AttemptSwap(0, 1); // ��
				else AttemptSwap(0, -1); // ��
			}
		}
	}

	void AttemptSwap(int dx, int dy)
	{
		int targetX = startX + dx;
		int targetY = startY + dy;

		// ��ǥ ������ �� ���̾�� ���� ����
		if (myGrid.IsValidIndex(targetX, targetY))
		{
			myGrid.SwapBlocks(startX, startY, targetX, targetY);
		}
	}
}*/