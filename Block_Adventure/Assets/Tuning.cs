//--- 2026-06-30 연출/레이아웃 튜닝 값 단일 소스. 흩어진 매직넘버를 한 곳에 모음.
// 인스펙터 직렬화 필드는 이 값을 기본값으로 쓰되(= Tuning.X), 개별 오버라이드 가능.
public static class Tuning
{
    // 연출 타이밍(초)
    public const float PivotDuration = 0.6f;          // 판 90도 회전
    public const float DodgeDuration = 0.6f;          // 플레이어 회피 점프
    public const float MonsterTelegraph = 0.55f;      // 몬스터 공격 예고 멈춤
    public const float RewardRotateInterval = 1.0f;   // 보상 카드 90도 회전 간격
    public const float RewardArtYOffset = 1.7f;        //--- 2026-07-01 보상 카드 블록 아트 Y 위치(하단 설명칸 침범 방지)
    public const float DotTickPause = 0.35f;          // 도트(독/화상) 틱 사이 멈춤
    public const float AnimWaitGuard = 1.0f;          // 애니 상태 진입 대기 안전 가드(무한대기 방지)
    public const float AttackMaxWait = 2.0f;          // 공격 모션 대기 상한(무한대기 방지)

    // 밸런스
    public const int DotStacksPerBlock = 3; // 독/화상 블록 1개당 부여 스택 (틱 데미지=스택 그대로 → 직관적)
    public const int ShieldPerDelay = 10;   //--- 2026-07-03 방패 N개당 몬스터 공격을 1턴 "지연"(스킵 아님)
    public const int IntentCooldown = 3;    //--- 2026-07-03 특수 인텐트(시야가림/판회전 등) 재등장 쿨다운(픽 횟수)
    //--- 2026-07-01 흩어진 매직넘버 집결
    public const float HitKnockbackRatio = 0.12f;       // 피격 데미지가 최대체력 이 비율 이상이면 넉백, 미만이면 hit
    public const float GlassHeartFillThreshold = 0.6f;  // 유리 심장 유물 발동 그리드 충전율

    // 연출
    public const float HpBarLerpSpeed = 8f;             //--- 2026-07-01 몬스터 체력바 보간 속도(클수록 빠름)
    //--- 2026-07-03 피격 데미지 숫자 팝업(sin 아치로 떠올랐다 내려옴)
    public const float DamagePopupLife = 0.9f;          // 지속(초)
    public const float DamagePopupRise = 3f;            // 최고 상승 높이(월드 단위)
    public const float DamagePopupSize = 1f;            // TextMesh characterSize
    public const float DamagePopupYOffset = 4f;         // 몬스터 기준 시작 높이
    public const float PoisonPulseRate = 6f;            // 독 상태 색 펄스 속도
    public const float PoisonColorIntensity = 0.7f;     // 독 색 보간 최대 강도(0~1)
    public const float BlockDestroyShakeStrength = 0.2f;// 블록 파괴 시 카메라 흔들림 강도
    public const float BlockDestroyShakeDuration = 0.3f;// 블록 파괴 시 카메라 흔들림 지속(초)

    // 레이아웃
    //--- 2026-07-01 PreviewY는 방향 인식 배치로 대체됨(미사용). 남겨둠(참조 안전용).
    public const float PreviewY = 18f;                // (구) 미리보기/홀드 세로 위치
    public const float ActorMinGap = 6f;              // 플레이어/몬스터 보드 모서리와의 최소 간격
    public const float PreviewMiniScale = 0.7f;       // 미니 블록 스케일
    public const float PreviewFrameSize = 3.4f;       // 미니 액자 한 변
    public const float PreviewFrameGap = 0.6f;        // 액자 사이/보드 여백
    public const float PreviewBoardMargin = 2.2f;     // 미리보기·홀드와 보드 모서리 간격

    //--- 2026-07-01 상태 뱃지(플레이어 버프 / 몬스터 디버프) 공용 크기·간격 — 버프/디버프 시각 통일
    public const float BadgeSize = 48f;               // 뱃지 한 변(px)
    public const float BadgeSpacing = 54f;            // 뱃지 간 가로 간격(px)
}
