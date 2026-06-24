using UnityEngine;

public class BlockMovement : MonoBehaviour
{
    public BlockGrid myGrid;
    public BlockSpawner mySpawner;

    [Header("Settings Data")]
    public BlockData blockData;

    private bool isLocked = false;
    private bool isInitialized = false;
    private float _moveTimer;
    private float _moveDelay = 0.1f;
    private float _moveInitDelay = 0.2f;

    // ★ Start()를 지우고 이 함수를 만듦
    // 스포너가 모든 세팅을 끝낸 뒤에 이 함수를 호출할 것임
    public void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;

        // 1. 투명도 설정
        float startAlpha = (blockData != null) ? blockData.movingAlpha : 0.5f;
        SetTransparency(startAlpha);

        // 2. 바닥 착지 (이제 myGrid가 확실히 있음)
        SnapToFloor();

        // 3. 아이콘은 처음부터 똑바로
        KeepChildrenUpright();
    }

    void Update()
    {
        // 초기화 안 됐거나, 굳었거나, 그리드 없으면 작동 X
        if (!isInitialized || isLocked || myGrid == null) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveHorizontal(Vector3.left); _moveTimer = _moveInitDelay; }
        else if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveHorizontal(Vector3.right); _moveTimer = _moveInitDelay; }

        if (Input.GetKey(KeyCode.LeftArrow)) { _moveTimer -= Time.deltaTime; if (_moveTimer <= 0) { MoveHorizontal(Vector3.left); _moveTimer = _moveDelay; } }
        else if (Input.GetKey(KeyCode.RightArrow)) { _moveTimer -= Time.deltaTime; if (_moveTimer <= 0) { MoveHorizontal(Vector3.right); _moveTimer = _moveDelay; } }

        if (Input.GetKeyDown(KeyCode.A)) RotateBlock(90);
        else if (Input.GetKeyDown(KeyCode.S)) RotateBlock(-90);
        else if (Input.GetKeyDown(KeyCode.Space)) LockBlock();
        else if (Input.GetKeyDown(KeyCode.C)) { mySpawner.HoldCurrent(); return; }  // 홀드 유물 (없으면 내부에서 무시)
    }

    // --- (이 아래는 기존 함수들 그대로 유지) ---

    void MoveHorizontal(Vector3 direction)
    {
        transform.position += direction;
        if (!IsInsideWalls())
        {
            transform.position -= direction;
            return;
        }
        SnapToFloor();
    }

    void RotateBlock(int angle)
    {
        if (blockData != null && !blockData.allowRotation) return;

        transform.Rotate(0, 0, angle);
        if (!ResolveWallCollision())
        {
            transform.Rotate(0, 0, -angle);
            return;
        }
        SnapToFloor();
        if (!myGrid.IsValidPosition(transform))
        {
            transform.Rotate(0, 0, -angle);
            SnapToFloor();
        }
        KeepChildrenUpright();
    }

    // 부모 회전과 무관하게 각 셀(아이콘)은 똑바로
    void KeepChildrenUpright()
    {
        foreach (Transform child in transform)
            child.rotation = Quaternion.identity;
    }

    void LockBlock()
    {
        if (isLocked) return;
        isLocked = true;

        float endAlpha = (blockData != null) ? blockData.lockedAlpha : 1.0f;
        SetTransparency(endAlpha);

        myGrid.AddToGrid(transform);
        GameManager.Instance?.OnPlayerInputDone();

        transform.SetParent(myGrid.transform);
        this.enabled = false;
    }

    void SnapToFloor()
    {
        // ★ 에러나던 곳: 이제 안전함
        if (myGrid == null) return;

        Vector3 currentPos = transform.position;
        // 일단 천장으로
        transform.position = new Vector3(currentPos.x, myGrid.data.height, 0);

        // 닿을 때까지 내림
        while (myGrid.IsValidPosition(transform))
        {
            transform.position += Vector3.down;
        }
        // 한 칸 위로
        transform.position += Vector3.up;
    }

    void SetTransparency(float alpha)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            Color color = sr.color;
            color.a = alpha;
            sr.color = color;
        }
    }

    bool ResolveWallCollision()
    {
        if (IsInsideWalls()) return true;
        if (CheckOutSideLeft()) { if (TryMove(Vector3.right)) return true; if (TryMove(Vector3.right * 2)) return true; }
        else if (CheckOutSideRight()) { if (TryMove(Vector3.left)) return true; if (TryMove(Vector3.left * 2)) return true; }
        return false;
    }

    bool TryMove(Vector3 offset) { transform.position += offset; if (IsInsideWalls()) return true; transform.position -= offset; return false; }
    bool CheckOutSideLeft() { foreach (Transform child in transform) if (Mathf.RoundToInt(child.position.x) < 0) return true; return false; }
    bool CheckOutSideRight() { foreach (Transform child in transform) if (Mathf.RoundToInt(child.position.x) >= myGrid.data.width) return true; return false; }
    bool IsInsideWalls() { foreach (Transform child in transform) { int x = Mathf.RoundToInt(child.position.x); if (x < 0 || x >= myGrid.data.width) return false; } return true; }
}