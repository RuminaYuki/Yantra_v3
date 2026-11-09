using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;


public class FadeObject : MonoBehaviour
{
    [Header("Fade Settings")]
    [FormerlySerializedAs("_targetRenderer")]
    [SerializeField] private Renderer[] _targetRenderers;
    [SerializeField] private float _fadeDuration = 1f;
    [SerializeField] AnimationCurve _animationCurve;

    [Header("Event Channels")]
    [Tooltip("Configuration via code.")]
    [SerializeField] VoidEventChannelSO _onFinishFade;

    private MaterialPropertyBlock _propertyBlock;

    [SerializeField] private string _propertyName = "_Alpha";
    [SerializeField] private bool _isColor;

    private int _fadeID;

    private float _currentFade;

    private void Awake()
    {
        if (_targetRenderers == null || _targetRenderers.Length == 0)
            _targetRenderers = new[] { GetComponent<Renderer>() };


        _propertyBlock = new MaterialPropertyBlock();
        _fadeID = Shader.PropertyToID(_propertyName);
    }

    public void PlayFade(
        float fadeDuration,
        float startFade,
        float endFade,
        VoidEventChannelSO eventChannel = null)
    {
        _fadeDuration = fadeDuration > 0f ? fadeDuration : _fadeDuration;

        _onFinishFade = eventChannel;

        StopAllCoroutines();
        StartCoroutine(Fade(startFade, endFade));
    }

    public void PlayFade(
        float fadeDuration,
        float endFade,
        VoidEventChannelSO eventChannel = null)
    {
        _fadeDuration = fadeDuration > 0f ? fadeDuration : _fadeDuration;

        _onFinishFade = eventChannel;

        StopAllCoroutines();
        StartCoroutine(Fade(_currentFade, endFade));
    }

    private IEnumerator Fade(float startFade, float endFade)
    {
        startFade = Mathf.Clamp01(startFade);
        endFade = Mathf.Clamp01(endFade);

        float time = 0f;

        SetFade(startFade);

        while (time < _fadeDuration)
        {
            time += Time.unscaledDeltaTime;

            float normalizedTime = Mathf.Clamp01(time / _fadeDuration);

            float curveValue = _animationCurve.Evaluate(normalizedTime);

            float fade = Mathf.Lerp(startFade, endFade, curveValue);

            SetFade(fade);

            yield return null;
        }

        if (_onFinishFade != null) _onFinishFade?.Raise();
        SetFade(endFade);
    }

    private void SetFade(float value)
    {
        _currentFade = value;

        foreach (var r in _targetRenderers)
        {
            r.GetPropertyBlock(_propertyBlock);

            if (_isColor)
            {
                Color c = r.sharedMaterial.GetColor(_fadeID);
                c.a = value;
                _propertyBlock.SetColor(_fadeID, c);
            }
            else
            {
                _propertyBlock.SetFloat(_fadeID, value);
            }

            r.SetPropertyBlock(_propertyBlock);
        }
    }
}
