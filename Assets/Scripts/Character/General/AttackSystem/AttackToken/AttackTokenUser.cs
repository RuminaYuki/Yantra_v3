using UnityEngine;

public class AttackTokenUser : MonoBehaviour
{
    [SerializeField] private AttackCoordinator _coordinator;

#if UNITY_EDITOR
    [Header("Runtime Debug")]
    [SerializeField] private bool _debugHasToken;
#endif

    public bool HasToken =>
        _coordinator != null &&
        _coordinator.IsOwner(gameObject);

#if UNITY_EDITOR
    private void Update()
    {
        _debugHasToken = HasToken;
    }
#endif

    private void Awake()
    {
        if (_coordinator == null)
        {
            _coordinator = GameObject.FindGameObjectWithTag("Player")?.GetComponent<AttackCoordinator>();
            if (_coordinator == null)
            {
                Debug.LogError(
                    "AttackTokenUser cannot find AttackCoordinator.",
                    this);
            }
        }
    }

    public void SetTarget(GameObject target)
    {
        if (HasToken)
            Release();

        _coordinator = target != null
            ? target.GetComponent<AttackCoordinator>()
            : null;
    }

    public bool TryClaim()
    {
        return _coordinator != null &&
               _coordinator.TryClaim(gameObject);
    }

    public void Release()
    {
        _coordinator?.Release(gameObject);
    }

    private void OnDisable()
    {
        Release();
    }
}
