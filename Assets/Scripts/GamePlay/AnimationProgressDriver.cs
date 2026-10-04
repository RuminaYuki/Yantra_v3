using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationProgressDriver : MonoBehaviour
{
    
    [SerializeField] Animator _animator;
    [SerializeField] BoolEventChannelSO _aPD_SetEnable_AEC;
    [SerializeField] AnimationProgressDriverAdvEventChannal _aPD_AEC;

    [Header("Setting")]
    [SerializeField] float speedMultiple = 1f;
    private int _layerIndex;
    private bool _active = false;
    private float _progress = 0;
    AnimatorStateInfo stateInfo;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (_aPD_AEC != null)
        {
            _aPD_AEC.Raised += HandleSetProgress;
        }
        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raised += HandleSetEnable;
        }
    }

    private void OnDisable()
    {
        if (_aPD_AEC != null)
        {
            _aPD_AEC.Raised -= HandleSetProgress;
        }
        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raised -= HandleSetEnable;
        }
    }

    private void Update()
    {
        if (!_active) return;

        SetProgress();
    }

    private void SetProgress()
    {
        stateInfo = _animator.GetCurrentAnimatorStateInfo(_layerIndex); // อ่านสดทุก frame
        float animProgress = stateInfo.normalizedTime;
        _animator.speed = CalculatSpeed(animProgress);
    }

    private float CalculatSpeed(float animProgress)
    {
        float diff = _progress - animProgress;
        float speed = Mathf.Max(diff, 0f) * 10f * speedMultiple; // เดินไปข้างหน้าอย่างเดียว ไม่ถอย
        Debug.Log(speed);
        return speed;
    }

    private void HandleSetEnable(bool enable)
    {
        _active = enable;

        if (!_active && _animator != null)
        {
            _animator.speed = 1;
        }
    }

    private void HandleSetProgress(
        string stateName,
        int layerIndex,
        float progress,
        float normalizedStart,
        float normalizedEnd)
    {
        if (!_active) return;

        if (!_animator.HasState(0, Animator.StringToHash(stateName)))
        {
            Debug.LogWarning(
                $"Animation state '{stateName}' was not found.",
                this
            );

            HandleSetEnable(false);

            return;
        }

        _layerIndex = layerIndex;

        stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (stateInfo.IsName(stateName))
        {
            _progress = progress;
        }
        else
        {
            _animator.Play(stateName, layerIndex);
            _animator.Update(0);
            _animator.speed = 0;
            _progress = 0;
        }
    }
}
