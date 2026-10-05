using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Put this on the RawImage that shows the 3D model in the inventory.
// A separate camera renders a copy of the item into a RenderTexture.
//
// Scene setup:
//   ItemPreviewStage (far away from the level, e.g. y = -1000)
//     Pivot        <- assign to "pivot"
//     PreviewCam   <- assign to "previewCamera" (culling mask is set from code)
//
// Controls: drag to rotate, scroll to zoom. Auto-rotates when idle.
[RequireComponent(typeof(RawImage))]
public class ItemPreview3D : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [Header("Reference")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform pivot;

    [Header("Layer")]
    [Tooltip("Layer index (0-31) for the preview model. Does not need a name in Tags & Layers; " +
             "pick one no other system uses. The preview camera renders only this layer.")]
    [Range(0, 31)]
    [SerializeField] private int previewLayer = 31;

    [Header("Rotation")]
    [SerializeField] private float autoRotateSpeed = 30f;   // degrees per second
    [SerializeField] private float dragSensitivity = 0.4f;  // degrees per pixel
    [SerializeField] private float resumeAutoRotateDelay = 1.5f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Framing")]
    [Tooltip("Bigger = more empty space around the model.")]
    [SerializeField] private float padding = 1.2f;
    [SerializeField] private float minZoom = 0.6f;
    [SerializeField] private float maxZoom = 2f;
    [SerializeField] private float zoomSpeed = 0.1f;

    private RawImage rawImage;
    private RenderTexture renderTexture;
    private GameObject stagingRoot;   // inactive parent, so prefab scripts never wake up
    private GameObject currentModel;

    private float yaw;
    private float pitch;
    private float zoom = 1f;
    private float fitDistance = 1f;
    private bool dragging;
    private float lastDragTime = -999f;
    private bool initialized;

    private void Awake() => EnsureInit();

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        rawImage = GetComponent<RawImage>();
        if (previewCamera != null)
        {
            previewCamera.cullingMask = 1 << previewLayer;
            previewCamera.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (previewCamera != null) previewCamera.enabled = currentModel != null;
    }

    private void OnDisable()
    {
        if (previewCamera != null) previewCamera.enabled = false;
        dragging = false;
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    // ---------- Public API ----------

    public void Show(ItemData item)
    {
        EnsureInit();
        Clear();

        GameObject source = item != null ? item.PreviewSource : null;
        if (source == null || pivot == null || previewCamera == null) return;

        if (stagingRoot == null)
        {
            stagingRoot = new GameObject("PreviewStaging");
            stagingRoot.SetActive(false);
            stagingRoot.transform.SetParent(pivot, false);
        }

        // Instantiate under an inactive parent: Awake/OnEnable of gameplay scripts never run.
        GameObject model = Instantiate(source, stagingRoot.transform);
        StripToVisuals(model);

        SetLayerRecursive(model, previewLayer);

        // Reset the turntable, then place the model with its configured starting rotation.
        yaw = 0f;
        pitch = 0f;
        zoom = 1f;
        pivot.rotation = Quaternion.identity;

        model.transform.SetParent(pivot, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(item.previewRotation);
        currentModel = model;

        FitToCamera();
        EnsureRenderTexture();
        rawImage.enabled = true;
        previewCamera.enabled = isActiveAndEnabled;
    }

    public void Clear()
    {
        EnsureInit();
        if (currentModel != null) Destroy(currentModel);
        currentModel = null;
        if (previewCamera != null) previewCamera.enabled = false;
        if (rawImage != null) rawImage.enabled = false;
    }

    // ---------- Update ----------

    private void LateUpdate()
    {
        if (currentModel == null) return;

        EnsureRenderTexture();

        // Unscaled time: still rotates if the game is paused while the inventory is open.
        bool idle = !dragging && Time.unscaledTime - lastDragTime > resumeAutoRotateDelay;
        if (idle) yaw += autoRotateSpeed * Time.unscaledDeltaTime;

        ApplyRotation();
        PlaceCamera();
    }

    private void ApplyRotation()
    {
        Vector3 camRight = previewCamera.transform.right;
        pivot.rotation = Quaternion.AngleAxis(pitch, camRight) * Quaternion.AngleAxis(yaw, Vector3.up);
    }

    private void PlaceCamera()
    {
        Transform cam = previewCamera.transform;
        cam.position = pivot.position - cam.forward * (fitDistance * zoom);
    }

    // Centers the model on the pivot and finds a camera distance that fits it.
    private void FitToCamera()
    {
        Renderer[] renderers = currentModel.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { fitDistance = 2f; PlaceCamera(); return; }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        // Move the model so its visual center sits exactly on the pivot.
        currentModel.transform.position -= b.center - pivot.position;

        float radius = Mathf.Max(b.extents.magnitude, 0.01f);
        float halfFov = previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfFovH = Mathf.Atan(Mathf.Tan(halfFov) * Mathf.Max(previewCamera.aspect, 0.01f));
        float limiting = Mathf.Min(halfFov, halfFovH);
        fitDistance = radius / Mathf.Sin(limiting) * padding;

        previewCamera.nearClipPlane = Mathf.Max(0.01f, (fitDistance * minZoom - radius) * 0.5f);
        previewCamera.farClipPlane = fitDistance * maxZoom + radius * 2f;
        PlaceCamera();
    }

    private void EnsureRenderTexture()
    {
        Rect r = rawImage.rectTransform.rect;
        Canvas canvas = rawImage.canvas;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        int w = Mathf.Max(64, Mathf.RoundToInt(r.width * scale));
        int h = Mathf.Max(64, Mathf.RoundToInt(r.height * scale));

        if (renderTexture != null && renderTexture.width == w && renderTexture.height == h) return;

        if (renderTexture != null)
        {
            previewCamera.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
        }

        renderTexture = new RenderTexture(w, h, 24) { name = "ItemPreviewRT", antiAliasing = 1 };
        renderTexture.Create();
        previewCamera.targetTexture = renderTexture;
        previewCamera.aspect = (float)w / h;
        rawImage.texture = renderTexture;
    }

    // ---------- Input ----------

    public void OnBeginDrag(PointerEventData e)
    {
        if (currentModel == null) return;
        dragging = true;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        yaw -= e.delta.x * dragSensitivity;
        pitch = Mathf.Clamp(pitch + e.delta.y * dragSensitivity, minPitch, maxPitch);
        lastDragTime = Time.unscaledTime;
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragging = false;
        lastDragTime = Time.unscaledTime;
    }

    public void OnScroll(PointerEventData e)
    {
        if (currentModel == null) return;
        zoom = Mathf.Clamp(zoom - e.scrollDelta.y * zoomSpeed, minZoom, maxZoom);
    }

    // ---------- Helpers ----------

    // Keeps only what is needed to draw the mesh. Removes scripts, physics,
    // audio, particles, lights, etc. Order matters because of component dependencies.
    private static void StripToVisuals(GameObject root)
    {
        Component[] all = root.GetComponentsInChildren<Component>(true);
        var others = new List<Component>();
        var extraRenderers = new List<Component>();
        var bodies = new List<Component>();

        // Pass 1: scripts, last added first (later scripts usually depend on earlier ones)
        for (int i = all.Length - 1; i >= 0; i--)
        {
            if (all[i] is MonoBehaviour) DestroyImmediate(all[i]);
        }

        foreach (Component c in all)
        {
            if (c == null) continue; // already destroyed
            if (c is Transform || c is MeshFilter || c is MeshRenderer ||
                c is SkinnedMeshRenderer || c is LODGroup) continue;

            if (c is Joint) DestroyImmediate(c);
            else if (c is Rigidbody) bodies.Add(c);
            else if (c is Renderer) extraRenderers.Add(c);
            else others.Add(c);
        }

        foreach (Component c in others) if (c != null) DestroyImmediate(c);
        foreach (Component c in extraRenderers) if (c != null) DestroyImmediate(c);
        foreach (Component c in bodies) if (c != null) DestroyImmediate(c);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }
}
