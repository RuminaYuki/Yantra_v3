using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Yantra.UI
{
    /// <summary>
    /// รหัสประจำหน้าจอ. เพิ่มหน้าใหม่ = เพิ่ม enum ตัวนี้ตัวเดียว
    /// อย่าเรียงใหม่หรือลบของเก่าออก เพราะ Inspector เก็บเป็นตัวเลข
    /// </summary>
    public enum ScreenId
    {
        None = 0,
        MainMenu = 1,
        Settings = 2,
        Credits = 3,
        Pause = 10,
        Loading = 20,
        ConfirmDialog = 30,
    }

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private ScreenId _id = ScreenId.None;

        [Header("Behaviour")]
        [Tooltip("ปิดหน้าที่อยู่ข้างล่างใน stack ด้วยหรือไม่ (เมนูเต็มจอ = true, popup = false)")]
        [SerializeField] private bool _hidesScreensBelow = true;

        [Tooltip("กด Back / ESC แล้วปิดหน้านี้ได้หรือไม่")]
        [SerializeField] private bool _canCloseWithBack = true;

        [Header("Transition")]
        [SerializeField] private float _fadeInDuration = 0.20f;
        [SerializeField] private float _fadeOutDuration = 0.15f;

        [Header("Navigation")]
        [Tooltip("ปุ่มที่จะถูกเลือกอัตโนมัติตอนเปิดหน้านี้ (สำหรับ gamepad — จะใช้จริงในขั้นที่ 6)")]
        [SerializeField] private GameObject _firstSelected;

        private CanvasGroup _canvasGroup;
        private Coroutine _fadeRoutine;

        public ScreenId Id => _id;
        public bool HidesScreensBelow => _hidesScreensBelow;
        public bool CanCloseWithBack => _canCloseWithBack;
        public GameObject FirstSelected => _firstSelected;
        public bool IsOpen { get; private set; }

        /// <summary>ยิงตอนหน้านี้เปิดจบสมบูรณ์ (fade เสร็จแล้ว)</summary>
        public event Action Opened;
        /// <summary>ยิงตอนหน้านี้ปิดจบสมบูรณ์</summary>
        public event Action Closed;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_id == ScreenId.None)
                Debug.LogError($"[UIScreen] {name} ยังไม่ได้ตั้ง ScreenId", this);

            // เริ่มต้นปิดเสมอ ไม่ต้องไปกด disable เองใน Inspector ให้ลืม
            ApplyVisible(false);
            IsOpen = false;

            UIManager.Register(this);
        }

        protected virtual void OnDestroy()
        {
            UIManager.Unregister(this);
        }

        // ---------- เรียกโดย UIManager เท่านั้น ----------

        internal void Open(bool instant)
        {
            if (IsOpen) return;
            IsOpen = true;

            gameObject.SetActive(true);
            OnOpening();

            StopFade();
            if (instant || _fadeInDuration <= 0f)
            {
                ApplyVisible(true);
                FinishOpen();
            }
            else
            {
                _fadeRoutine = StartCoroutine(FadeRoutine(1f, _fadeInDuration, FinishOpen));
            }
        }

        internal void Close(bool instant)
        {
            if (!IsOpen) return;
            IsOpen = false;

            OnClosing();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            StopFade();
            if (instant || _fadeOutDuration <= 0f)
            {
                ApplyVisible(false);
                FinishClose();
            }
            else
            {
                _fadeRoutine = StartCoroutine(FadeRoutine(0f, _fadeOutDuration, () =>
                {
                    ApplyVisible(false);
                    FinishClose();
                }));
            }
        }

        /// <summary>ซ่อนแบบไม่ pop ออกจาก stack (ตอนมีหน้าใหม่ทับข้างบน)</summary>
        internal void SetCovered(bool covered)
        {
            if (!IsOpen) return;
            _canvasGroup.interactable = !covered;
            _canvasGroup.blocksRaycasts = !covered;
            if (covered) _canvasGroup.alpha = 0f;
            else if (_fadeRoutine == null) _canvasGroup.alpha = 1f;
        }

        // ---------- override ได้ในคลาสลูก ----------

        /// <summary>ก่อน fade in — ใส่ค่าเริ่มต้น, โหลดข้อมูล, subscribe event</summary>
        protected virtual void OnOpening() { }

        /// <summary>ก่อน fade out — เซฟค่า, unsubscribe</summary>
        protected virtual void OnClosing() { }

        /// <summary>กด Back / ESC ตอนหน้านี้อยู่บนสุด. return true = จัดการเองแล้ว ไม่ต้องปิด</summary>
        public virtual bool HandleBack() => false;

        // ---------- ภายใน ----------

        private void FinishOpen()
        {
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            SelectFirst();
            Opened?.Invoke();
        }

        private void FinishClose()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void SelectFirst()
        {
            if (_firstSelected == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_firstSelected);
        }

        private void ApplyVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
            if (!visible) gameObject.SetActive(false);
        }

        private void StopFade()
        {
            if (_fadeRoutine == null) return;
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        // unscaledDeltaTime สำคัญมาก — ขั้นที่ 4 จะมี Time.timeScale = 0
        private IEnumerator FadeRoutine(float target, float duration, Action onComplete)
        {
            float start = _canvasGroup.alpha;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }

            _canvasGroup.alpha = target;
            _fadeRoutine = null;
            onComplete?.Invoke();
        }
    }
}