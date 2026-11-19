using Unity.Cinemachine;
using UnityEngine;

// Created at runtime by PlayPairedAnimationAction; it does not need Inspector setup.
// This only moves the render camera, never the animated rig or the tracked Head target.
[DisallowMultipleComponent]
public class PairedAnimationViewModelCameraSync : MonoBehaviour
{
    [Tooltip("ระหว่าง Glory ให้กล้องหลักวาดแขนลงในฉาก แทนภาพที่แปะทับจอ\n" +
             "หัวผีจะบังนิ้วที่อ้อมไปด้านหลังได้ ไม่ดูเหมือนมือทะลุหน้า\n" +
             "ปิด = แบบเดิม (แขนอยู่หน้าทุกอย่างเสมอ)")]
    public bool renderArmsInWorld = true;

    private PairedAnimationActor _actor;
    private CinemachineBrain _brain;
    private CinemachineCore.BrainEvent _cameraEvent;
    private Camera _viewCamera;
    private Camera _outputCamera;
    private Transform _savedParent;
    private Vector3 _savedLocalPosition;
    private Quaternion _savedLocalRotation;
    private float _savedFieldOfView;
    private bool _outputMaskChanged;
    private int _viewModelBit;
    private bool _armsInWorld;            // true = เพิ่มเลเยอร์แขนให้กล้องหลัก / false = เอาออก (แบบเดิม)
    private ViewModelRenderer _viewModelRenderer;
    private bool _active;
    private bool _returning;
    private bool _entering;
    private float _entryFieldOfView;
    private float _returnFieldOfView;

    public bool Begin(PairedAnimationActor actor, CinemachineBrain brain, Camera viewCamera)
    {
        Camera output = brain != null ? brain.OutputCamera : null;
        if (actor == null || !actor.isActiveAndEnabled || brain == null || !brain.isActiveAndEnabled ||
            output == null || !output.isActiveAndEnabled || viewCamera == null ||
            !viewCamera.isActiveAndEnabled || output == viewCamera || output.orthographic ||
            viewCamera.orthographic || !isActiveAndEnabled || CinemachineCore.CameraUpdatedEvent == null)
            return false;

        if (_active)
        {
            if (_brain == brain && _viewCamera == viewCamera && _outputCamera == output)
            {
                // A new Glory may interrupt the return blend. Keep the original baseline.
                _actor = actor;
                _returning = false;
                _entering = true;
                _entryFieldOfView = viewCamera.fieldOfView;

                // ตอนกล้องเริ่มกลับ แขนถูกคืนไปแปะทับจอแล้ว — เข้า Glory ใหม่ต้องย้ายมาวาดในฉากอีกรอบ
                if (!_outputMaskChanged && !_armsInWorld)
                    ApplyArmMode(output, viewCamera);
                return true;
            }
            RestoreNow();
        }

        _actor = actor;
        _brain = brain;
        _viewCamera = viewCamera;
        _outputCamera = output;
        _savedParent = viewCamera.transform.parent;
        _savedLocalPosition = viewCamera.transform.localPosition;
        _savedLocalRotation = viewCamera.transform.localRotation;
        _savedFieldOfView = viewCamera.fieldOfView;
        _entryFieldOfView = viewCamera.fieldOfView;
        _entering = true;
        _returning = false;
        _active = true;

        ApplyArmMode(output, viewCamera);

        _cameraEvent = CinemachineCore.CameraUpdatedEvent;
        _cameraEvent.AddListener(HandleCameraUpdated);
        return true;
    }

    // Called after the action restores Glory priority. Wait for Cinemachine's next update.
    public void End()
    {
        if (!_active || _returning)
            return;

        _returning = true;
        _returnFieldOfView = _viewCamera != null ? _viewCamera.fieldOfView : _savedFieldOfView;

        // ท่าจบแล้ว คืนแขนไปแปะทับจอเลย ไม่รอกล้องเลื่อนกลับ
        // ระหว่างเลื่อนกลับ เลนส์กล้องหลักจะค่อยๆ กลับเป็นของเกมเพลย์ (near clip ไกลขึ้น) แขนในฉากอาจโดนตัดหาย
        if (_armsInWorld)
            RestoreArms();
    }

    // เลือกว่าระหว่าง Glory แขนจะถูกวาดแบบไหน แล้วตั้งเลเยอร์ของกล้องหลักให้ตรง
    private void ApplyArmMode(Camera output, Camera viewCamera)
    {
        int viewModelLayer = LayerMask.NameToLayer("ViewModel");
        if (viewModelLayer >= 0)
        {
            _viewModelBit = 1 << viewModelLayer;
            _viewModelRenderer = viewCamera.GetComponent<ViewModelRenderer>();

            if (renderArmsInWorld && _viewModelRenderer != null)
            {
                // วาดแขนในฉากด้วยกล้องหลัก แล้วซ่อนภาพที่แปะทับจอ — หัวผีจะบังนิ้วได้ตามจริง
                // ช่วงนี้กล้องแขนตามตำแหน่งและ FOV ของกล้องหลักอยู่แล้ว (HandleCameraUpdated) ภาพแขนเลยไม่กระโดด
                _armsInWorld = true;
                _outputMaskChanged = (output.cullingMask & _viewModelBit) == 0;
                if (_outputMaskChanged)
                    output.cullingMask |= _viewModelBit;
                _viewModelRenderer.SetOverlayVisible(false);
            }
            else
            {
                // Prevent the same arms from being drawn by both cameras during Glory.
                // Only touch the named ViewModel layer, and only if the arm camera renders it.
                _armsInWorld = false;
                _outputMaskChanged = (viewCamera.cullingMask & output.cullingMask & _viewModelBit) != 0;
                if (_outputMaskChanged)
                    output.cullingMask &= ~_viewModelBit;
            }
        }
    }

    private void RestoreArms()
    {
        if (_outputMaskChanged && _outputCamera != null)
        {
            if (_armsInWorld) _outputCamera.cullingMask &= ~_viewModelBit;
            else _outputCamera.cullingMask |= _viewModelBit;
        }
        _outputMaskChanged = false;

        if (_armsInWorld && _viewModelRenderer != null)
            _viewModelRenderer.SetOverlayVisible(true);
        _armsInWorld = false;
    }

    private void HandleCameraUpdated(CinemachineBrain brain)
    {
        if (!_active || brain != _brain)
            return;

        if (!CamerasReady())
        {
            RestoreNow();
            return;
        }

        // This event runs after the output camera's final blended pose and lens are applied.
        if (_returning && !brain.IsBlending)
        {
            RestoreNow();
            return;
        }

        _viewCamera.transform.SetPositionAndRotation(
            _outputCamera.transform.position, _outputCamera.transform.rotation);

        float blend = brain.IsBlending && brain.ActiveBlend != null
            ? Mathf.Clamp01(brain.ActiveBlend.BlendWeight) : 1f;
        if (_returning)
            _viewCamera.fieldOfView = Mathf.Lerp(_returnFieldOfView, _savedFieldOfView, blend);
        else if (_entering)
        {
            _viewCamera.fieldOfView = Mathf.Lerp(_entryFieldOfView, _outputCamera.fieldOfView, blend);
            if (blend >= 1f)
                _entering = false;
        }
        else
            _viewCamera.fieldOfView = _outputCamera.fieldOfView;
    }

    private void LateUpdate()
    {
        if (!_active)
            return;

        // A disabled/destroyed camera cannot send the event used for normal cleanup.
        if (!CamerasReady())
        {
            RestoreNow();
            return;
        }

        if (_actor == null || !_actor.isActiveAndEnabled || _actor.CurrentRequest.Animation == null)
            End();
    }

    private bool CamerasReady() =>
        _brain != null && _brain.isActiveAndEnabled && _viewCamera != null &&
        _viewCamera.isActiveAndEnabled && _outputCamera != null &&
        _outputCamera.isActiveAndEnabled && _brain.OutputCamera == _outputCamera;

    private void OnDisable() => RestoreNow();
    private void OnDestroy() => RestoreNow();

    private void RestoreNow()
    {
        if (_cameraEvent != null)
            _cameraEvent.RemoveListener(HandleCameraUpdated);
        _cameraEvent = null;

        if (_active)
        {
            if (_viewCamera != null)
            {
                // Do not undo a deliberate reparent by another system.
                if (_viewCamera.transform.parent == _savedParent)
                {
                    _viewCamera.transform.localPosition = _savedLocalPosition;
                    _viewCamera.transform.localRotation = _savedLocalRotation;
                }
                _viewCamera.fieldOfView = _savedFieldOfView;
            }
            RestoreArms();
        }

        _active = false;
        _returning = false;
        _entering = false;
        _outputMaskChanged = false;
        _armsInWorld = false;
        _viewModelRenderer = null;
        _actor = null;
        _brain = null;
        _viewCamera = null;
        _outputCamera = null;
        _savedParent = null;
    }
}