#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartScene
{
    static PlayModeStartScene()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MapScene.unity");
        if (scene != null)
            EditorSceneManager.playModeStartScene = scene;
    }
}
#endif
