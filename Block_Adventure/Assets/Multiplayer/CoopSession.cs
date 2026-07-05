using UnityEngine;

//--- 2026-07-01 협동 전투 세션 상태(전역). 매칭 성공 시 Active=true로 GameScene 진입, 전투 로직이 이 플래그로 분기.
public static class CoopSession
{
    public static bool Active;          // 협동 모드 여부
    public static int PlayerId = -1;    // 0 / 1
    public static int Seed;             // 공유 맵 시드(방장 시작 시 서버가 전달)
    public static int MonsterHp;
    public static int MonsterMaxHp;
    public static int MonsterSeed;      //--- 2026-07-03 현재 전투 몬스터 프로필 시드(양쪽 동일 스폰)
    public static int AttackCountdown;  //--- 2026-07-03 다음 공격까지 남은 턴(서버 동기)
    public static bool TutorialShown;   //--- 2026-07-03 협동 런 1회 튜토리얼 표시 여부

    public static void Reset()
    {
        Active = false;
        PlayerId = -1;
        Seed = 0;
        MonsterHp = MonsterMaxHp = 0;
        TutorialShown = false;
    }

    // WebGL은 접속한 페이지와 같은 오리진(같은 스프링 서버)으로, 그 외는 로컬 서버로 연결.
    public static string GetUrl()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string abs = Application.absoluteURL;
        if (!string.IsNullOrEmpty(abs))
        {
            var uri = new System.Uri(abs);
            string scheme = uri.Scheme == "https" ? "wss" : "ws";
            int port = uri.Port > 0 ? uri.Port : (scheme == "wss" ? 443 : 80);
            return $"{scheme}://{uri.Host}:{port}/ws/coop";
        }
        return "ws://localhost:8888/ws/coop";
#else
        return "ws://localhost:8888/ws/coop";
#endif
    }
}
