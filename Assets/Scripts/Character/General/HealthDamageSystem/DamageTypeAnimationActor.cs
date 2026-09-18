using UnityEngine;
[RequireComponent(typeof(Animator),typeof(Health))]
public class DamageTypeAnimationActor : MonoBehaviour
{
    [System.Serializable]
    private class DamageTypeAnimation
    {
        public DamageTypeID id;
        public string stateName;
        public float transitionduration = 0.25f;
    }
    [SerializeField] private DamageTypeAnimation[] _animations;
    private Animator _animator;
    private Health _health;
    private DamageTypeID _currentDamageTypeID;
    private int _previousStateHash;
    private int _playedStateHash;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _health = GetComponent<Health>();
    }
    private void OnEnable()
    {
        _health.OnHurtDamageType += ReceiveDamageTypeIDHandle;
    }
    private void OnDisable()
    {
        _health.OnHurtDamageType -= ReceiveDamageTypeIDHandle;
    }
    private void ReceiveDamageTypeIDHandle(DamageTypeID damageTypeid)
    {
        _currentDamageTypeID = damageTypeid;
    }
    public void PlayAnimationWithDamageType()
    {
        foreach(DamageTypeAnimation animation in _animations)
        {
            if(animation.id == _currentDamageTypeID)
            {
                _previousStateHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                _playedStateHash = Animator.StringToHash(animation.stateName);
                _animator.CrossFadeInFixedTime(_playedStateHash,animation.transitionduration);
                break;
            }
        }
    }
    public void RestorePreviousAnimatorState()
    {
        if(_animator.GetCurrentAnimatorStateInfo(0).shortNameHash != _playedStateHash) return;

        _animator.Play(_previousStateHash);
    }
}
