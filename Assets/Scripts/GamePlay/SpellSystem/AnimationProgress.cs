using UnityEngine;

public class AnimationProgress : MonoBehaviour
{
    [Header("References")]
    [SerializeField] FloatEventChannelSO _spellProgress;
    [SerializeField] BoolEventChannelSO _aPD_SetEnable_AEC;
    [SerializeField] AnimationProgressDriverAdvEventChannal _aPD_AEC;

    [Header("Animation Setting")]
    [SerializeField] int _layerIndex;
    [SerializeField] string _animationName;
    [SerializeField, Range(0f, 1f)] float _startTime = 0f;
    [SerializeField, Range(0f, 1f)] float _endTime = 1f;


    private void OnEnable()
    {
        if (_spellProgress != null)
        {
            _spellProgress.Raised += HandleProgressChanger;
        }

        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raise(true);
        }
    }

    private void OnDisable()
    {
        if (_spellProgress != null)
        {
            _spellProgress.Raised -= HandleProgressChanger;
        }

        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raise(false);
        }
    }


    private void HandleProgressChanger(float value)
    {
        if (value >= 1) 
        {
            if (_spellProgress != null)
            {
                _spellProgress.Raised -= HandleProgressChanger;
            }
            HandleFinishSpell(); 
        }
        _aPD_AEC.Raise(_animationName, _layerIndex, value, _startTime, _endTime);
    }

    private void HandleFinishSpell()
    {
        _aPD_SetEnable_AEC.Raise(false);
    }
}