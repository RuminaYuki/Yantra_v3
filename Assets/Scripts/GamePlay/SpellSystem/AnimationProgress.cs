using UnityEngine;

public class AnimationProgress : MonoBehaviour
{
    [Header("References")]
    [SerializeField] FloatEventChannelSO _spellProgress;
    [SerializeField] VoidEventChannelSO _finishedSpell;
    [SerializeField] BoolEventChannelSO _aPD_SetEnable_AEC;
    [SerializeField] AnimationProgressDriverAdvEventChannal _aPD_AEC;

    [Header("Animation Setting")]
    [SerializeField] int _layerIndex;
    [SerializeField] string _animationName;


    private void OnEnable()
    {
        if (_spellProgress != null)
        {
            _spellProgress.Raised += HandleProgressChanger;
        }

        if (_finishedSpell != null)
        {
            _finishedSpell.Raised += HandleFinishSpell;
        }

        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raise(true);
            _aPD_SetEnable_AEC.Raised += HandleDestroy;
        }
    }

    private void OnDisable()
    {
        if (_spellProgress != null)
        {
            _spellProgress.Raised -= HandleProgressChanger;
        }

        if (_finishedSpell != null)
        {
            _finishedSpell.Raised -= HandleFinishSpell;
        }

        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raise(false);
            _aPD_SetEnable_AEC.Raised -= HandleDestroy;
        }
    }


    private void HandleProgressChanger(float value)
    {
        _aPD_AEC.Raise(_animationName, _layerIndex, value, 0, 1);
    }

    private void HandleFinishSpell()
    {
        _aPD_SetEnable_AEC.Raise(false);
    }

    private void HandleDestroy(bool force)
    {
        if (force) return;
        Destroy(gameObject);
    }
}