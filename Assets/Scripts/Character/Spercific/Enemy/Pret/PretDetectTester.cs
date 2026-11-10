using UnityEngine;
using UnityEngine.InputSystem;

// Test only: drives PretDetectComponent from the keyboard in Play Mode.
//   E = SetDetecting(true)
//   Q = SetDetecting(false)
//   R = SetDetectPoint(0)
//   F = SetDetectPoint(Max)
// Also on the component's ⋮ menu (right-click the header) for testing without keys.
public class PretDetectTester : MonoBehaviour
{
    [Tooltip("Empty = the PretDetectComponent on this object.")]
    [SerializeField] private PretDetectComponent _detect;
    [Tooltip("Writes the meter to the Console every second.")]
    [SerializeField] private bool _log = true;

    private float _nextLogTime;

    private void Awake()
    {
        if (_detect == null) _detect = GetComponent<PretDetectComponent>();
        if (_detect == null) Debug.LogError("PretDetectTester: no PretDetectComponent to test.", this);
    }

    private void Update()
    {
        if (_detect == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.eKey.wasPressedThisFrame) Enable();
            if (keyboard.qKey.wasPressedThisFrame) Disable();
            if (keyboard.rKey.wasPressedThisFrame) Empty();
            if (keyboard.fKey.wasPressedThisFrame) Fill();
        }

        if (_log && Time.time >= _nextLogTime)
        {
            _nextLogTime = Time.time + 1f;
            Debug.Log($"Detect {_detect.DetectPoint:0.0} / {_detect.MaxDetect} ({_detect.Normalized:P0}) " +
                      $"rate: {_detect.RatePerSecond}/s detecting: {_detect.IsDetecting} full: {_detect.IsFull}", this);
        }
    }

    [ContextMenu("Enable")]
    private void Enable()
    {
        _detect.SetDetecting(true);
        Debug.Log("SetDetecting(true)", this);
    }

    [ContextMenu("Disable")]
    private void Disable()
    {
        _detect.SetDetecting(false);
        Debug.Log("SetDetecting(false)", this);
    }

    [ContextMenu("Empty")]
    private void Empty()
    {
        _detect.SetDetectPoint(0f);
        Debug.Log("SetDetectPoint(0)", this);
    }

    [ContextMenu("Fill")]
    private void Fill()
    {
        _detect.SetDetectPoint(_detect.MaxDetect);
        Debug.Log($"SetDetectPoint({_detect.MaxDetect})", this);
    }
}
