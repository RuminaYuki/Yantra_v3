using UnityEngine;

[System.Serializable]
public struct SceneReference : ISerializationCallbackReceiver
{
#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset _sceneAsset;
#endif

    [SerializeField, HideInInspector] private string _sceneName;
    [SerializeField, HideInInspector] private string _scenePath;

    public string SceneName => _sceneName;
    public string ScenePath => _scenePath;
    public bool IsValid => !string.IsNullOrEmpty(_sceneName);

    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        // ดึงชื่อจาก asset ทุกครั้งที่เซฟ — ถ้ามีคนเปลี่ยนชื่อ scene จะตามอัตโนมัติ
        if (_sceneAsset != null)
        {
            _scenePath = UnityEditor.AssetDatabase.GetAssetPath(_sceneAsset);
            _sceneName = System.IO.Path.GetFileNameWithoutExtension(_scenePath);
        }
        else
        {
            _scenePath = string.Empty;
            _sceneName = string.Empty;
        }
#endif
    }

    public void OnAfterDeserialize() { }

    public static implicit operator string(SceneReference reference) => reference._sceneName;
}