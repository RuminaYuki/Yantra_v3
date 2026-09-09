using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yantra.UI
{
    /// <summary>
    /// จัดการ stack ของหน้าจอ UI
    /// ตัวมันเองไม่รู้จักหน้าจอไหนเลย — UIScreen เป็นฝ่ายมาลงทะเบียนเอง
    /// วางไว้บน GameObject ที่อยู่ใน scene ตลอด (UIRoot)
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private bool _persistAcrossScenes = true;
        [SerializeField] private ScreenId _openOnStart = ScreenId.None;

        // registry เป็น static เพราะ UIScreen.Awake อาจวิ่งก่อน UIManager.Awake
        private static readonly Dictionary<ScreenId, UIScreen> _registry = new();
        private readonly List<UIScreen> _stack = new();

        /// <summary>ยิงทุกครั้งที่ stack เปลี่ยน — ให้ระบบอื่น (input, audio) ฟังได้โดยไม่ผูกกัน</summary>
        public event Action<ScreenId> StackChanged;

        public ScreenId Current => _stack.Count > 0 ? _stack[^1].Id : ScreenId.None;
        public bool HasAnyOpen => _stack.Count > 0;
        public int Depth => _stack.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (_persistAcrossScenes) DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_openOnStart != ScreenId.None) Open(_openOnStart, instant: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Registry ----------

        internal static void Register(UIScreen screen)
        {
            if (screen == null || screen.Id == ScreenId.None) return;

            if (_registry.TryGetValue(screen.Id, out var existing) && existing != null && existing != screen)
            {
                Debug.LogWarning($"[UIManager] มี {screen.Id} ซ้ำกัน 2 อัน: {existing.name} / {screen.name}", screen);
                return;
            }
            _registry[screen.Id] = screen;
        }

        internal static void Unregister(UIScreen screen)
        {
            if (screen == null) return;
            if (_registry.TryGetValue(screen.Id, out var found) && found == screen)
                _registry.Remove(screen.Id);
        }

        public static bool TryGet<T>(ScreenId id, out T screen) where T : UIScreen
        {
            screen = null;
            if (_registry.TryGetValue(id, out var s) && s is T typed)
            {
                screen = typed;
                return true;
            }
            return false;
        }

        // ---------- API หลัก ----------

        /// <summary>เปิดหน้าใหม่ทับบน stack</summary>
        public void Open(ScreenId id, bool instant = false)
        {
            if (id == ScreenId.None) return;

            if (!_registry.TryGetValue(id, out var screen) || screen == null)
            {
                Debug.LogError($"[UIManager] ไม่พบหน้าจอ {id} — มันอยู่ใน scene และ active ตอน Awake หรือเปล่า");
                return;
            }

            if (_stack.Contains(screen))
            {
                Debug.LogWarning($"[UIManager] {id} เปิดอยู่แล้ว");
                return;
            }

            // ซ่อนหน้าที่อยู่ข้างล่างถ้าหน้าใหม่เป็นเมนูเต็มจอ
            if (screen.HidesScreensBelow && _stack.Count > 0)
                _stack[^1].SetCovered(true);
            else if (_stack.Count > 0)
                _stack[^1].SetCovered(true); // popup ก็ยังต้องบล็อกการคลิกทะลุ

            _stack.Add(screen);
            SortStackOrder();
            screen.Open(instant);
            StackChanged?.Invoke(Current);
        }

        /// <summary>ปิดหน้าบนสุด แล้วคืนหน้าที่อยู่ข้างล่าง</summary>
        public void Back(bool instant = false)
        {
            if (_stack.Count == 0) return;

            var top = _stack[^1];

            // ให้หน้าจอมีสิทธิ์ดักก่อน (เช่น ปิด dropdown ที่กางอยู่)
            if (top.HandleBack()) return;
            if (!top.CanCloseWithBack) return;

            _stack.RemoveAt(_stack.Count - 1);
            top.Close(instant);

            if (_stack.Count > 0) _stack[^1].SetCovered(false);
            StackChanged?.Invoke(Current);
        }

        /// <summary>ปิดหน้าที่ระบุไม่ว่ามันอยู่ตรงไหนใน stack</summary>
        public void Close(ScreenId id, bool instant = false)
        {
            int index = _stack.FindIndex(s => s.Id == id);
            if (index < 0) return;

            var screen = _stack[index];
            _stack.RemoveAt(index);
            screen.Close(instant);

            if (_stack.Count > 0) _stack[^1].SetCovered(false);
            StackChanged?.Invoke(Current);
        }

        /// <summary>ล้าง stack ทั้งหมด — ใช้ตอนเปลี่ยน scene</summary>
        public void CloseAll(bool instant = true)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
                _stack[i].Close(instant);

            _stack.Clear();
            StackChanged?.Invoke(ScreenId.None);
        }

        /// <summary>ปิดทุกหน้ายกเว้นอันที่ระบุ — ใช้ตอนโหลด scene</summary>
        public void CloseAllExcept(ScreenId keep, bool instant = true)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].Id == keep) continue;
                _stack[i].Close(instant);
                _stack.RemoveAt(i);
            }
            SortStackOrder();
            if (_stack.Count > 0) _stack[^1].SetCovered(false);
            StackChanged?.Invoke(Current);
        }

        /// <summary>ปิดทุกหน้าแล้วเปิดหน้าเดียวขึ้นมาเป็นราก (เช่น กลับ Main Menu)</summary>
        public void SwitchTo(ScreenId id, bool instant = false)
        {
            CloseAll(instant: true);
            Open(id, instant);
        }

        public bool IsOpen(ScreenId id) => _stack.Exists(s => s.Id == id);

        // ---------- ภายใน ----------

        // หน้าที่อยู่บน stack สูงกว่า ต้องวาดทับ — ใช้ sibling order แทนการยุ่งกับ Canvas sortingOrder
        private void SortStackOrder()
        {
            for (int i = 0; i < _stack.Count; i++)
                _stack[i].transform.SetSiblingIndex(i);
        }
    }
}