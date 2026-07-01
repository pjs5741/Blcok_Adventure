using UnityEngine;

//--- 2026-07-01 협동 전투 세션 상태(전역). 매칭 성공 시 Active=true로 GameScene 진입, 전투 로직이 이 플래그로 분기.
public static class CoopSession
{
    public static bool Active;          // 협동 모드 여부
    public static int PlayerId = -1;    // 0 / 1
    public static int MonsterHp;
    public static int MonsterMaxHp;

    public static void Reset()
    {
        Active = false;
        PlayerId = -1;
        MonsterHp = MonsterMaxHp = 0;
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
