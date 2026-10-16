using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

/// <summary>
/// ONE-TIME tool for the Self Anchor migration. Delete this file afterwards.
/// StateElementSO._selfAnchor temporarily has [FormerlySerializedAs] for the old per-blueprint
/// anchor fields, so Unity already reads the old values into _selfAnchor. Reserializing writes them
/// back under the new name; after that the [FormerlySerializedAs] lines can be removed safely.
/// </summary>
public static class SelfAnchorMigration
{
    [MenuItem("Tools/State Machine/Reserialize Blueprints (Self Anchor migration)")]
    private static void Reserialize()
    {
        string[] paths = AssetDatabase.FindAssets($"t:{nameof(StateElementSO)}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .ToArray();

        if (!EditorUtility.DisplayDialog(
                "Reserialize Blueprints",
                $"Rewrite {paths.Length} Action/Condition assets so old anchor fields are saved as Self Anchor?\n\n" +
                "Commit first so you can undo.",
                "Reserialize", "Cancel"))
        {
            return;
        }

        AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssets);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SelfAnchorMigration] Reserialized {paths.Length} blueprint assets. " +
                  "Now remove the [FormerlySerializedAs] lines from StateElementSO and delete this file.");
    }
}
