using UnityEngine;

public class GameObjectHandeler : MonoBehaviour
{
    [SerializeField] private GameObject _gameObject;
    [SerializeField] private bool _enableDebug = false;
    void Awake()
    {
        if (_gameObject == null)
        {
            Debug.LogWarning("_gameObject reference has not been assigned.");
        }
    }

    public void EnableMesh()
    {
        if(_enableDebug)
            Debug.Log("Enable");
        _gameObject.SetActive(true);
    }
    public void DisableMesh()
    {
        if(_enableDebug)
            Debug.Log("Disable");
        _gameObject.SetActive(false);
    }
}
