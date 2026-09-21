using UnityEngine;

public class PrefabIdentity : MonoBehaviour
{
    [SerializeField] private string prefabId;

    public string PrefabId => prefabId;
}