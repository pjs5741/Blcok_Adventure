using UnityEngine;

//--- 2026-07-09 애니메이터 헬퍼. 아직 클립/트리거를 안 넣은 상태에서도 안전하게 동작하도록
// "트리거가 실제로 있으면 그걸, 없으면 폴백 트리거를" 쓰는 공용 로직 (강공격/특수시전 클립은 리소스 작업 때 추가).
public static class AnimHelper
{
    // 애니메이터에 해당 트리거 파라미터가 존재하는지
    public static bool HasTrigger(Animator anim, string name)
    {
        if (anim == null) return false;
        foreach (var p in anim.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }

    // preferred 트리거가 있으면 그걸 쏘고 true, 없으면 fallback 쏘고 false 반환 (호출부가 대기 상태명 선택용)
    public static bool TriggerOrFallback(Animator anim, string preferred, string fallback)
    {
        if (anim == null) return false;
        if (HasTrigger(anim, preferred)) { anim.SetTrigger(preferred); return true; }
        anim.SetTrigger(fallback);
        return false;
    }
}
