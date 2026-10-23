using System.Collections;
using UnityEngine;

public class ShootableDoor : MonoBehaviour
{
    [SerializeField] GameObject doorObject;
    [SerializeField] FlagSO doorFlag;

    [SerializeField] private string opacityProperty = "_DissolveAmount";
    [SerializeField] private MeshRenderer meshRenderer;

    [SerializeField] bool smoothDissolve = true;
    [SerializeField] float dissolveSpeed = 1f;

    private Coroutine dissolveCoroutine;

    private void Awake()
    {

    }

    public void OnShot()
    {
        //doorObject.SetActive(false);
        Collider doorCollider = GetComponent<Collider>();
        if (doorCollider != null)
        {
            doorCollider.enabled = false;
        }

        ChangeDissolve();
    }

    public FlagSO GetDoorFlag()
    {
        return doorFlag;
    }

    private void ChangeDissolve()
    {
        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
        }

        if (smoothDissolve)
        {    
            dissolveCoroutine = StartCoroutine(DissolveCoroutine());
        }
    }

    private float GetDissolveValue()
    {
        if (meshRenderer.TryGetComponent<Renderer>(out Renderer renderer))
        {
            return renderer.material.GetFloat(opacityProperty);
        }
        return 0f;
    }

    private void SetDissolveValue(float value)
    {
        if (meshRenderer.TryGetComponent<Renderer>(out Renderer renderer))
        {
            renderer.material.SetFloat(opacityProperty, value);
        }
    }

    IEnumerator DissolveCoroutine()
    {
        float targetValue = 1f;
        float currentValue = GetDissolveValue();
        while (Mathf.Abs(currentValue - targetValue) > 0.01f)
        {
            currentValue = Mathf.Lerp(currentValue, targetValue, dissolveSpeed * Time.deltaTime);
            SetDissolveValue(currentValue);
            yield return null;
        }
        SetDissolveValue(targetValue);
    }
}
