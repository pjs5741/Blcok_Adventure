#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

//--- 2026-07-03 협동 테스트용 Mac 스탠드얼론 빌드(설치 불필요, 맥 빌드 기본 포함). 창모드로 떠서 에디터와 동시 실행 가능.
// 에디터=1번째 클라, 이 .app=2번째 클라, 서버는 ./gradlew bootRun. WebGL/브라우저 없이 2인 협동 테스트.
public static class StandaloneBuilder
{
    [MenuItem("Build/Standalone (Mac) — 협동 테스트용")]
    public static void Build()
    {
        // 창모드로(전체화면 X) → 에디터와 나란히 보기
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.runInBackground = true;   // 창 포커스 없어도 계속 동작(2창 테스트 필수)

        var scenes = new[]
        {
            "Assets/Scenes/TitleScene.unity",
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/MapScene.unity",
            "Assets/Scenes/ShopScene.unity",
            "Assets/Scenes/RestScene.unity",
            "Assets/Scenes/EndScene.unity",
        };

        string outputPath = "Build/Mac/BlockAdventureCoop.app";

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        };

        Debug.Log($"Mac 스탠드얼론 빌드 시작 → {outputPath}");
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"빌드 완료: {report.summary.result} ({report.summary.totalTime}) — Build/Mac 폴더의 .app 더블클릭해 실행");
    }
}
#endif
