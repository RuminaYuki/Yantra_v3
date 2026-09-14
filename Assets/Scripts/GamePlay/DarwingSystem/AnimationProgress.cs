using UnityEngine;

public class AnimationProgress : MonoBehaviour
{
    [Header("References")]
    [SerializeField] FloatEventChannelSO _drawingProgress;
    [SerializeField] VoidEventChannelSO _finishedDrawing;
    [SerializeField] BoolEventChannelSO _aPD_SetEnable_AEC;
    [SerializeField] AnimationProgressDriverAdvEventChannal _aPD_AEC;

    [Header("Animation Setting")]
    [SerializeField] int _layerIndex;
    [SerializeField] string _animationName;


    private void OnEnable()
    {
        if (_drawingProgress != null)
        {
            _drawingProgress.Raised += HandleProgressChanger;
        }

        if (_finishedDrawing != null)
        {
            _finishedDrawing.Raised += HandleFinishDrawing;
        }

        if (_aPD_SetEnable_AEC != null)
        {
            _aPD_SetEnable_AEC.Raise(true);
            _aPD_SetEnable_AEC.Raised += HandleDestroy;
        }
    }

    private void OnDisable()
    {
        if (_drawingProgress != null)
        {
            _drawingProgress.Raised -= HandleProgressChanger;
        }

        if (_finishedDrawing != null)
        {
            _finishedDrawing.Raised -= HandleFinishDrawing;
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

    private void HandleFinishDrawing()
    {
        _aPD_SetEnable_AEC.Raise(false);
    }

    private void HandleDestroy(bool force)
    {
        if (force) return;
        Destroy(gameObject);
    }
}