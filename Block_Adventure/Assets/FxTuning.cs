using UnityEngine;

//--- 2026-07-09 연출(느낌) 튜닝 단일 소스 — ScriptableObject 에셋(Resources/FxTuning.asset).
// 에디터에서 값·커브를 마우스로 조정 + 플레이 모드 중 실시간 튜닝(에셋이라 종료 후에도 유지).
// Tuning.cs(const)는 게임 밸런스 숫자 전용, 여기는 연출 전용. 씬 직렬화 덮어쓰기 문제 없음(에셋 하나가 단일 소스).
[CreateAssetMenu(fileName = "FxTuning", menuName = "BlockAdventure/FxTuning")]
public class FxTuning : ScriptableObject
{
    static FxTuning _i;
    public static FxTuning I
    {
        get
        {
            if (_i == null) _i = Resources.Load<FxTuning>("FxTuning");
            if (_i == null)
            {
                _i = CreateInstance<FxTuning>();   // 에셋 없어도 코드 기본값으로 동작 (경고만)
                Debug.LogWarning("FxTuning.asset 없음 — 코드 기본값 사용 (Build/Create FxTuning Asset 메뉴로 생성)");
            }
            return _i;
        }
    }

    [Header("피벗(판 90도) — ① 예고 삐걱")]
    public float pivotShakeDuration = 0.3f;    // 넘어지기 전 삐걱 시간
    public float pivotShakeAngle = 2.2f;       // 삐걱 최대 기울기(도)
    public float pivotShakeFreq = 28f;         // 삐걱 진동 속도

    [Header("피벗 — ② 회전(넘어짐/일어서기)")]
    public float pivotDuration = 0.6f;         // 90도 회전 시간
    [Tooltip("넘어짐 진행 커브(가로=시간 0~1, 세로=회전 진행 0~1). 뒤로 갈수록 가파르게 = 가속 무게감")]
    public AnimationCurve pivotFallCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.4f, 0f));
    [Tooltip("원복(일어서기) 커브. 처음 빠르게 → 천천히 안착")]
    public AnimationCurve pivotRiseCurve = new AnimationCurve(new Keyframe(0f, 0f, 2.4f, 2.4f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("피벗 — ③ 착지 쿵")]
    public float pivotLandShakeDuration = 0.35f;   // 카메라 셰이크 시간
    public float pivotLandShakeStrength = 0.45f;   // 카메라 셰이크 강도
    public float pivotBounceDuration = 0.22f;      // 판 스쿼시(눌림) 복원 시간
    [Range(0.7f, 1f)] public float pivotBounceSquash = 0.94f;   // 착지 순간 판 세로 눌림 비율

    [Header("몬스터 점프 가격 (피벗 전용 연출)")]
    public float lungeDuration = 0.45f;                       // 점프 편도 시간
    public float lungeArc = 3.5f;                             // 점프 포물선 추가 높이
    //--- 2026-07-10 몸이 판에 깊게 파고들던 문제 → 모서리 바깥에서 때리는 느낌으로 오프셋 확대
    public Vector2 lungeTargetOffset = new Vector2(6f, 2f);   // 그리드 우상단 모서리 기준 타격 지점 오프셋(몬스터 중심)

    [Header("플레이어 회피 점프 (피벗 때)")]
    public Vector2 dodgeOffset = new Vector2(-2.5f, 0f);   // 뒤로 점프 착지 변위
    public float dodgeHeight = 1.5f;                       // 포물선 정점 높이
    public float dodgeDuration = 0.6f;                     // 점프 시간(피벗 회전과 맞춤)

    [Header("페이드 (확 사라짐 → 보간)")]
    public float blindFadeDuration = 0.45f;    // 은폐 적용/해제 페이드
    public float freezeFadeDuration = 0.45f;   // 얼음 틴트 페이드
    public Color freezeTint = new Color(0.55f, 0.8f, 1f);   // 얼음 색 (원래 색과 혼합)
    [Range(0f, 1f)] public float freezeTintStrength = 0.65f; // 얼음 색 혼합 비율

    [Header("시한폭탄")]
    public Color bombColor = new Color(0.55f, 0.1f, 0.12f);    // 폭탄 블록 색
    public Color bombWarnColor = new Color(0.95f, 0.2f, 0.15f); // 1턴 남았을 때 색
    public float bombExplodeShakeDuration = 0.3f;
    public float bombExplodeShakeStrength = 0.4f;

    [Header("광폭화")]
    public float enrageScale = 1.12f;                        // 몬스터 크기 배율
    public float enrageDuration = 0.5f;                      // 커지는/붉어지는 시간
    public Color enrageTint = new Color(1f, 0.45f, 0.4f);    // 광폭화 색 (기본색과 혼합)
    [Range(0f, 1f)] public float enrageTintStrength = 0.55f;
    public float enrageShakeDuration = 0.4f;
    public float enrageShakeStrength = 0.35f;
}
