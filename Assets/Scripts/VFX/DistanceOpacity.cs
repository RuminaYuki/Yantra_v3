using UnityEngine;

/// <summary>
/// Changes a Shader Graph opacity property based on the distance to the player.
///   distance <= startDistance  -> startOpacity
///   distance >= endDistance    -> endOpacity
///   in between                 -> blended (optionally shaped by blendCurve)
///
/// Distance mode can be turned off at runtime, and opacity can be set manually.
/// </summary>
public class DistanceOpacity : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("ON = opacity follows player distance. OFF = opacity stays at the manual value.")]
    [SerializeField] private bool distanceEnabled = true;

    [Header("Player")]
    [Tooltip("Leave empty to find the player by tag (useful when the player is spawned at runtime).")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Optional point to measure distance from. Uses this transform if empty.")]
    [SerializeField] private Transform distanceOrigin;

    [Header("Shader")]
    [Tooltip("Renderers that use the Shader Graph material. Uses renderers on this object and children if empty.")]
    [SerializeField] private Renderer[] targetRenderers;
    [Tooltip("Reference name of the float property in Shader Graph (not the display name).")]
    [SerializeField] private string opacityProperty = "_Opacity";

    [Header("Opacity")]
    [Range(0f, 1f)][SerializeField] private float startOpacity = 1f;
    [Range(0f, 1f)][SerializeField] private float endOpacity = 0f;

    [Header("Distance")]
    [Tooltip("Distance where opacity starts to change.")]
    [Min(0f)][SerializeField] private float startDistance = 2f;
    [Tooltip("Distance where opacity reaches the end value.")]
    [Min(0f)][SerializeField] private float endDistance = 10f;

    [Header("Blend")]
    [Tooltip("X = 0..1 progress between start and end distance, Y = 0..1 blend amount.")]
    [SerializeField] private AnimationCurve blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Opacity change per second. 0 = instant.")]
    [Min(0f)][SerializeField] private float smoothSpeed = 0f;

    private Material[] materials;
    private int propertyId;
    private float currentOpacity;
    private float manualOpacity;
    private bool manualInstant = true;
    private float nextFindTime;

    /// <summary>Opacity currently applied to the material.</summary>
    public float CurrentOpacity => currentOpacity;

    /// <summary>True if opacity is driven by player distance.</summary>
    public bool IsDistanceEnabled => distanceEnabled;

    private void Awake()
    {
        propertyId = Shader.PropertyToID(opacityProperty);

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>();

        CollectMaterials();

        currentOpacity = startOpacity;
        manualOpacity = startOpacity;
        ApplyOpacity(currentOpacity);
    }

    private void CollectMaterials()
    {
        var list = new System.Collections.Generic.List<Material>();

        foreach (var r in targetRenderers)
        {
            if (r == null) continue;

            // .materials creates per-object instances so other objects are not affected
            foreach (var mat in r.materials)
            {
                if (mat != null && mat.HasProperty(propertyId))
                    list.Add(mat);
            }
        }

        materials = list.ToArray();

        if (materials.Length == 0)
            Debug.LogWarning($"[DistanceOpacity] No material with property '{opacityProperty}' found on {name}.", this);
    }

    private void Update()
    {
        float targetOpacity;
        bool instant;

        if (distanceEnabled)
        {
            if (player == null)
            {
                TryFindPlayer();
                if (player == null) return;
            }

            targetOpacity = GetDistanceOpacity();
            instant = smoothSpeed <= 0f;
        }
        else
        {
            targetOpacity = manualOpacity;
            instant = manualInstant || smoothSpeed <= 0f;
        }

        float newOpacity = instant
            ? targetOpacity
            : Mathf.MoveTowards(currentOpacity, targetOpacity, smoothSpeed * Time.deltaTime);

        // Skip SetFloat when nothing changed
        if (!Mathf.Approximately(newOpacity, currentOpacity))
        {
            currentOpacity = newOpacity;
            ApplyOpacity(currentOpacity);
        }
    }

    private float GetDistanceOpacity()
    {
        Vector3 origin = distanceOrigin != null ? distanceOrigin.position : transform.position;
        float distance = Vector3.Distance(origin, player.position);

        float t = Mathf.InverseLerp(startDistance, endDistance, distance);
        return Mathf.Lerp(startOpacity, endOpacity, blendCurve.Evaluate(t));
    }

    private void TryFindPlayer()
    {
        // Search at most twice per second to avoid calling Find every frame
        if (Time.time < nextFindTime) return;
        nextFindTime = Time.time + 0.5f;

        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null) player = go.transform;
    }

    private void ApplyOpacity(float value)
    {
        if (materials == null) return;

        for (int i = 0; i < materials.Length; i++)
            materials[i].SetFloat(propertyId, value);
    }

    // ---------------- Public API ----------------

    /// <summary>
    /// Set opacity manually. This turns distance mode OFF.
    /// instant = false uses smoothSpeed to fade to the value.
    /// </summary>
    public void SetOpacity(float value, bool instant = true)
    {
        distanceEnabled = false;
        manualOpacity = Mathf.Clamp01(value);
        manualInstant = instant;

        if (instant)
        {
            currentOpacity = manualOpacity;
            ApplyOpacity(currentOpacity);
        }
    }

    /// <summary>Overload for UnityEvent / Button (instant only).</summary>
    public void SetOpacity(float value) => SetOpacity(value, true);

    /// <summary>Turn distance mode ON/OFF. When OFF, opacity stays at its current value.</summary>
    public void SetDistanceEnabled(bool enabled)
    {
        if (!enabled)
        {
            // Freeze at the current value
            manualOpacity = currentOpacity;
            manualInstant = true;
        }

        distanceEnabled = enabled;
    }

    public void EnableDistanceOpacity() => SetDistanceEnabled(true);
    public void DisableDistanceOpacity() => SetDistanceEnabled(false);

    /// <summary>Set the player at runtime (e.g. local player after network spawn).</summary>
    public void SetPlayer(Transform newPlayer) => player = newPlayer;

    // ---------------- Unity callbacks ----------------

    private void OnDestroy()
    {
        // Clean up material instances created by .materials
        if (materials == null) return;
        foreach (var mat in materials)
            if (mat != null) Destroy(mat);
    }

    private void OnValidate()
    {
        if (endDistance < startDistance) endDistance = startDistance;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = distanceOrigin != null ? distanceOrigin.position : transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, startDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, endDistance);
    }
}