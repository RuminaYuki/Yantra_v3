using System.Collections;
using UnityEngine;

public class FadeObject : MonoBehaviour
{
    [SerializeField] private Renderer _targetRenderer;
    [SerializeField] private float _fadeDuration = 1f;
    [SerializeField] AnimationCurve animationCurve;

    private MaterialPropertyBlock _propertyBlock;

    private static readonly int FadeID = Shader.PropertyToID("_Alpha");

    private float _currentFade;

    private void Awake()
    {
        if (_targetRenderer == null)
            _targetRenderer = GetComponent<Renderer>();

        _propertyBlock = new MaterialPropertyBlock();
    }

    public void PlayFade(float fadeDuration, float startFade, float endFade)
    {
        _fadeDuration = fadeDuration > 0f ? fadeDuration : _fadeDuration;

        StopAllCoroutines();
        StartCoroutine(Fade(startFade, endFade));
    }

    public void PlayFade(float fadeDuration, float endFade)
    {
        _fadeDuration = fadeDuration > 0f ? fadeDuration : _fadeDuration;

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
            time += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(time / _fadeDuration);

            float curveValue = animationCurve.Evaluate(normalizedTime);

            float fade = Mathf.Lerp(startFade, endFade, curveValue);

            SetFade(fade);

            yield return null;
        }

        SetFade(endFade);
    }

    private void SetFade(float value)
    {
        _currentFade = value;

        _targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(FadeID, value);
        _targetRenderer.SetPropertyBlock(_propertyBlock);
    }
}
