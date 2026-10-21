using UnityEngine;

// Permanent unique id for one object placed in a scene (an enemy, a lever, a spawn point...).
// Saveables use it as their section id, so the same object finds its own data after loading.
//
// The id is created automatically in the Editor and stays the same after the object
// is renamed or moved. Duplicating an object gives the copy a new id.
// Do not change an id after the game has save files: that object's saved state would be lost.
[DisallowMultipleComponent]
public class SaveId : MonoBehaviour
{
    [SerializeField] private string id;

    public string Id => id;

    public bool HasId => !string.IsNullOrEmpty(id);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Changing serialized data inside OnValidate directly can be lost or warn, so wait one Editor tick.
        UnityEditor.EditorApplication.delayCall -= EnsureUniqueId;
        UnityEditor.EditorApplication.delayCall += EnsureUniqueId;
    }

    private void EnsureUniqueId()
    {
        if (this == null || Application.isPlaying) return;

        // A prefab asset must not own an id, otherwise every instance would share it.
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
        {
            if (HasId) SetId(string.Empty);
            return;
        }

        // Objects that are not in a scene (e.g. open in Prefab Mode) are left alone.
        if (!gameObject.scene.IsValid()) return;
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null) return;

        if (!HasId || IsUsedByAnother()) SetId(System.Guid.NewGuid().ToString("N"));
    }

    private bool IsUsedByAnother()
    {
        foreach (SaveId other in FindObjectsByType<SaveId>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == this || other.gameObject.scene != gameObject.scene) continue;
            if (other.id == id) return true;
        }
        return false;
    }

    private void SetId(string value)
    {
        UnityEditor.Undo.RecordObject(this, "Set Save Id");
        id = value;
        UnityEditor.EditorUtility.SetDirty(this);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    [ContextMenu("Generate New Id")]
    private void GenerateNewId()
    {
        if (Application.isPlaying) return;
        SetId(System.Guid.NewGuid().ToString("N"));
    }
#endif
}
