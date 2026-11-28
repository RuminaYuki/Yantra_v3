using System;
using Unity.Cinemachine;
using UnityEngine;

public class CameraEffectsController : CinemachineExtension
{
    [Serializable]
    private class MotionProfile
    {
        [Tooltip("ตำแหน่งสั่นสูงสุดในแกนกล้อง")]
        public Vector3 PositionAmplitude;

        [Tooltip("มุมหมุนสูงสุด")]
        public Vector3 RotationAmplitude;

        [Min(0.01f)]
        public float Frequency = 1f;

        [Tooltip("ช่วงเวลาสุ่มตำแหน่งใหม่เป็นจังหวะ; 0 คือเคลื่อนต่อเนื่อง")]
        [Min(0f)]
        public float StepInterval;
    }

    [Header("เวียนหัว")]
    [SerializeField] private MotionProfile _dizziness = new MotionProfile
    {
        PositionAmplitude = new Vector3(0.012f, 0.01f, 0f),
        RotationAmplitude = new Vector3(1.2f, 0.8f, 1.8f),
        Frequency = 0.7f
    };
    [SerializeField, Range(0f, 1f)] private float _defaultDizziness = 0.7f;

    [Header("กล้องถือถ่าย")]
    [SerializeField] private MotionProfile _handheld = new MotionProfile
    {
        PositionAmplitude = new Vector3(0.006f, 0.005f, 0f),
        RotationAmplitude = new Vector3(0.35f, 0.3f, 0.55f),
        Frequency = 1.1f,
        StepInterval = 0.08f
    };
    [SerializeField, Range(0f, 1f)] private float _defaultHandheld = 1f;
    [SerializeField, Min(0.01f)] private float _blendSpeed = 2f;

    [Header("แรงกระแทก")]
    [SerializeField, Min(0f)] private float _impactPosition = 0.035f;
    [SerializeField, Min(0f)] private float _impactRotation = 2.5f;
    [SerializeField, Min(0.01f)] private float _impactDuration = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _defaultImpactStrength = 0.7f;

    private float _seed;
    private float _motionTime;
    private float _dizzinessTarget;
    private float _dizzinessWeight;
    private float _handheldTarget;
    private float _handheldWeight;
    private float _impactStrength;
    private float _impactDurationLeft;
    private Vector3 _impactPositionDirection;
    private Vector3 _impactRotationDirection;
    private Vector3 _positionOffset;
    private Vector3 _rotationOffset;

    protected override void Awake()
    {
        base.Awake();
        _seed = UnityEngine.Random.value * 100f;
    }

    private void OnDisable()
    {
        _dizzinessTarget = 0f;
        _dizzinessWeight = 0f;
        _handheldTarget = 0f;
        _handheldWeight = 0f;
        _impactDurationLeft = 0f;
        _positionOffset = Vector3.zero;
        _rotationOffset = Vector3.zero;
    }

    public void SetDizziness(float intensity)
    {
        _dizzinessTarget = Mathf.Clamp01(intensity);
    }

    public void EnableDizziness()
    {
        SetDizziness(_defaultDizziness);
    }

    public void DisableDizziness()
    {
        SetDizziness(0f);
    }

    public void SetHandheld(float intensity)
    {
        _handheldTarget = Mathf.Clamp01(intensity);
    }

    public void EnableHandheld()
    {
        SetHandheld(_defaultHandheld);
    }

    public void DisableHandheld()
    {
        SetHandheld(0f);
    }

    public void TriggerImpact()
    {
        TriggerImpact(_defaultImpactStrength);
    }

    public void TriggerImpact(float strength)
    {
        if (strength <= 0f)
            return;

        _impactStrength = Mathf.Clamp01(strength);
        _impactDurationLeft = _impactDuration;

        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        _impactPositionDirection = new Vector3(direction.x, direction.y, 0f);
        _impactRotationDirection = new Vector3(-direction.y, direction.x, UnityEngine.Random.Range(-1f, 1f));
    }

    public void ClearEffects()
    {
        DisableDizziness();
        DisableHandheld();
        _impactDurationLeft = 0f;
        _positionOffset = Vector3.zero;
        _rotationOffset = Vector3.zero;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        _motionTime += deltaTime;
        _dizzinessWeight = Mathf.MoveTowards(_dizzinessWeight, _dizzinessTarget, _blendSpeed * deltaTime);
        _handheldWeight = Mathf.MoveTowards(_handheldWeight, _handheldTarget, _blendSpeed * deltaTime);

        Vector3 position = Sample(_dizziness, _motionTime, 0f, out Vector3 dizzinessRotation) * _dizzinessWeight;
        Vector3 handheldPosition = Sample(_handheld, _motionTime, 37f, out Vector3 handheldRotation) * _handheldWeight;

        Vector3 impactPosition = Vector3.zero;
        Vector3 impactRotation = Vector3.zero;
        if (_impactDurationLeft > 0f)
        {
            float fade = Mathf.Clamp01(_impactDurationLeft / _impactDuration);
            impactPosition = _impactPositionDirection * (_impactPosition * _impactStrength * fade);
            impactRotation = _impactRotationDirection * (_impactRotation * _impactStrength * fade);
            _impactDurationLeft = Mathf.Max(0f, _impactDurationLeft - Time.unscaledDeltaTime);
        }

        _positionOffset = position + handheldPosition + impactPosition;
        _rotationOffset = dizzinessRotation * _dizzinessWeight
                          + handheldRotation * _handheldWeight
                          + impactRotation;
    }

    private Vector3 Sample(MotionProfile profile, float time, float seedOffset, out Vector3 rotation)
    {
        if (profile == null)
        {
            rotation = Vector3.zero;
            return Vector3.zero;
        }

        if (profile.StepInterval > 0f)
            time = Mathf.Floor(time / profile.StepInterval) * profile.StepInterval;

        float sampleTime = time * profile.Frequency;
        Vector3 noise = new Vector3(
            Noise(_seed + seedOffset, sampleTime),
            Noise(_seed + seedOffset + 17f, sampleTime),
            Noise(_seed + seedOffset + 31f, sampleTime));

        rotation = Vector3.Scale(noise, profile.RotationAmplitude);
        return Vector3.Scale(noise, profile.PositionAmplitude);
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize)
            return;

        state.PositionCorrection += state.GetCorrectedOrientation() * _positionOffset;
        state.OrientationCorrection *= Quaternion.Euler(_rotationOffset);
    }

    private static float Noise(float seed, float time)
    {
        return Mathf.PerlinNoise(seed, time) * 2f - 1f;
    }
}
