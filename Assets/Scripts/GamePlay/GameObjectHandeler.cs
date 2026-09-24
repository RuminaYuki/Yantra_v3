using UnityEngine;

public class GameObjectHandeler : MonoBehaviour
{
    [SerializeField] private GameObject _gameObject;
    [SerializeField] private bool _enableOnAwake = false;
    [SerializeField] private bool _enableDebug = false;
    void Awake()
    {
        if (_gameObject == null)
        {
            Debug.LogWarning("_gameObject reference has not been assigned.");
        }
        if(_enableOnAwake)
        {
            EnableGameobject();
        }
    }

    public void EnableGameobject()
    {
        if(_enableDebug)
            Debug.Log("Enable");
        _gameObject.SetActive(true);
    }
    public void DisableGameobject()
    {
        if(_enableDebug)
            Debug.Log("Disable");
        _gameObject.SetActive(false);
    }
}
