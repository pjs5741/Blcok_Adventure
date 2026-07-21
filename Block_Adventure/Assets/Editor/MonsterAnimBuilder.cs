using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

//--- 2026-07-14 몬스터 스프라이트 자동 세팅 (PlayerAnimBuilder와 동일 파이프라인).
// 스트립: Assets/Art/<몬스터>/ · 클립+컨트롤러: Assets/Resources/MonsterAnim/<몬스터>/ (런타임 Resources.Load용)
// 상태명은 Monster.cs와 일치 필수: Basic_Attack / Cast_Attack / Death. 트리거: basicAttack/castAttack/hit/knockBack/death
public static class MonsterAnimBuilder
{
    const int Cell = 64;
    const float PPU = 16f;

    struct Def
    {
        public string file, state, trigger; public int fps; public bool loop;
        public Def(string f, string s, string t, int fp, bool l) { file = f; state = s; trigger = t; fps = fp; loop = l; }
    }

    [MenuItem("Build/Monster Anim — Slime 생성")]
    public static void BuildSlime()
    {
        Build("Slime", new[]
        {
            new Def("slime_idle",   "Idle",         null,          8,  true),
            new Def("slime_attack", "Basic_Attack", "basicAttack", 12, false),
            new Def("slime_devour", "Cast_Attack",  "castAttack",  12, false),
            new Def("slime_hit",    "Hit",          "hit",         12, false),
            new Def("slime_death",  "Death",        "death",       10, false),
        });
    }

    //--- 2026-07-15 골렘
    [MenuItem("Build/Monster Anim — Golem 생성")]
    public static void BuildGolem()
    {
        Build("Golem", new[]
        {
            new Def("golem_idle",   "Idle",         null,          8,  true),
            new Def("golem_attack", "Basic_Attack", "basicAttack", 12, false),
            new Def("golem_cast",   "Cast_Attack",  "castAttack",  12, false),
            new Def("golem_hit",    "Hit",          "hit",         12, false),
            new Def("golem_death",  "Death",        "death",       10, false),
        });
    }

    //--- 2026-07-16 보스 (공격=boss_base_attack 스트립, 특수패턴(폭탄/디스펠)=cast, 판뒤집기=attack 재활용)
    [MenuItem("Build/Monster Anim — Boss 생성")]
    public static void BuildBoss()
    {
        Build("Boss", new[]
        {
            new Def("boss_idle",   "Idle",         null,          8,  true),
            new Def("boss_attack", "Basic_Attack", "basicAttack", 12, false),
            new Def("boss_cast",   "Cast_Attack",  "castAttack",  12, false),
            new Def("boss_hit",    "Hit",          "hit",         12, false),
            new Def("boss_death",  "Death",        "death",       10, false),
        });
    }

    //--- 2026-07-14 좀비
    [MenuItem("Build/Monster Anim — Zombie 생성")]
    public static void BuildZombie()
    {
        Build("Zombie", new[]
        {
            new Def("zombie_idle",   "Idle",         null,          8,  true),
            new Def("zombie_attack", "Basic_Attack", "basicAttack", 12, false),
            new Def("zombie_cast",   "Cast_Attack",  "castAttack",  12, false),
            new Def("zombie_hit",    "Hit",          "hit",         12, false),
            new Def("zombie_death",  "Death",        "death",       10, false),
        });
    }

    static void Build(string name, Def[] defs)
    {
        string artDir = $"Assets/Art/{name}";
        string resDir = $"Assets/Resources/MonsterAnim/{name}";
        if (!AssetDatabase.IsValidFolder("Assets/Resources/MonsterAnim"))
            AssetDatabase.CreateFolder("Assets/Resources", "MonsterAnim");
        if (!AssetDatabase.IsValidFolder(resDir))
            AssetDatabase.CreateFolder("Assets/Resources/MonsterAnim", name);

        foreach (var d in defs) ImportSliced($"{artDir}/{d.file}.png");
        AssetDatabase.Refresh();

        string ctrlPath = $"{resDir}/{name}Controller.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        foreach (var t in new[] { "basicAttack", "castAttack", "hit", "knockBack", "death" })
            controller.AddParameter(t, AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();
        foreach (var d in defs)
        {
            var sprites = LoadSprites($"{artDir}/{d.file}.png");
            if (sprites.Length == 0) { Debug.LogError($"스프라이트 없음: {d.file}"); continue; }
            string clipPath = $"{resDir}/{d.file}.anim";
            AssetDatabase.DeleteAsset(clipPath);
            var clip = MakeClip(sprites, d.fps, d.loop);
            AssetDatabase.CreateAsset(clip, clipPath);
            var st = sm.AddState(d.state);
            st.motion = clip;
            states[d.state] = st;
        }
        sm.defaultState = states["Idle"];

        foreach (var d in defs)
        {
            if (d.trigger == null) continue;
            AddAny(sm, states[d.state], d.trigger);
            if (d.state != "Death")   // 사망은 Idle 복귀 없음 (마지막 프레임 유지 후 코드가 비활성화)
            {
                var back = states[d.state].AddTransition(states["Idle"]);
                back.hasExitTime = true; back.exitTime = 1f; back.duration = 0f;
            }
        }
        AddAny(sm, states["Hit"], "knockBack");   // 넉백 클립 없음 → 피격 재사용

        AssetDatabase.SaveAssets();
        Debug.Log($"✅ {name} 애니 생성 완료 — Resources/MonsterAnim/{name}/{name}Controller");
    }

    static void ImportSliced(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError($"임포터 없음: {path}"); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PPU;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

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
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (ids != null) ids.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToArray());
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

    static void AddAny(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        var t = sm.AddAnyStateTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
    }
}
