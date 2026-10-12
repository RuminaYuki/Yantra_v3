// Temporary Editor-only helper. Install in Assets/Editor and remove after validation.
// It changes only the explicitly copied Poom tables, scene objects, and new local test assets.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using SDFcl.GamePlay.Interactable;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using UObject = UnityEngine.Object;

[InitializeOnLoad]
public static class CodexGlorySetup
{
    const string SceneName = "PoomTestInteractRoom";
    const string Folder = "Assets/Scripts/Character/General/PairedAnimation/Glory/Data/PoomGloryTest";
    const string ReportPath = @"C:\Users\komin\Documents\Codex\2026-10-11\v\work\glory_verification.json";
    const string PlayerTableGuid = "38821288e735a814dba59e842a3fed37";
    const string EnemyTableGuid = "8b00f211134c7a44eae6097e49370e52";
    const string InteractConditionGuid = "45d2dd7c3a55599428b14c8cd6c4c8d4";
    const string PlayerStateName = "PoomGlory_Player_State";
    const string EnemyStateName = "PoomGlory_Victim_State";
    const string EndStateName = "PoomGlory_Finished_State";

    static Report report = new Report();
    static double monitoringUntil, nextSample;
    static BaseInteractor observedInteractor;
    static Context observed;

    static CodexGlorySetup()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingPlayMode) StopMonitoring();
        };
    }

    sealed class Context
    {
        public Scene scene;
        public PlayerCameraInput input;
        public GameObject player;
        public GloryInteractAddition addition;
        public BaseInteractor interactor;
        public PairedAnimationActor victim;
        public StateMachineController playerMachine, enemyMachine;
        public Animator body, arm, enemyAnimator;
        public PlayerCameraController camera;
        public Transform fppHead;
    }

    static Context GetContext()
    {
        var scene = SceneManager.GetActiveScene();
        Require(scene.name == SceneName && scene.isLoaded, "Open PoomTestInteractRoom before using this helper.");
        var inputs = SceneObjects<PlayerCameraInput>(scene);
        var additions = SceneObjects<GloryInteractAddition>(scene);
        Require(inputs.Length == 1 && additions.Length == 1, "Expected exactly one PlayerCameraInput and one GloryInteractAddition in this scene.");
        var c = new Context { scene = scene, input = inputs[0], addition = additions[0] };
        c.player = c.input.gameObject;
        c.camera = c.input.cameraController;
        c.interactor = c.addition.GetComponent<BaseInteractor>();
        c.victim = Ref(c.addition, "_victim") as PairedAnimationActor;
        Require(c.victim != null && c.interactor != null && c.camera != null, "Assign Victim, BaseInteractor and PlayerCameraInput.CameraController first.");
        c.playerMachine = c.player.GetComponent<StateMachineController>();
        c.enemyMachine = c.victim.GetComponent<StateMachineController>();
        c.body = c.player.GetComponent<Animator>();
        c.enemyAnimator = c.victim.GetComponent<Animator>();
        c.arm = c.player.transform.root.GetComponentsInChildren<Animator>(true)
            .SingleOrDefault(a => a.gameObject.name == "ArmController");
        Require(c.playerMachine != null && c.enemyMachine != null && c.body != null && c.arm != null && c.enemyAnimator != null,
            "Player/enemy need their StateMachineController and Animator; the player needs ArmController's Animator.");
        c.fppHead = c.camera.vcamGlory != null ? c.camera.vcamGlory.Target.TrackingTarget : null;
        return c;
    }

    [MenuItem("Tools/Poom Glory/Apply setup")]
    public static void ApplySetup()
    {
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode before applying setup.");
            var c = GetContext();
            var playerTable = ByGuid<TransitionTableSO>(PlayerTableGuid);
            var enemyTable = ByGuid<TransitionTableSO>(EnemyTableGuid);
            Require(TableAt(c.playerMachine, 1) == playerTable && TableAt(c.enemyMachine, 0) == enemyTable,
                "The active scene must use the explicitly copied Poomtest PlayerAction and EnemyTestEnterToGlory tables. No other tables will be edited.");
            Require(c.body.runtimeAnimatorController is AnimatorController && c.arm.runtimeAnimatorController is AnimatorController && c.enemyAnimator.runtimeAnimatorController is AnimatorController,
                "Expected three ordinary Animator Controllers, not override controllers.");
            var armClip = ModelClip("Assets/Animation/AnimationPack/Rin/Fpp/Pair/Rin_Gkory_Ka.fbx", "Rin_Rig|Ka_Stun_Glory");
            var victimClip = ModelClip("Assets/Animation/AnimationPack/Enemy/Ka/Pair/Ka_Glory.fbx", "Ka_Rig|Ka_Glory");
            Require(armClip.length > 0.1f && victimClip.length > 0.1f, "Imported pair clips have no duration.");
            var interactAnchor = ByGuid<AnchorIDSO>("c90374c069c104d43b654c4ec0c510a2");
            var existingRequest = ByGuid<StateConditionSO>("e43d741fa486b84418d8adc25c9f4e4b");
            var existingFinished = ByGuid<StateConditionSO>("a9d148b6fc3d21847b507d4f6183e2f1");
            var existingPlay = ByGuid<PlayPairedAnimationActionSO>("a08d08dd4545af34e8fe6d747b00038a");
            var existingKill = ByGuid<KillOnPairedFinishActionSO>("5b44cec97896e7e45b4e05af5ede7af4");
            var initial = Ref(playerTable, "_initialState") as StateSO;
            Require(initial != null, "Player copied table has no initial state.");
            Require(c.camera.vcamGlory != null, "Assign PlayerCameraController.vcamGlory before applying.");
            var head = c.camera.vcamGlory.Target.TrackingTarget;
            Require(head != null && head.IsChildOf(c.arm.transform), "Glory camera must track a bone under the FPP Animator.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure copied Poom Glory test");
            EnsureFolder(Folder);
            var bindings = InspectBindings(armClip, c.arm.transform);
            armClip = LocalArmClip(armClip, c.arm.transform, bindings);
            var bodyController = CopyAsset((AnimatorController)c.body.runtimeAnimatorController, "PlayerBody.controller");
            var armController = CopyAsset((AnimatorController)c.arm.runtimeAnimatorController, "PlayerArm.controller");
            var enemyController = CopyAsset((AnimatorController)c.enemyAnimator.runtimeAnimatorController, "Victim.controller");
            var timer = NewAssetClip("GloryBodyTimer.anim");
            Undo.RecordObject(timer, "Set Glory timer duration");
            timer.ClearCurves();
            timer.frameRate = armClip.frameRate > 0 ? armClip.frameRate : 30;
            timer.SetCurve("__PoomGloryTimer", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, armClip.length, 0));
            var settings = AnimationUtility.GetAnimationClipSettings(timer);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(timer, settings);
            var marker = c.player.transform.Find("__PoomGloryTimer");
            if (marker == null)
            {
                var go = new GameObject("__PoomGloryTimer");
                Undo.RegisterCreatedObjectUndo(go, "Add Glory timer marker");
                Undo.SetTransformParent(go.transform, c.player.transform, "Parent Glory marker");
                go.transform.localPosition = Vector3.zero;
            }
            AddAnimatorState(bodyController, "Glory", timer);
            AddAnimatorState(armController, "Glory", armClip);
            AddAnimatorState(enemyController, "GloryVictim", victimClip);
            SetController(c.body, bodyController);
            SetController(c.arm, armController);
            SetController(c.enemyAnimator, enemyController);

            var playerId = CopyAsset(ByGuid<PairedAnimationId>("96636afe6b366ea45b2e97bf9f00e50a"), "PlayerGlory.asset");
            var victimId = CopyAsset(ByGuid<PairedAnimationId>("17f09f540f5ae3b43adbf6daa41b6aa5"), "VictimGlory.asset");
            ConfigureId(playerId, "Glory", DefaultState(bodyController), "Glory", DefaultState(armController));
            ConfigureId(victimId, "GloryVictim", "", "", "");
            Edit(c.addition, so => { so.FindProperty("_playerAnimation").objectReferenceValue = playerId; so.FindProperty("_victimAnimation").objectReferenceValue = victimId; });
            var playerActor = c.player.GetComponent<PairedAnimationActor>() ?? Undo.AddComponent<PairedAnimationActor>(c.player);
            ConfigureActor(playerActor, c.body, c.arm);
            ConfigureActor(c.victim, c.enemyAnimator, null);
            SetProvider(c.victim.gameObject, interactAnchor, c.interactor.gameObject);
            Edit(c.interactor, so => { so.FindProperty("canInteract").boolValue = false; so.FindProperty("hideInteract").boolValue = true; so.FindProperty("holdInteract").boolValue = false; });
            Undo.RecordObject(c.camera, "Assign normal gameplay camera");
            if (c.camera.vcamGameplay == null) c.camera.vcamGameplay = c.camera.GetComponent<CinemachineCamera>();
            Undo.RecordObject(c.camera.vcamGlory, "Track actual animated FPP head");
            var target = c.camera.vcamGlory.Target;
            target.TrackingTarget = head;
            c.camera.vcamGlory.Target = target;
            c.camera.vcamGlory.Priority = 0;
            PrefabUtility.RecordPrefabInstancePropertyModifications(c.camera.vcamGlory);
            PrefabUtility.RecordPrefabInstancePropertyModifications(c.camera);
            if (c.camera.vcamGlory.GetComponent<CinemachineHardLockToTarget>() == null) Undo.AddComponent<CinemachineHardLockToTarget>(c.camera.vcamGlory.gameObject);
            if (c.camera.vcamGlory.GetComponent<CinemachineRotateWithFollowTarget>() == null) Undo.AddComponent<CinemachineRotateWithFollowTarget>(c.camera.vcamGlory.gameObject);

            var request = CopyAsset(existingRequest, "HasGloryRequest.asset");
            var finished = CopyAsset(existingFinished, "PairedFinished.asset");
            var playerPlay = CopyAsset(existingPlay, "PlayerPlayPair.asset");
            var victimPlay = CopyAsset(existingPlay, "VictimPlayPair.asset");
            var kill = CopyAsset(existingKill, "VictimKillOnFinish.asset");
            foreach (var item in new ScriptableObject[] { request, finished, playerPlay, victimPlay, kill }) Edit(item, so => so.FindProperty("_selfAnchor").objectReferenceValue = null);
            Edit(playerPlay, so => { so.FindProperty("_lockLook").boolValue = true; so.FindProperty("_useGloryCamera").boolValue = true; });
            Edit(victimPlay, so => { so.FindProperty("_lockLook").boolValue = false; so.FindProperty("_useGloryCamera").boolValue = false; });
            var activeFlag = NewAsset<FlagSO>("GloryActiveFlag.asset");
            var flagAction = NewAsset<SetFlagOnEnterActionSO>("SetGloryActive.asset");
            Edit(flagAction, so => { so.FindProperty("flag").objectReferenceValue = activeFlag; so.FindProperty("value").boolValue = true; so.FindProperty("changeValueOnExit").boolValue = true; });
            var flagCondition = NewAsset<FlagConditionSO>("GloryActiveCondition.asset");
            Edit(flagCondition, so => so.FindProperty("flag").objectReferenceValue = activeFlag);
            RegisterFlag(c.player, activeFlag);
            var hide = NewAsset<SetHideInteractorSO>("HideFinishedVictim.asset");
            Edit(hide, so => { so.FindProperty("_selfAnchor").objectReferenceValue = interactAnchor; so.FindProperty("value").boolValue = true; so.FindProperty("resetValueOnExit").boolValue = false; });
            var playerState = NewAsset<StateSO>(PlayerStateName + ".asset");
            var enemyState = NewAsset<StateSO>(EnemyStateName + ".asset");
            var endState = NewAsset<StateSO>(EndStateName + ".asset");
            SetActions(playerState, flagAction, playerPlay);
            SetActions(enemyState, victimPlay, kill);
            SetActions(endState, hide);
            GuardNormalInteraction(playerTable, flagCondition);
            AddAny(playerTable, playerState, request);
            AddLocal(playerTable, playerState, initial, finished);
            AddAny(enemyTable, enemyState, request);
            AddLocal(enemyTable, enemyState, endState, finished);
            AddLayout(playerTable, playerState, new Vector2(850, -160));
            AddLayout(enemyTable, enemyState, new Vector2(850, -160));
            AddLayout(enemyTable, endState, new Vector2(1100, -160));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(c.scene);
            Require(EditorSceneManager.SaveScene(c.scene), "Could not save the active scene.");
            Undo.CollapseUndoOperations(group);
            report = new Report { phase = "setup_saved", scene = c.scene.name, bindings = bindings, note = "Readiness retained. Enter Play Mode, Prepare test, focus Game and press E." };
            WriteReport();
            Debug.Log("[Poom Glory] Setup saved. Readiness remains gated; use Prepare test in Play Mode, then press E in Game.");
        }
        catch (Exception ex) { Failure(ex); }
    }

    [MenuItem("Tools/Poom Glory/Prepare test")]
    public static void PrepareTest()
    {
        try
        {
            Require(EditorApplication.isPlaying, "Prepare test runs only in Play Mode; it never bypasses the persistent readiness gate.");
            var c = GetContext();
            var health = c.victim.GetComponent<Health>();
            Require(health != null, "Victim owner has no Health.");
            var collider = c.player.GetComponent<CharacterController>();
            bool wasEnabled = collider != null && collider.enabled;
            if (wasEnabled) collider.enabled = false;
            var forward = Vector3.ProjectOnPlane(c.victim.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward;
            var position = c.victim.transform.position + forward * 1.6f;
            position.y = c.player.transform.position.y;
            c.player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-forward));
            if (wasEnabled) collider.enabled = true;
            var eye = Ref(c.camera, "_fppEyePosition") as Transform;
            var aim = c.interactor.transform.position - (eye != null ? eye.position : c.camera.transform.position);
            var rotation = Quaternion.LookRotation(aim);
            typeof(PlayerCameraController).GetField("_yaw", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c.camera, rotation.eulerAngles.y);
            typeof(PlayerCameraController).GetField("_pitch", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c.camera, Mathf.DeltaAngle(0, rotation.eulerAngles.x));
            c.camera.transform.rotation = rotation;
            c.camera.IsLookLocked = false;
            c.camera.SetGloryCameraActive(false);
            if (!health.IsDead) health.TakeDamage(health.CurrentHP);
            StartMonitoring(c);
            Debug.Log("[Poom Glory] Victim damaged through Health.TakeDamage for readiness. Wait for ReadyGlory, focus Game, press E once. Monitoring actual OnInteract and paired states for 120 seconds.");
        }
        catch (Exception ex) { Failure(ex); }
    }

    [MenuItem("Tools/Poom Glory/Report state")]
    public static void ReportState()
    {
        try
        {
            var c = GetContext();
            if (report == null) report = new Report();
            report.scene = c.scene.name;
            report.phase = EditorApplication.isPlaying ? "runtime_report" : "edit_report";
            report.samples.Add(Sample(c));
            WriteReport();
            Debug.Log("[Poom Glory] Report written to " + ReportPath);
        }
        catch (Exception ex) { Failure(ex); }
    }

    static void StartMonitoring(Context c)
    {
        StopMonitoring();
        observed = c;
        observedInteractor = c.interactor;
        observedInteractor.OnInteract += Interacted;
        Application.logMessageReceived += CaptureLog;
        var bindings = InspectBindings(ModelClip("Assets/Animation/AnimationPack/Rin/Fpp/Pair/Rin_Gkory_Ka.fbx", "Rin_Rig|Ka_Stun_Glory"), c.arm.transform);
        var actualClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/ActualPlayerGlory.anim");
        if (actualClip != null) { var actual = InspectBindings(actualClip, c.arm.transform); bindings.pathsMatchedAfter = actual.pathsMatched; bindings.unmatchedAfter = actual.unmatched; }
        report = new Report { scene = c.scene.name, phase = "awaiting_actual_E", bindings = bindings, note = "No scripted Interact or TryRequest invocation; OnInteract is observed from the real input pipeline." };
        monitoringUntil = EditorApplication.timeSinceStartup + 120;
        nextSample = 0;
        EditorApplication.update += Poll;
        Poll();
    }
    static void Interacted(GameObject player) { report.interactEvents++; report.events.Add("OnInteract at " + Time.time.ToString("F3") + " from " + (player != null ? player.name : "null")); WriteReport(); }
    static void CaptureLog(string message, string stack, LogType type) { if (message.Contains("PairedAnimationActor") || type == LogType.Exception) report.events.Add(type + ": " + message); }
    static void Poll()
    {
        if (!EditorApplication.isPlaying || observed == null || observed.player == null || EditorApplication.timeSinceStartup >= monitoringUntil) { StopMonitoring(); return; }
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) report.eKeyObserved = true;
        if (EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + 0.1;
        var sample = Sample(observed);
        report.samples.Add(sample);
        if (sample.playerState == PlayerStateName) report.playerEnteredGlory = true;
        if (sample.enemyState == EnemyStateName) report.victimEnteredGlory = true;
        if (sample.lookLocked && sample.gloryPriority >= 20) report.cameraLockedObserved = true;
        if (report.samples.Count > 1)
        {
            var baseline = report.samples[0];
            report.maxHeadLocalPositionDelta = Mathf.Max(report.maxHeadLocalPositionDelta, Vector3.Distance(baseline.headLocalPosition, sample.headLocalPosition));
            report.maxHeadLocalRotationDelta = Mathf.Max(report.maxHeadLocalRotationDelta, Quaternion.Angle(Quaternion.Euler(baseline.headLocalEuler), Quaternion.Euler(sample.headLocalEuler)));
        }
        if (sample.enemyState == EndStateName && sample.playerState != PlayerStateName && !sample.lookLocked && sample.gloryPriority == 0) report.finishedAndCameraRestored = true;
        report.phase = report.finishedAndCameraRestored ? "finished_and_camera_restored" : (report.playerEnteredGlory ? "paired_observed" : "awaiting_actual_E");
        WriteReport();
    }
    static void StopMonitoring()
    {
        EditorApplication.update -= Poll;
        Application.logMessageReceived -= CaptureLog;
        if (observedInteractor != null) observedInteractor.OnInteract -= Interacted;
        observedInteractor = null;
        observed = null;
        if (report != null && report.samples.Count > 0) WriteReport();
    }

    static Snapshot Sample(Context c)
    {
        var actor = c.player.GetComponent<PairedAnimationActor>();
        var health = c.victim.GetComponent<Health>();
        var r = new Snapshot { time = Time.time, playerPosition = c.player.transform.position, victimPosition = c.victim.transform.position,
            playerState = c.playerMachine.GetCurrentStateName(1), enemyState = c.enemyMachine.GetCurrentStateName(0),
            playerPending = actor != null && actor.HasPending(PairedActionType.Glory), victimPending = c.victim.HasPending(PairedActionType.Glory),
            playerBusy = actor != null && actor.IsBusy, victimBusy = c.victim.IsBusy, victimFinished = c.victim.IsFinished,
            canInteract = c.interactor.CanInteract, rawCanInteract = new SerializedObject(c.interactor).FindProperty("canInteract").boolValue,
            lookLocked = c.camera.IsLookLocked, cutscene = c.camera.IsCutsceneMode,
            gloryPriority = c.camera.vcamGlory != null ? (int)c.camera.vcamGlory.Priority : -999,
            gameplayPriority = c.camera.vcamGameplay != null ? (int)c.camera.vcamGameplay.Priority : -999,
            hp = health != null ? health.CurrentHP : -999,
            bodyController = c.body.runtimeAnimatorController != null ? c.body.runtimeAnimatorController.name : "None",
            armController = c.arm.runtimeAnimatorController != null ? c.arm.runtimeAnimatorController.name : "None",
            bodyGlory = EditorApplication.isPlaying && c.body.GetCurrentAnimatorStateInfo(0).IsName("Glory"),
            armGlory = EditorApplication.isPlaying && c.arm.GetCurrentAnimatorStateInfo(0).IsName("Glory"),
            victimGlory = EditorApplication.isPlaying && c.enemyAnimator.GetCurrentAnimatorStateInfo(0).IsName("GloryVictim") };
        if (c.fppHead != null) { r.headLocalPosition = c.fppHead.localPosition; r.headLocalEuler = c.fppHead.localEulerAngles; r.headWorldPosition = c.fppHead.position; r.headWorldEuler = c.fppHead.eulerAngles; }
        if (actor != null && actor.CurrentRequest.Animation != null) r.requestStart = actor.CurrentRequest.StartPosition;
        if (EditorApplication.isPlaying)
        {
            r.bodyTime = c.body.GetCurrentAnimatorStateInfo(0).normalizedTime;
            r.armTime = c.arm.GetCurrentAnimatorStateInfo(0).normalizedTime;
            r.victimTime = c.enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            var machine = c.enemyMachine.GetStateMachine(0);
            if (machine?.CurrentState != null) foreach (var action in machine.CurrentState.Actions)
                if (action is SubStateMachineAction sub && sub.ChildStateMachine != null) r.enemyChildState = sub.ChildStateMachine.CurrentStateName;
        }
        return r;
    }

    static void GuardNormalInteraction(TransitionTableSO table, FlagConditionSO guard)
    {
        var interact = ByGuid<StateConditionSO>(InteractConditionGuid);
        Edit(table, so =>
        {
            var transitions = so.FindProperty("_anyTransitions");
            for (int i = 0; i < transitions.arraySize; i++)
            {
                var groups = transitions.GetArrayElementAtIndex(i).FindPropertyRelative("ConditionGroups");
                for (int g = 0; g < groups.arraySize; g++)
                {
                    var conditions = groups.GetArrayElementAtIndex(g).FindPropertyRelative("Conditions");
                    bool positiveInteract = false, alreadyGuarded = false;
                    for (int j = 0; j < conditions.arraySize; j++)
                    {
                        var item = conditions.GetArrayElementAtIndex(j);
                        var condition = item.FindPropertyRelative("Condition").objectReferenceValue;
                        if (condition == interact && item.FindPropertyRelative("ExpectedResult").enumValueIndex == 0) positiveInteract = true;
                        if (condition == guard) alreadyGuarded = true;
                    }
                    if (positiveInteract && !alreadyGuarded)
                    {
                        int index = conditions.arraySize++;
                        var item = conditions.GetArrayElementAtIndex(index);
                        item.FindPropertyRelative("Condition").objectReferenceValue = guard;
                        item.FindPropertyRelative("ExpectedResult").enumValueIndex = 1;
                    }
                }
            }
        });
    }
    static void AddAny(TransitionTableSO table, StateSO to, StateConditionSO condition)
    {
        Edit(table, so => { var list = so.FindProperty("_anyTransitions"); int index = FindTransition(list, null, to); if (index < 0) { index = list.arraySize++; list.MoveArrayElement(index, 0); index = 0; } else if (index != 0) { list.MoveArrayElement(index, 0); index = 0; } FillTransition(list.GetArrayElementAtIndex(index), null, to, condition); });
    }
    static void AddLocal(TransitionTableSO table, StateSO from, StateSO to, StateConditionSO condition)
    {
        Edit(table, so => { var list = so.FindProperty("_transitions"); int index = FindTransition(list, from, to); if (index < 0) index = list.arraySize++; FillTransition(list.GetArrayElementAtIndex(index), from, to, condition); });
    }
    static int FindTransition(SerializedProperty list, StateSO from, StateSO to)
    {
        for (int i = 0; i < list.arraySize; i++) { var row = list.GetArrayElementAtIndex(i); if (row.FindPropertyRelative("ToState").objectReferenceValue == to && (from == null || row.FindPropertyRelative("FromState").objectReferenceValue == from)) return i; } return -1;
    }
    static void FillTransition(SerializedProperty row, StateSO from, StateSO to, StateConditionSO condition)
    {
        if (from != null) row.FindPropertyRelative("FromState").objectReferenceValue = from;
        row.FindPropertyRelative("ToState").objectReferenceValue = to;
        var groups = row.FindPropertyRelative("ConditionGroups"); groups.arraySize = 1;
        var conditions = groups.GetArrayElementAtIndex(0).FindPropertyRelative("Conditions"); conditions.arraySize = 1;
        conditions.GetArrayElementAtIndex(0).FindPropertyRelative("Condition").objectReferenceValue = condition;
        conditions.GetArrayElementAtIndex(0).FindPropertyRelative("ExpectedResult").enumValueIndex = 0;
    }
    static void AddLayout(TransitionTableSO table, StateSO state, Vector2 position)
    {
        Edit(table, so => { var list = so.FindProperty("_nodePositions"); int index = -1; for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("State").objectReferenceValue == state) index = i; if (index < 0) index = list.arraySize++; var row = list.GetArrayElementAtIndex(index); row.FindPropertyRelative("State").objectReferenceValue = state; row.FindPropertyRelative("Position").vector2Value = position; });
    }
    static void SetActions(StateSO state, params StateActionSO[] actions) { Edit(state, so => { var list = so.FindProperty("_actions"); list.arraySize = actions.Length; for (int i = 0; i < actions.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = actions[i]; }); }
    static void ConfigureId(PairedAnimationId id, string body, string bodyExit, string arm, string armExit) { Edit(id, so => { SetAnimatorId(so.FindProperty("_body"), body, bodyExit); SetAnimatorId(so.FindProperty("_arm"), arm, armExit); }); }
    static void SetAnimatorId(SerializedProperty field, string state, string exit) { field.FindPropertyRelative("StateName").stringValue = state; field.FindPropertyRelative("Layer").intValue = 0; field.FindPropertyRelative("ExitStateName").stringValue = exit; }
    static void ConfigureActor(PairedAnimationActor actor, Animator body, Animator arm) { Edit(actor, so => { so.FindProperty("_bodyAnimator").objectReferenceValue = body; so.FindProperty("_armAnimator").objectReferenceValue = arm; }); }
    static void RegisterFlag(GameObject owner, FlagSO flag)
    {
        var flags = owner.GetComponent<StateFlags>() ?? Undo.AddComponent<StateFlags>(owner);
        Edit(flags, so => { var list = so.FindProperty("flags"); for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Flag").objectReferenceValue == flag) return; int index = list.arraySize++; var row = list.GetArrayElementAtIndex(index); row.FindPropertyRelative("Flag").objectReferenceValue = flag; row.FindPropertyRelative("InitialValue").boolValue = false; row.FindPropertyRelative("runtimeValue").boolValue = false; });
    }
    static void SetProvider(GameObject owner, AnchorIDSO anchor, GameObject target)
    {
        var provider = owner.GetComponent<Provider>() ?? Undo.AddComponent<Provider>(owner);
        Edit(provider, so => { var list = so.FindProperty("_anchorReferences"); int index = -1; for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("anchorID").objectReferenceValue == anchor) index = i; if (index < 0) index = list.arraySize++; var row = list.GetArrayElementAtIndex(index); row.FindPropertyRelative("anchorID").objectReferenceValue = anchor; row.FindPropertyRelative("tagetgameObject").objectReferenceValue = target; });
    }
    static void AddAnimatorState(AnimatorController controller, string name, AnimationClip clip)
    {
        Require(controller.layers.Length > 0, "Controller has no Base Layer: " + controller.name);
        var machine = controller.layers[0].stateMachine;
        Undo.RecordObject(machine, "Add local Glory Animator state");
        var state = machine.states.Select(x => x.state).SingleOrDefault(x => x.name == name) ?? machine.AddState(name, new Vector3(900, -100, 0));
        Undo.RecordObject(state, "Assign real Glory motion"); state.motion = clip; state.speed = 1; state.writeDefaultValues = true;
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(state); EditorUtility.SetDirty(machine);
    }
    static string DefaultState(AnimatorController controller) { Require(controller.layers[0].stateMachine.defaultState != null, "Controller has no default state."); return controller.layers[0].stateMachine.defaultState.name; }
    static void SetController(Animator animator, AnimatorController controller) { Undo.RecordObject(animator, "Assign local Glory controller"); animator.runtimeAnimatorController = controller; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; PrefabUtility.RecordPrefabInstancePropertyModifications(animator); EditorUtility.SetDirty(animator); }
    static BindingReport InspectBindings(AnimationClip clip, Transform root)
    {
        var result = new BindingReport { clip = clip.name, duration = clip.length };
        foreach (var path in AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)).Select(b => b.path).Distinct())
        {
            result.pathsTotal++;
            if (string.IsNullOrEmpty(path) || root.Find(path) != null) result.pathsMatched++;
            else result.unmatched.Add(path);
        }
        return result;
    }
    static AnimationClip LocalArmClip(AnimationClip imported, Transform root, BindingReport diagnostics)
    {
        string path = Folder + "/ActualPlayerGlory.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;
        var clip = UObject.Instantiate(imported);
        clip.name = "ActualPlayerGlory";
        // Only unmatched paths are mapped, only when exactly one complete suffix identifies a target.
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path) || root.Find(binding.path) != null) continue;
            string[] candidates = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t != root).Select(t => AnimationUtility.CalculateTransformPath(t, root))
                .Where(p => p.EndsWith("/" + binding.path, StringComparison.Ordinal) || binding.path.EndsWith("/" + p, StringComparison.Ordinal)).ToArray();
            if (candidates.Length != 1) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            var mapped = binding; mapped.path = candidates[0];
            AnimationUtility.SetEditorCurve(clip, binding, null);
            AnimationUtility.SetEditorCurve(clip, mapped, curve);
            diagnostics.remappedPaths.Add(binding.path + " -> " + mapped.path);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false; AnimationUtility.SetAnimationClipSettings(clip, settings);
        var after = InspectBindings(clip, root);
        diagnostics.pathsMatchedAfter = after.pathsMatched;
        diagnostics.unmatchedAfter = after.unmatched;
        Require(after.pathsTotal > 0 && after.pathsMatched == after.pathsTotal,
            "FPP clip has unmatched transform paths. Review bindings in the report before setup: " + string.Join(", ", after.unmatched));
        AssetDatabase.CreateAsset(clip, path);
        Undo.RegisterCreatedObjectUndo(clip, "Create local actual FPP Glory clip");
        return clip;
    }
    static T CopyAsset<T>(T source, string filename) where T : UObject
    {
        string destination = Folder + "/" + filename;
        if (AssetDatabase.GetAssetPath(source) == destination) return source;
        var existing = AssetDatabase.LoadAssetAtPath<T>(destination); if (existing != null) return existing;
        Require(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), destination), "Could not copy " + source.name);
        return AssetDatabase.LoadAssetAtPath<T>(destination);
    }
    static T NewAsset<T>(string filename) where T : ScriptableObject
    {
        string path = Folder + "/" + filename; var existing = AssetDatabase.LoadAssetAtPath<T>(path); if (existing != null) return existing;
        var asset = ScriptableObject.CreateInstance<T>(); asset.name = Path.GetFileNameWithoutExtension(filename); AssetDatabase.CreateAsset(asset, path); Undo.RegisterCreatedObjectUndo(asset, "Create local Glory test asset"); return asset;
    }
    // AnimationClip is an Object, not a ScriptableObject.
    static AnimationClip NewAssetClip(string filename)
    {
        string path = Folder + "/" + filename; var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); if (clip != null) return clip;
        clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(filename) }; AssetDatabase.CreateAsset(clip, path); Undo.RegisterCreatedObjectUndo(clip, "Create Glory timer clip"); return clip;
    }
    static AnimationClip ModelClip(string path, string name) { var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().SingleOrDefault(x => x.name == name); Require(clip != null, "Imported clip missing: " + name); return clip; }
    static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1)); }
    static void Edit(UObject obj, Action<SerializedObject> change) { Undo.RecordObject(obj, "Configure Poom Glory test"); var so = new SerializedObject(obj); change(so); so.ApplyModifiedProperties(); EditorUtility.SetDirty(obj); }
    static UObject Ref(UObject obj, string field) { return new SerializedObject(obj).FindProperty(field).objectReferenceValue; }
    static TransitionTableSO TableAt(StateMachineController controller, int index) { var list = new SerializedObject(controller).FindProperty("_transitionTables"); return list.arraySize > index ? list.GetArrayElementAtIndex(index).objectReferenceValue as TransitionTableSO : null; }
    static T ByGuid<T>(string guid) where T : UObject { var item = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)); Require(item != null, "Missing expected asset GUID " + guid); return item; }
    static T[] SceneObjects<T>(Scene scene) where T : Component { return scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray(); }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Failure(Exception ex) { report.phase = "error"; report.events.Add(ex.ToString()); WriteReport(); Debug.LogException(ex); }
    static void WriteReport() { Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true)); }

    [Serializable] sealed class Report
    {
        public string scene, phase, note;
        public int interactEvents;
        public bool eKeyObserved, playerEnteredGlory, victimEnteredGlory, cameraLockedObserved, finishedAndCameraRestored;
        public float maxHeadLocalPositionDelta, maxHeadLocalRotationDelta;
        public BindingReport bindings;
        public List<string> events = new List<string>();
        public List<Snapshot> samples = new List<Snapshot>();
    }
    [Serializable] sealed class Snapshot
    {
        public float time, hp, bodyTime, armTime, victimTime;
        public string playerState, enemyState, enemyChildState, bodyController, armController;
        public Vector3 playerPosition, victimPosition, requestStart, headLocalPosition, headLocalEuler, headWorldPosition, headWorldEuler;
        public bool playerPending, victimPending, playerBusy, victimBusy, victimFinished, canInteract, rawCanInteract, lookLocked, cutscene, bodyGlory, armGlory, victimGlory;
        public int gloryPriority, gameplayPriority;
    }
    [Serializable] sealed class BindingReport
    {
        public string clip;
        public float duration;
        public int pathsTotal, pathsMatched, pathsMatchedAfter;
        public List<string> unmatched = new List<string>(), unmatchedAfter = new List<string>(), remappedPaths = new List<string>();
    }
}
#endif
