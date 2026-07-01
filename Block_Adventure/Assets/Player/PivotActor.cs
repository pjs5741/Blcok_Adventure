using System.Collections;
using UnityEngine;

//--- 2026-06-29 피벗(판 뒤집기)용 액터 헬퍼. 그리드 옆 Player/Monster에 부착.
// transform.position = _basePos + extraOffset 로만 위치 합성.
//  - _basePos: 의도된 월드 위치. 평소 홈(authored) 고정. 피벗 때 MoveToGap으로 부드럽게 이동(회전과 동시).
//  - extraOffset: 점프 등 추가 변위(Player가 구동, Monster는 0).
// 위치 모델: 평소엔 홈 위치 유지하고, 보드 모서리가 minGap보다 가까워질 때만 모서리 밖으로 밀어냄.
//  → 왼쪽 모서리는 항상 x=0이라 왼쪽(플레이어)은 안 움직이고, 가로판서 오른쪽(몬스터)만 살짝 비켜남.
public class PivotActor : MonoBehaviour
{
    public Vector3 extraOffset;
    public float minGap = Tuning.ActorMinGap;   // 보드 모서리와의 최소 간격. 이보다 가까워지면 밀려남

    private Vector3 _basePos;
    private bool _init;
    private Coroutine _co;

    // 홈(authored) 정보 — Start에서 1회 캡처
    private bool _captured;
    private bool _isLeft;       // 보드 중심 기준 좌/우
    private float _homeX;
    private float _homeY;

    void Start()
    {
        _basePos = transform.position;
        _init = true;
        Capture();
    }

    void Capture()
    {
        if (_captured) return;
        var b = Board();
        if (b == null) return;
        float centerX = (b.width - 1) / 2f;
        _isLeft = transform.position.x < centerX;
        _homeX = transform.position.x;
        _homeY = transform.position.y;
        _captured = true;
    }

    GridData Board()
    {
        return (GameManager.Instance != null && GameManager.Instance.blockGrid != null)
            ? GameManager.Instance.blockGrid.data : null;
    }

    void LateUpdate()
    {
        if (!_init) { _basePos = transform.position; _init = true; }
        transform.position = _basePos + extraOffset;
    }

    // 회전 후 보드 폭(boardWidth) 기준으로 목표 위치까지 부드럽게 이동 (회전과 동시 호출)
    public void MoveToGap(float duration, int boardWidth)
    {
        if (!_captured) Capture();
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(MoveRoutine(duration, boardWidth));
    }

    IEnumerator MoveRoutine(float duration, int boardWidth)
    {
        Vector3 start = _basePos;
        Vector3 target = GapTarget(boardWidth);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.01f);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            _basePos = Vector3.Lerp(start, target, e);
            yield return null;
        }
        _basePos = target;
        _co = null;
    }

    Vector3 GapTarget(int boardWidth)
    {
        if (!_captured) return _basePos;
        // 홈 유지하되, 보드 모서리가 minGap보다 가까우면 모서리 밖으로 밀어냄
        float x;
        if (_isLeft)
        {
            float leftEdge = 0f;
            x = Mathf.Min(_homeX, leftEdge - minGap);
        }
        else
        {
            float rightEdge = boardWidth - 1;
            x = Mathf.Max(_homeX, rightEdge + minGap);
        }
        return new Vector3(x, _homeY, _basePos.z);
    }
}
