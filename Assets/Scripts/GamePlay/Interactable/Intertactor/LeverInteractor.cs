using System.Collections;
using UnityEngine;

﻿namespace SDFcl.GamePlay.Interactable
{
    public class LeverInteractor : BaseInteractor
    {
        public bool IsActive { get; private set; }

        public override bool Interact(GameObject rootplayer)
        {
            if (!base.Interact(rootplayer)) return false; // ยิง OnInteract ในนี้
            if (IsActive)
            {
                CancelInteraction(rootplayer);
                return true;
            }

            IsActive = true;
            return true;
        }

        public override bool CancelInteraction(GameObject rootplayer)
        {
            if (!IsActive) return false;

            IsActive = false;
            return base.CancelInteraction(rootplayer); // ยิง OnEndInteract ในนี้
        }

        private void OnDisable()
        {
            // ถ้าถูกปิดตอนกำลัง interact อยู่ ให้แจ้งจบ เพื่อให้ตัวเสริมคืนค่า
            // (ต้องส่ง player เข้าไป ถ้าไม่มี ให้ส่ง null ได้ เพราะ listener ไม่ต้องใช้)
            if (IsActive)
            {
                IsActive = false;
                OnEndInteract?.Invoke(null);
            }
        }
    }
}
