using UnityEngine;

public class AnimationProgress : MonoBehaviour
{
    [Header("References")]
    [SerializeField] FloatEventChannelSO _spellProgress;
    [SerializeField] BoolEventChannelSO _aPD_SetEnable_AEC;
    [SerializeField] AnimationProgressDriverAdvEventChannal _aPD_AEC;

    [Header("Animation Setting")]
    [SerializeField] int _layerIndex;
    [Tooltip("ท่าเริ่ม เล่นจนจบครั้งเดียวก่อนเข้าท่าหลัก (ว่างไว้ได้)")]
    [SerializeField] string _animationNameFade;
    [Tooltip("ท่าหลัก เล่นตาม progress ของการวาด")]
    [SerializeField] string _animationName;
    [SerializeField, Min(0f)] float _fadeDuration = 0f;

    bool _fadeSent;

    private void OnEnable()
    {
        _fadeSent = false;

        if (_spellProgress != null) _spellProgress.Raised += HandleProgressChanged;
        if (_aPD_SetEnable_AEC != null) _aPD_SetEnable_AEC.Raise(true);
    }

    private void OnDisable()
    {
        if (_spellProgress != null) _spellProgress.Raised -= HandleProgressChanged;
        if (_aPD_SetEnable_AEC != null) _aPD_SetEnable_AEC.Raise(false);
    }

    private void HandleProgressChanged(float progress)
    {
        if (_aPD_AEC == null) return;

        progress = Mathf.Clamp01(progress);

        // ส่งท่าเริ่มครั้งแรกครั้งเดียว driver จะเล่นจนจบก่อน
        // ท่าหลักที่ส่งตามไประหว่างนั้น driver จะเก็บไว้แล้วค่อยเล่นต่อเอง
        if (!_fadeSent)
        {
            _fadeSent = true;
            if (!string.IsNullOrEmpty(_animationNameFade))
            {
                _aPD_AEC.Raise(_animationNameFade, _layerIndex, AnimationProgressDriver.PlayFull,
                               _fadeDuration, AnimatorUpdateMode.UnscaledTime);
            }
        }

        _aPD_AEC.Raise(_animationName, _layerIndex, progress, _fadeDuration, AnimatorUpdateMode.UnscaledTime);

        if (progress >= 1f) HandleFinishSpell();
    }

    private void HandleFinishSpell()
    {
        if (_spellProgress != null) _spellProgress.Raised -= HandleProgressChanged;
        if (_aPD_SetEnable_AEC != null) _aPD_SetEnable_AEC.Raise(false);
    }
}
