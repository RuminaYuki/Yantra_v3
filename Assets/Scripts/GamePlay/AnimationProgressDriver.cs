using UnityEngine;

public class AnimationProgressDriver : MonoBehaviour
{
    [SerializeField] Animator _animator;
    [SerializeField] BoolEventChannelSO _aPD_SetEnable_AEC;
    [SerializeField] AnimationProgressDriverAdvEventChannal _aPD_AEC;

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

    private void HandleSetEnable(bool enable)
    {
        //Debug.Log($"APD {enable}");
        if (enable)
        {
            _animator.speed = 0f;
        }
        else
        {
            _animator.speed = 1f;
        }
    }

    private void HandleSetProgress(
        string animationName,
        int layerIndex,
        float progress,
        float normalizedStart,
        float normalizedEnd)
    {
        if (!_animator.HasState(0, Animator.StringToHash(animationName)))
        {
            Debug.LogWarning(
                $"Animation state '{animationName}' was not found.",
                this
            );

            HandleSetEnable(false);

            return;
        }

        normalizedStart = Mathf.Clamp01(normalizedStart);
        normalizedEnd = Mathf.Clamp01(normalizedEnd);

        float normalizedTime = Mathf.Lerp(
            normalizedStart,
            normalizedEnd,
            progress
        );


        _animator.Play(animationName, layerIndex, normalizedTime);
        _animator.Update(0f);
    }
}
