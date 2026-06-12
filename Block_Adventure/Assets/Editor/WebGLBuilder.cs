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

        var scenes = new[]
        {
            "Assets/Scenes/MapScene.unity",
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/ShopScene.unity",
            "Assets/Scenes/RestScene.unity",
            "Assets/Scenes/EndScene.unity",
        };

        string outputPath = "Build/WebGL";

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
