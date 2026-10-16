// Place in an Editor folder under Assets/Scripts/Character/General/PairedAnimation/.
// Runs only from its menu. No scenes, prefabs or original imported clips are changed.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PrepareGroundedGloryClip
{
    const string AllowedFolder = "Assets/Scripts/Character/General/PairedAnimation/";
    const string MenuPath = "Tools/Paired Animation/Create grounded victim clip";
    const string VictimStateName = "GloryVictim";

    [MenuItem(MenuPath)]
    public static void CreateGroundedVictimClip()
    {
        string createdPath = null;
        AnimatorState changedState = null;
        Motion previousMotion = null;
        AnimatorController controller = null;
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Stop Play Mode before preparing a clip.");
            controller = Selection.activeObject as AnimatorController;
            Require(controller != null, "Select the copied victim Animator Controller in the Project window.");
            string controllerPath = AssetDatabase.GetAssetPath(controller);
            Require(IsAllowedPath(controllerPath) &&
                controllerPath.EndsWith(".controller", StringComparison.OrdinalIgnoreCase),
                "The selected controller must be inside " + AllowedFolder);
            Require(controller.layers.Length > 0, "The selected controller has no layer 0.");

            var states = new List<AnimatorState>();
            FindStates(controller.layers[0].stateMachine, states);
            Require(states.Count == 1,
                "Expected exactly one state named GloryVictim on layer 0; found " + states.Count + ".");
            AnimatorState state = states[0];
            Require(AssetDatabase.GetAssetPath(state) == controllerPath,
                "GloryVictim must belong to the selected controller asset.");
            var sourceClip = state.motion as AnimationClip;
            Require(sourceClip != null, "GloryVictim must use an imported AnimationClip directly, not a Blend Tree.");
            string sourcePath = AssetDatabase.GetAssetPath(sourceClip);
            Require(IsProjectAssetPath(sourcePath) &&
                sourcePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.IsSubAsset(sourceClip),
                "GloryVictim must currently reference an AnimationClip imported from an FBX in Assets.");
            var sourceImporter = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            Require(sourceImporter != null, "The source clip has no ModelImporter.");
            Require(sourceImporter.animationType == ModelImporterAnimationType.Human,
                "This utility is for a Humanoid victim clip.");
            string clipName = sourceClip.name;
            FindClipSettings(sourceImporter, clipName);
            Avatar originalAvatar = sourceImporter.sourceAvatar;
            ModelImporterAvatarSetup originalAvatarSetup = sourceImporter.avatarSetup;

            string directory = Path.GetDirectoryName(controllerPath).Replace('\\', '/');
            string proposedPath = directory + "/" + Path.GetFileNameWithoutExtension(sourcePath) + "_Grounded.fbx";
            string outputPath = AssetDatabase.GenerateUniqueAssetPath(proposedPath);
            Require(IsAllowedPath(outputPath) && IsProjectAssetPath(outputPath) &&
                outputPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(outputPath, sourcePath, StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(outputPath), "The new FBX path must be unique and inside the PairedAnimation folder.");

            Require(AssetDatabase.CopyAsset(sourcePath, outputPath), "Could not copy the source FBX.");
            createdPath = outputPath;
            var copiedImporter = AssetImporter.GetAtPath(outputPath) as ModelImporter;
            Require(copiedImporter != null, "The copied FBX has no ModelImporter.");
            Require(copiedImporter.sourceAvatar == originalAvatar &&
                copiedImporter.avatarSetup == originalAvatarSetup,
                "The copied importer did not preserve its avatar configuration; the controller was not changed.");

            // Keep every other import setting, clip range, avatar reference and clip untouched.
            var clips = GetClipSettings(copiedImporter);
            var matchingSettings = clips.Where(c => c.name == clipName).ToArray();
            Require(matchingSettings.Length == 1,
                "The copied importer must contain exactly one clip named " + clipName + ".");
            matchingSettings[0].lockRootHeightY = true; // Root Transform Position (Y): Bake Into Pose.
            matchingSettings[0].loopTime = false;
            copiedImporter.clipAnimations = clips;
            copiedImporter.SaveAndReimport();

            var importedClips = AssetDatabase.LoadAllAssetsAtPath(outputPath)
                .OfType<AnimationClip>().Where(c => c.name == clipName).ToArray();
            Require(importedClips.Length == 1,
                "The copied FBX did not import exactly one AnimationClip named " + clipName + ".");
            Require(importedClips[0].isHumanMotion && importedClips[0].length > 0f,
                "The copied victim clip must contain a valid Humanoid animation.");
            var verifiedImporter = (ModelImporter)AssetImporter.GetAtPath(outputPath);
            var verifiedSettings = FindClipSettings(verifiedImporter, clipName);
            Require(verifiedSettings.lockRootHeightY && !verifiedSettings.loopTime &&
                verifiedImporter.sourceAvatar == originalAvatar &&
                verifiedImporter.avatarSetup == originalAvatarSetup,
                "The copied FBX did not retain the requested Y bake and avatar settings.");

            previousMotion = state.motion;
            changedState = state;
            Undo.RecordObject(state, "Assign grounded Glory victim clip");
            state.motion = importedClips[0];
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
            // Save only this controller, without flushing unrelated dirty assets.
            AssetDatabase.SaveAssetIfDirty(controller);
            createdPath = null; // Keep the successful copy; Undo restores the old state motion.
            Debug.Log("[Paired Animation] Created " + outputPath + " and assigned " + clipName +
                " to layer 0/GloryVictim in " + controllerPath +
                ". Original FBX unchanged. Test the fall in Play Mode; Ctrl+Z restores the controller's previous clip.", controller);
        }
        catch (Exception exception)
        {
            if (changedState != null)
            {
                changedState.motion = previousMotion;
                EditorUtility.SetDirty(changedState);
                if (controller != null)
                {
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssetIfDirty(controller);
                }
            }
            // On failure remove only the unique FBX created by this invocation.
            if (!string.IsNullOrEmpty(createdPath) && IsAllowedPath(createdPath))
                AssetDatabase.DeleteAsset(createdPath);
            Debug.LogError("[Paired Animation] " + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    static bool ValidateMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
            Selection.activeObject is AnimatorController controller &&
            IsAllowedPath(AssetDatabase.GetAssetPath(controller));
    }

    static void FindStates(AnimatorStateMachine machine, List<AnimatorState> found)
    {
        foreach (var child in machine.states)
            if (child.state.name == VictimStateName) found.Add(child.state);
        foreach (var child in machine.stateMachines) FindStates(child.stateMachine, found);
    }

    static ModelImporterClipAnimation[] GetClipSettings(ModelImporter importer)
    {
        var clips = importer.clipAnimations;
        return clips.Length > 0 ? clips : importer.defaultClipAnimations;
    }

    static ModelImporterClipAnimation FindClipSettings(ModelImporter importer, string clipName)
    {
        var matching = GetClipSettings(importer).Where(c => c.name == clipName).ToArray();
        Require(matching.Length == 1,
            "Expected exactly one importer clip named " + clipName + "; found " + matching.Length + ".");
        return matching[0];
    }

    static bool IsProjectAssetPath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
            !path.Contains('\\') && !path.Split('/').Any(p => p == ".." || p == ".");
    }

    static bool IsAllowedPath(string path)
    {
        return IsProjectAssetPath(path) && path.StartsWith(AllowedFolder, StringComparison.Ordinal);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
