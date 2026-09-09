using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Component
{
    public static T Instance { get; private set; }
    protected virtual bool UseDontDestroyOnLoad => true;

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this as T;
            if (UseDontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this as T) Instance = null;
    }
}