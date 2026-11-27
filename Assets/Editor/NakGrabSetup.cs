using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// ตั้งท่าจับของแขนนาคให้ครบทีเดียว — เมนู Tools > Yantra > Setup Nak Grab
// ต้องอยู่ในโฟลเดอร์ชื่อ Editor / กดซ้ำได้ (ตั้งค่าเดิมทับ ไม่สร้างซ้ำ)
//
// 1. Animator ของแขน: ใส่คลิปให้ Showup / Flick / Idle / GoBack, เพิ่ม Attack → ChockLoop (ต่อลูกศร Exit Time)
// 2. Paired Animation Id 3 ตัว: Nak_Grab_Player / Nak_Grab_Loop / Nak_Grab_Escape
// 3. ทุก prefab ชื่อขึ้นต้น PNak ที่มี GrabQTE: ใส่ท่า, Ghost, Mash Input (ถ้าว่าง)
//    + EnemyController มี PairedAnimationActor + PairedAnimationPlayer + Body Animator
public static class NakGrabSetup
{
    private static readonly (string state, string clip)[] StateClips =
    {
        ("Showup", "Nak_ShowUp"),
        ("Flick", "Nak_Flick"),
        ("Idle", "Nak_Idle"),
        ("GoBack", "Nak_GoBack"),
        ("Attack", "Nak_Chock Player"),
        ("ChockLoop", "Nak_Chock Playe Loop"),
    };

    private static readonly StringBuilder Log = new StringBuilder();

    [MenuItem("Tools/Yantra/Setup Nak Grab (ตั้งท่าจับแขนนาค)")]
    private static void Run()
    {
        List<string> prefabs = FindNakPrefabs();
        if (prefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("Setup Nak Grab", "ไม่เจอ prefab ชื่อ PNak... ที่มี GrabQTE", "ตกลง");
            return;
        }

        string list = string.Join("\n", prefabs.ConvertAll(p => "• " + Path.GetFileNameWithoutExtension(p)));
        if (!EditorUtility.DisplayDialog("Setup Nak Grab",
                $"จะตั้งค่าท่าจับให้ prefab เหล่านี้ + Animator ของแขน:\n\n{list}\n\n(ถ้าเปิด prefab ค้างอยู่ จะปิดกลับหน้าฉากก่อน)",
                "ตั้งเลย", "ยกเลิก"))
            return;

        // แก้ prefab ทั้งไฟล์ — ปิด Prefab Mode ก่อน กันชนกับหน้าที่เปิดค้าง
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            StageUtility.GoToMainStage();

        Log.Clear();

        // 1) ท่า 3 ตัว
        string folder = AssetFolder();
        PairedAnimationId player = UpsertPaired(folder, "Nak_Grab_Player",
            "BaseMovement", "", "Idle_ArmClip", 0.2f, true, 0.2f);
        PairedAnimationId loop = UpsertPaired(folder, "Nak_Grab_Loop",
            "Attack", "", "", 0.15f, true, 0.35f);
        PairedAnimationId escape = UpsertPaired(folder, "Nak_Grab_Escape",
            "GoBack", "", "", 0.15f, false, 0f);

        Object mashInput = FindInteractInput();

        // 2) prefab — ตัวหลักก่อน ตัว Variant ทีหลัง (Variant จะได้ของจากตัวหลักไปก่อน)
        prefabs.Sort((a, b) => IsVariant(a).CompareTo(IsVariant(b)));
        var controllers = new HashSet<AnimatorController>();
        foreach (string path in prefabs)
            SetupPrefab(path, player, loop, escape, mashInput, controllers);

        // 3) Animator ของแขน
        foreach (AnimatorController controller in controllers)
            SetupController(controller);

        AssetDatabase.SaveAssets();

        string message = Log.ToString();
        Debug.Log("[NakGrabSetup]\n" + message);
        EditorUtility.DisplayDialog("Setup Nak Grab", message + "\nเสร็จแล้ว — กด Play แล้วกด G เทสได้", "ตกลง");
    }

    // ---------- Prefab ----------

    private static List<string> FindNakPrefabs()
    {
        var result = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab PNak"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(path).StartsWith("PNak")) continue;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && asset.GetComponentInChildren<GrabQTEInteractAddition>(true) != null)
                result.Add(path);
        }
        return result;
    }

    private static bool IsVariant(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return asset != null && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant;
    }

    private static void SetupPrefab(string path, PairedAnimationId player, PairedAnimationId loop,
                                    PairedAnimationId escape, Object mashInput, HashSet<AnimatorController> controllers)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GrabQTEInteractAddition qte = root.GetComponentInChildren<GrabQTEInteractAddition>(true);
            GameObject owner = FindOwner(root);

            PairedAnimationActor actor = root.GetComponentInChildren<PairedAnimationActor>(true);
            bool addedActor = false, addedPlayer = false;
            if (actor == null)
            {
                actor = owner.AddComponent<PairedAnimationActor>();
                addedActor = true;
            }
            if (actor.GetComponent<PairedAnimationPlayer>() == null)
            {
                actor.gameObject.AddComponent<PairedAnimationPlayer>();
                addedPlayer = true;
            }

            // Animator ของแขน (Nak_Arm) — ใส่ Body Animator ถ้าตัว actor เองไม่มี Animator
            Animator arm = FindArmAnimator(root);
            var actorSo = new SerializedObject(actor);
            SerializedProperty bodyProp = actorSo.FindProperty("_bodyAnimator");
            if (bodyProp != null && bodyProp.objectReferenceValue == null && actor.GetComponent<Animator>() == null && arm != null)
            {
                bodyProp.objectReferenceValue = arm;
                actorSo.ApplyModifiedPropertiesWithoutUndo();
            }

            Animator used = bodyProp != null && bodyProp.objectReferenceValue != null
                ? (Animator)bodyProp.objectReferenceValue
                : actor.GetComponent<Animator>();
            AnimatorController controller = ControllerOf(used);
            if (controller != null) controllers.Add(controller);

            var qteSo = new SerializedObject(qte);
            Set(qteSo, "_ghost", actor);
            Set(qteSo, "_playerLoop", player);
            Set(qteSo, "_ghostLoop", loop);
            Set(qteSo, "_ghostEscape", escape);
            Set(qteSo, "_ghostFail", escape);   // แพ้ = แขนหดกลับเหมือนกัน ไม่บีบค้างกลางอากาศ

            // ช่องของผู้เล่นห้ามใส่ท่าของแขน — รินไม่มี state GoBack / Attack จะค้าง 1 วิ
            string cleared = "";
            foreach (string field in new[] { "_playerEscape", "_playerFail" })
            {
                SerializedProperty p = qteSo.FindProperty(field);
                if (p == null || p.objectReferenceValue == null) continue;
                Object v = p.objectReferenceValue;
                if (v == loop || v == escape || v.name.StartsWith("Nak_Grab_Loop") || v.name.StartsWith("Nak_Grab_Escape"))
                {
                    p.objectReferenceValue = null;
                    cleared += $" · ล้าง {(field == "_playerEscape" ? "Player Escape" : "Player Fail")} (ใส่ท่าแขนไว้ผิดฝั่ง)";
                }
            }

            SerializedProperty mash = qteSo.FindProperty("_mashInput");
            string mashNote = "";
            if (mash != null && mash.objectReferenceValue == null)
            {
                if (mashInput != null) { mash.objectReferenceValue = mashInput; mashNote = $" · Mash Input = {mashInput.name}"; }
                else mashNote = " · ⚠ Mash Input ยังว่าง (หา Player/Interact ไม่เจอ ใส่เอง)";
            }
            qteSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);

            Log.AppendLine($"✓ {name}");
            Log.AppendLine($"   Actor บน {actor.gameObject.name}{(addedActor ? " (เพิ่มใหม่)" : "")} · Player{(addedPlayer ? " (เพิ่มใหม่)" : " มีแล้ว")}" +
                           $" · Body Animator = {(used != null ? used.gameObject.name : "⚠ ไม่เจอ")}{mashNote}{cleared}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ตัวที่มี StateMachineController (EnemyController) — ไม่เจอใช้ตัวชื่อ EnemyController / ตัวบนสุด
    private static GameObject FindOwner(GameObject root)
    {
        foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && mb.GetType().Name == "StateMachineController")
                return mb.gameObject;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "EnemyController")
                return t.gameObject;

        return root;
    }

    private static Animator FindArmAnimator(GameObject root)
    {
        Animator fallback = null;
        foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
        {
            if (a.runtimeAnimatorController == null) continue;
            if (a.gameObject.name.Contains("Nak")) return a;
            if (fallback == null) fallback = a;
        }
        return fallback;
    }

    private static AnimatorController ControllerOf(Animator animator)
    {
        if (animator == null) return null;
        RuntimeAnimatorController rac = animator.runtimeAnimatorController;
        if (rac is AnimatorOverrideController over) rac = over.runtimeAnimatorController;
        return rac as AnimatorController;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }

    // ---------- Animator ----------

    private static void SetupController(AnimatorController controller)
    {
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        Log.AppendLine($"\nAnimator: {controller.name}");

        var states = new Dictionary<string, AnimatorState>();
        CollectStates(sm, states);

        // วาง state ใหม่ใต้ GoBack
        Vector3 basePos = new Vector3(300f, 300f, 0f);
        foreach (ChildAnimatorState child in sm.states)
            if (child.state.name == "GoBack") basePos = child.position + new Vector3(0f, 80f, 0f);

        int added = 0;
        foreach (var pair in StateClips)
        {
            string stateName = pair.state;
            string clipName = pair.clip;
            AnimationClip clip = FindClip(clipName);
            if (!states.TryGetValue(stateName, out AnimatorState state))
            {
                state = sm.AddState(stateName, basePos + new Vector3(0f, 80f * added, 0f));
                states[stateName] = state;
                added++;
                Log.Append($"   + สร้าง state {stateName}");
            }
            else
            {
                Log.Append($"   • {stateName}");
            }

            if (clip != null)
            {
                state.motion = clip;
                EditorUtility.SetDirty(state);
                Log.AppendLine($" = {clip.name}");
            }
            else
            {
                Log.AppendLine($"  ⚠ หาคลิป {clipName} ไม่เจอ");
            }
        }

        // Attack → ChockLoop (คว้าเสร็จแล้วบีบค้างต่อเอง)
        if (states.TryGetValue("Attack", out AnimatorState attack) && states.TryGetValue("ChockLoop", out AnimatorState chock))
        {
            AnimatorStateTransition link = null;
            foreach (AnimatorStateTransition t in attack.transitions)
                if (t.destinationState == chock) link = t;

            if (link == null)
            {
                link = attack.AddTransition(chock);
                Log.AppendLine("   + ต่อลูกศร Attack → ChockLoop");
            }
            link.hasExitTime = true;
            link.exitTime = 1f;
            link.hasFixedDuration = true;
            link.duration = 0.1f;
            EditorUtility.SetDirty(link);
        }

        EditorUtility.SetDirty(controller);
    }

    private static void CollectStates(AnimatorStateMachine sm, Dictionary<string, AnimatorState> states)
    {
        foreach (ChildAnimatorState child in sm.states)
            if (!states.ContainsKey(child.state.name))
                states[child.state.name] = child.state;
        foreach (ChildAnimatorStateMachine sub in sm.stateMachines)
            CollectStates(sub.stateMachine, states);
    }

    private static string Norm(string s) => s.ToLowerInvariant().Replace(" ", "").Replace("_", "");

    // ไฟล์ .anim ก่อน (ตัวที่ตั้ง Loop Time ไว้) แล้วค่อยคลิปใน FBX
    private static AnimationClip FindClip(string name)
    {
        string want = Norm(name);
        AnimationClip fromModel = null;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip Nak"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            bool fileMatch = Norm(Path.GetFileNameWithoutExtension(path)) == want;

            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(obj is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                if (!fileMatch && Norm(clip.name) != want) continue;

                if (path.EndsWith(".anim")) return clip;
                if (fromModel == null) fromModel = clip;
            }
        }
        return fromModel;
    }

    // ---------- Paired Animation Id ----------

    private static string AssetFolder()
    {
        string parent = "Assets/ScriptableObject";
        foreach (string guid in AssetDatabase.FindAssets("t:PairedAnimationId Grab_Ghost_Loop"))
        {
            parent = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid)).Replace('\\', '/');
            break;
        }
        if (!AssetDatabase.IsValidFolder(parent)) parent = "Assets";

        string folder = parent + "/Nak_Grab";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, "Nak_Grab");
        return folder;
    }

    private static PairedAnimationId UpsertPaired(string folder, string name, string bodyState, string bodyExit,
                                                  string armState, float transition, bool loop, float snap)
    {
        PairedAnimationId asset = null;
        foreach (string guid in AssetDatabase.FindAssets($"t:PairedAnimationId {name}"))
        {
            var found = AssetDatabase.LoadAssetAtPath<PairedAnimationId>(AssetDatabase.GUIDToAssetPath(guid));
            if (found != null && found.name == name) { asset = found; break; }
        }

        bool created = asset == null;
        if (created)
        {
            asset = ScriptableObject.CreateInstance<PairedAnimationId>();
            AssetDatabase.CreateAsset(asset, $"{folder}/{name}.asset");
        }

        var so = new SerializedObject(asset);
        so.FindProperty("_body.StateName").stringValue = bodyState;
        so.FindProperty("_body.Layer").intValue = 0;
        so.FindProperty("_body.ExitStateName").stringValue = bodyExit;
        so.FindProperty("_arm.StateName").stringValue = armState;
        so.FindProperty("_arm.Layer").intValue = 0;
        so.FindProperty("_arm.ExitStateName").stringValue = "";
        so.FindProperty("_transitionDuration").floatValue = transition;
        so.FindProperty("_loop").boolValue = loop;
        so.FindProperty("_moveMode").enumValueIndex = (int)PairedMoveMode.None;
        so.FindProperty("_snapDuration").floatValue = snap;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);

        Log.AppendLine($"{(created ? "+ สร้าง" : "• ตั้งค่า")} {name}: Body = {bodyState}{(loop ? " (Loop)" : "")}");
        return asset;
    }

    // ---------- Input ----------

    private static Object FindInteractInput()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:InputActionReference"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (obj == null || obj.GetType().Name != "InputActionReference") continue;
                string n = obj.name.Replace(" ", "");
                if (n.EndsWith("/Interact") && n.StartsWith("Player")) return obj;
            }
        }
        return null;
    }
}
