using UnityEngine;

// Pret's detect meter, 0 to Max. Starts at 0.
// One speed for both ways: SetDetecting(true) goes up Rate Per Second until Max,
// SetDetecting(false) goes down the same Rate Per Second back to 0.
public class PretDetectComponent : MonoBehaviour
{
    [SerializeField] [Min(0.01f)] private float _maxDetect = 100f;
    [Tooltip("How much it goes up per second while detecting, and down per second while not.")]
    [SerializeField] [Min(0f)] private float _ratePerSecond = 10f;

    [Header("Debug (shown in Play Mode)")]
    [SerializeField] private bool _isDetecting;
    [SerializeField] private float _detectPoint;

    public float DetectPoint => _detectPoint;
    public float MaxDetect => _maxDetect;
    public float RatePerSecond => _ratePerSecond;
    public float Normalized => _detectPoint / _maxDetect;   // 0..1, for a UI bar
    public bool IsFull => _detectPoint >= _maxDetect;
    public bool IsDetecting => _isDetecting;

    // true: goes up. false: goes down.
    public void SetDetecting(bool isOn)
    {
        _isDetecting = isOn;
    }

    // The speed for both ways. Negative counts as 0 (doesn't move).
    public void SetRatePerSecond(float ratePerSecond)
    {
        _ratePerSecond = Mathf.Max(0f, ratePerSecond);
    }

    // Jumps the meter straight to a value, kept between 0 and Max. SetDetectPoint(0) empties it.
    public void SetDetectPoint(float value)
    {
        _detectPoint = Mathf.Clamp(value, 0f, _maxDetect);
    }

    private void Update()
    {
        // Per second * deltaTime = how much for this frame. Up while detecting, down while not.
        float change = _ratePerSecond * Time.deltaTime;
        if (!_isDetecting) change = -change;

        _detectPoint = Mathf.Clamp(_detectPoint + change, 0f, _maxDetect);
    }
}
