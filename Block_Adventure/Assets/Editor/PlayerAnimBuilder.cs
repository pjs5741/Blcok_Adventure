using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

//--- 2026-07-10 플레이어 스프라이트(PixelLab 산출물) 자동 세팅.
// (1) 스트립 PNG 임포트 설정(픽셀아트) + 64x64 슬라이스 + AnimationClip + AnimatorController 생성
// (2) 열려있는 씬의 Player에 컨트롤러/스프라이트 적용(기존 월드 크기 유지하도록 스케일 보정)
// 트리거/상태명은 코드와 일치: basicAttack/heavyAttack/hit/knockBack/jump, Player_BasicAttack/Player_HeavyAttack
public static class PlayerAnimBuilder
{
    const string Dir = "Assets/Art/Player";
    const string ControllerPath = Dir + "/PlayerController.controller";
    const int Cell = 64;
    const float PPU = 16f;   // 64px → 4유닛 (씬 적용 단계에서 기존 크기에 맞게 스케일 보정)

    struct Def
    {
        public string file, state, trigger;
        public int fps;
        public bool loop;
        public Def(string file, string state, string trigger, int fps, bool loop)
        { this.file = file; this.state = state; this.trigger = trigger; this.fps = fps; this.loop = loop; }
    }

    static readonly Def[] Defs =
    {
        new Def("player_idle",   "Player_Idle",        null,          8,  true),
        new Def("player_attack", "Player_BasicAttack", "basicAttack", 12, false),
        new Def("player_heavy",  "Player_HeavyAttack", "heavyAttack", 12, false),
        new Def("player_hit",    "Player_Hit",         "hit",         12, false),
        new Def("player_jump",   "Player_Jump",        "jump",        12, false),
    };

    [MenuItem("Build/Player Anim (1) 클립+컨트롤러 생성")]
    public static void Build()
    {
        foreach (var d in Defs) ImportSliced($"{Dir}/{d.file}.png");
        AssetDatabase.Refresh();

        // 기존 컨트롤러 있으면 갈아엎기 (재실행 안전)
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("basicAttack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("heavyAttack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("knockBack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("jump", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();

        foreach (var d in Defs)
        {
            var sprites = LoadSprites($"{Dir}/{d.file}.png");
            if (sprites.Length == 0) { Debug.LogError($"스프라이트 없음: {d.file}"); continue; }

            var clipPath = $"{Dir}/{d.file}.anim";
            AssetDatabase.DeleteAsset(clipPath);
            var clip = MakeClip(sprites, d.fps, d.loop);
            AssetDatabase.CreateAsset(clip, clipPath);

            var state = sm.AddState(d.state);
            state.motion = clip;
            states[d.state] = state;
        }

        sm.defaultState = states["Player_Idle"];

        // 트리거 → AnyState 전이 (즉시), 끝나면 Idle 복귀
        foreach (var d in Defs)
        {
            if (d.trigger == null) continue;
            AddAnyTransition(sm, states[d.state], d.trigger);
            var back = states[d.state].AddTransition(states["Player_Idle"]);
            back.hasExitTime = true; back.exitTime = 1f; back.duration = 0f;
        }
        AddAnyTransition(sm, states["Player_Hit"], "knockBack");   // 넉백 클립 없음 → 피격 재사용

        AssetDatabase.SaveAssets();
        Debug.Log("✅ Player 클립+컨트롤러 생성 완료 → 이어서 Build/Player Anim (2) 실행 (GameScene 열고)");
    }

    [MenuItem("Build/Player Anim (2) 씬 Player에 적용")]
    public static void Apply()
    {
        var player = Object.FindFirstObjectByType<Player>();
        if (player == null) { Debug.LogError("씬에 Player 없음 — GameScene을 열고 실행"); return; }

        var sr = player.GetComponent<SpriteRenderer>();
        var anim = player.GetComponent<Animator>();
        if (sr == null || anim == null) { Debug.LogError("Player에 SpriteRenderer/Animator 필요"); return; }

        float oldWorldH = sr.sprite != null ? sr.bounds.size.y : 0f;   // 기존 월드 높이(스케일 포함)

        var idle = LoadSprites($"{Dir}/player_idle.png");
        if (idle.Length == 0) { Debug.LogError("player_idle 스프라이트 없음 — (1) 먼저 실행"); return; }
        sr.sprite = idle[0];
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

        // 기존 캐릭터 월드 크기에 맞게 스케일 보정 (배치/간격 로직 안 깨지게)
        if (oldWorldH > 0.01f)
        {
            float newWorldH = sr.bounds.size.y;
            if (newWorldH > 0.01f)
            {
                float f = oldWorldH / newWorldH;
                player.transform.localScale = player.transform.localScale * f;
                Debug.Log($"스케일 보정 ×{f:F2} (기존 높이 {oldWorldH:F1} 유지)");
            }
        }

        EditorUtility.SetDirty(player.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log("✅ Player 적용 완료 — 씬 저장 필요");
    }

    // ---------- 헬퍼 ----------
    static void ImportSliced(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError($"임포터 없음: {path}"); return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PPU;
        importer.filterMode = FilterMode.Point;              // 픽셀아트 필수
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        // 64x64 격자 슬라이스 (Unity 6: ISpriteEditorDataProvider)
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int count = Mathf.Max(1, (tex != null ? tex.width : Cell) / Cell);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
        var rects = new List<SpriteRect>();
        for (int i = 0; i < count; i++)
            rects.Add(new SpriteRect
            {
                name = $"{baseName}_{i}",
                rect = new Rect(i * Cell, 0, Cell, Cell),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate(),
            });
        provider.SetSpriteRects(rects.ToArray());

        var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameIds != null)
            nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToArray());

        provider.Apply();
        importer.SaveAndReimport();
    }

    static Sprite[] LoadSprites(string path)
        => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

    static AnimationClip MakeClip(Sprite[] sprites, int fps, bool loop)
    {
        var clip = new AnimationClip { frameRate = fps };
        var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
        var keys = sprites.Select((s, i) => new ObjectReferenceKeyframe { time = i / (float)fps, value = s }).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        if (loop)
        {
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, st);
        }
        return clip;
    }

    static void AddAnyTransition(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        var t = sm.AddAnyStateTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.hasExitTime = false;
        t.duration = 0f;
        t.canTransitionToSelf = false;
    }
}
