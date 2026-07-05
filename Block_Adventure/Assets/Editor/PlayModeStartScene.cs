#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartScene
{
    static PlayModeStartScene()
    {
        //--- 2026-07-03 협동 테스트 편의: 에디터 Play도 타이틀부터(협동/이어하기 진입 가능). 단일 개발 시 불편하면 MapScene으로 되돌리면 됨.
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/TitleScene.unity");
        if (scene != null)
            EditorSceneManager.playModeStartScene = scene;
    }
}
#endif
