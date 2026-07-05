#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class WebGLBuilder
{
    [MenuItem("Build/WebGL Build")]
    public static void Build()
    {
        // 압축 끔 → 정적 서버 어디서든 그대로 동작
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

        //--- 2026-07-02 TitleScene을 첫 씬(진입점)으로 포함(협동/이어하기 메뉴). 순서 = 빌드 인덱스.
        var scenes = new[]
        {
            "Assets/Scenes/TitleScene.unity",
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/MapScene.unity",
            "Assets/Scenes/ShopScene.unity",
            "Assets/Scenes/RestScene.unity",
            "Assets/Scenes/EndScene.unity",
        };

        //--- 2026-07-02 스프링 static에 바로 빌드 → 복사 불필요. (프로젝트 루트 기준 상대경로)
        string outputPath = "../block-adventure-web/src/main/resources/static";

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        Debug.Log($"WebGL 빌드 시작 → {outputPath} (압축 Disabled)");
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"WebGL 빌드 완료: {report.summary.result} ({report.summary.totalTime})");
    }
}
#endif
