using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ShootableDoor : MonoBehaviour
{
    [Serializable]
    public class RendererMaterialPair
    {
        public Coroutine dissolveCoroutine;
        public MeshRenderer renderer;
        public Material material;
    }

    [Header("Door Settings")]
    [SerializeField] FlagSO doorFlag;

    [Header("Dissolve Settings")]
    [SerializeField] private MeshRenderer[] meshRenderers;
    [SerializeField] private string opacityProperty = "_DissolveAmount";

    [SerializeField] bool smoothDissolve = true;
    [SerializeField] float dissolveSpeed = 1f;

    private List<RendererMaterialPair> rendererMaterialPairs = new List<RendererMaterialPair>();

    public void OnShot()
    {
        //doorObject.SetActive(false);
        Collider doorCollider = GetComponent<Collider>();
        if (doorCollider != null)
        {
            doorCollider.enabled = false;
        }

        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            if (meshRenderer.TryGetComponent<Renderer>(out Renderer renderer))
            {
                List<Material> materials = new List<Material>();
                materials.AddRange(renderer.materials);
                foreach (Material material in materials)
                {
                    if (material.HasProperty(opacityProperty))
                    {
                        RendererMaterialPair pair = new RendererMaterialPair
                        {
                            renderer = meshRenderer,
                            material = material
                        };
                        rendererMaterialPairs.Add(pair);
                    }
                }
            }
        }

        ChangeDissolve();
    }

    public FlagSO GetDoorFlag()
    {
        return doorFlag;
    }

    private void ChangeDissolve()
    {
        foreach (RendererMaterialPair pair in rendererMaterialPairs)
        {
            if (pair.dissolveCoroutine != null)
            {
                StopCoroutine(pair.dissolveCoroutine);
            }
            if (smoothDissolve)
            {
                pair.dissolveCoroutine = StartCoroutine(DissolveCoroutine(pair));
            }
        }
    }

    private IEnumerator DissolveCoroutine(RendererMaterialPair pair)
    {
        float targetValue = 1f;
        float currentValue = pair.material.GetFloat(opacityProperty);
        while (Mathf.Abs(currentValue - targetValue) > 0.01f)
        {
            currentValue = Mathf.Lerp(currentValue, targetValue, dissolveSpeed * Time.deltaTime);
            pair.material.SetFloat(opacityProperty, currentValue);
            yield return null;
        }
        pair.material.SetFloat(opacityProperty, targetValue);
    }

    [ContextMenu("Dissolve Door")]
    private void DissolveDoor()
    {
        OnShot();
    }
}
