using UnityEditor;
using UnityEngine;

//--- 2026-07-09 FxTuning.asset 생성 유틸 (없을 때 1회). Resources/에 만들어 코드에서 Resources.Load로 참조.
public static class FxTuningCreator
{
    const string Path = "Assets/Resources/FxTuning.asset";

    //--- 2026-07-10 컨텍스트 튜토리얼(행동별 1회) 다시 보기 — 본 기록 초기화
    [MenuItem("Build/Reset Context Tutorials")]
    public static void ResetTutorials()
    {
        ContextTutorial.ResetAll();
        Debug.Log("✅ 컨텍스트 튜토리얼 기록 초기화 — 다음 플레이에서 다시 뜸");
    }

    [MenuItem("Build/Create FxTuning Asset")]
    public static void Create()
    {
        var existing = AssetDatabase.LoadAssetAtPath<FxTuning>(Path);
        if (existing != null)
        {
            Debug.Log("FxTuning.asset 이미 있음 — 생성 생략");
            Selection.activeObject = existing;
            return;
        }
        var so = ScriptableObject.CreateInstance<FxTuning>();
        AssetDatabase.CreateAsset(so, Path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = so;
        Debug.Log("✅ FxTuning.asset 생성 완료 — 인스펙터에서 연출 값/커브 조정 가능");
    }
}
