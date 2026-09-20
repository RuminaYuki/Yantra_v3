// using UnityEngine;

// 1. เติม , ICutsceneListener
// public class CutsceneTutorial : MonoBehaviour, ICutsceneListener
// {
// private void OnEnable()
// {
// 2. สมัครรับข่าวตอนเกิด
// CutsceneController.OnGlobalCutsceneStateChanged += OnCutsceneStateChanged;
// }

// private void OnDisable()
// {
// 3. เลิกรับข่าวตอนตาย
//     CutsceneController.OnGlobalCutsceneStateChanged -= OnCutsceneStateChanged;
// }

// 4. รับคำสั่งตรงนี้เลย!
// public void OnCutsceneStateChanged(bool isPlaying)
// {
//    if (isPlaying)
//   {
// โค้ดสั่งผีหยุดเดิน
//    }
//    else
//    {
// โค้ดสั่งผีเดินต่อ
//    }
//  }
// }