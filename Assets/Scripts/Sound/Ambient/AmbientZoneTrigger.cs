using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class AmbientZoneTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("Tag ที่นับตอนเล่นเกมปกติ")]
    [SerializeField] private string[]    normalTags = { "Player" };

    [Tooltip("Tag ที่นับตอนคัตซีนกำลังเล่น" +
        "\nทีมคัตซีนใช้ Dummy เดินแทนผู้เล่น ส่วนผู้เล่นจริงถูกซ่อนไว้ใต้พื้น" +
        "\nตอนคัตซีนเราจึงต้องฟัง Dummy แทน และเมินเหตุการณ์ของผู้เล่นทั้งหมด")]
    [SerializeField] private string[] cutsceneTags = { "Dummy" };

    [Header("Transition Settings")]
    [SerializeField] private float fadeDuration = 2.0f;

    [Header("Outside Ambient Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float normalOutsideVolume = 1.0f;

    [Tooltip("ความดังตอนที่ตัวละคร 'เดินเข้ามาในโซนนี้' (เช่น เข้าบ้านเหลือ 0.25)")]
    [Range(0f, 1f)]
    [SerializeField] private float insideVolumeTarget = 0.25f;

    [Header("Indoor Ambient (Optional)")]
    [SerializeField] private SoundID indoorAmbientSound;

    private SFXHandle currentIndoorAmbient = SFXHandle.None;

    // จดบัญชีทุกคนที่อยู่ในโซน ไม่ว่าจะเป็น Player หรือ Dummy
    // แล้วค่อยไปตัดสินใจทีหลังว่ารอบนี้สนใจใคร
    private readonly Dictionary<Collider, int> overlapCount = new Dictionary<Collider, int>();

    // สถานะที่สั่งเสียงไปแล้วจริงๆ ใช้กันไม่ให้สั่งซ้ำ
    private bool isZoneActive;

    private void OnEnable()
    {
        // ต้องรู้ตอนคัตซีนเริ่ม/จบ เพราะ "คนที่เราสนใจ" เปลี่ยนไป
        // ต้องประเมินใหม่ทันที ไม่ใช่รอให้มีใครเดินเข้าออก
        CutsceneController.OnGlobalCutsceneStateChanged += OnCutsceneStateChanged;
    }

    private void OnDisable()
    {
        CutsceneController.OnGlobalCutsceneStateChanged -= OnCutsceneStateChanged;

        // PlayLoopSFXForever ไม่คืนลำโพงเข้าพูลเอง ถ้าไม่สั่งหยุด = เสียโควต้าพูลถาวร
        currentIndoorAmbient.Stop();
        currentIndoorAmbient = SFXHandle.None;

        overlapCount.Clear();
        isZoneActive = false;
    }

    private void OnCutsceneStateChanged(bool isPlaying)
    {
        // คัตซีนเริ่ม -> เลิกสนใจ Player หันไปดู Dummy แทน
        // คัตซีนจบ -> กลับมาดู Player
        Evaluate();
    }

    // ==========================================
    // จดบัญชี - จดทุกคนเสมอ ไม่สนว่าตอนนี้คัตซีนอยู่หรือเปล่า
    // ==========================================
    private void OnTriggerEnter(Collider other)
    {
        if (!IsTracked(other.tag)) return;

        if (overlapCount.TryGetValue(other, out int count))
            overlapCount[other] = count + 1;
        else
            overlapCount[other] = 1;

        Evaluate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsTracked(other.tag)) return;

        if (overlapCount.TryGetValue(other, out int count))
        {
            if (count <= 1) overlapCount.Remove(other);
            else overlapCount[other] = count - 1;
        }

        Evaluate();
    }

    // ==========================================
    // ตัดสินใจ - ดูเฉพาะคนที่เกี่ยวข้องในตอนนี้
    // ==========================================
    private void Evaluate()
    {
        bool shouldBeActive = HasRelevantOccupant();

        if (shouldBeActive == isZoneActive) return;   // สถานะไม่เปลี่ยน ไม่ต้องสั่งอะไร

        isZoneActive = shouldBeActive;

        if (shouldBeActive) EnterZone();
        else ExitZone();
    }

    private bool HasRelevantOccupant()
    {
        string[] activeTags = LevelAudioManager.IsCutsceneActive ? cutsceneTags : normalTags;

        List<Collider> destroyed = null;
        bool found = false;

        foreach (var pair in overlapCount)
        {
            if (pair.Key == null)
            {
                // ตัวละครถูก Destroy ไปแล้วแต่ยังค้างในบัญชี
                if (destroyed == null) destroyed = new List<Collider>();
                destroyed.Add(pair.Key);
                continue;
            }

            if (InArray(activeTags, pair.Key.tag)) found = true;
        }

        if (destroyed != null)
        {
            foreach (var key in destroyed) overlapCount.Remove(key);
        }

        return found;
    }

    private void EnterZone()
    {
        if (LevelAudioManager.Instance != null)
            LevelAudioManager.Instance.MuffleOutsideAmbients(insideVolumeTarget, fadeDuration);

        if (indoorAmbientSound == null || SoundManager.Instance == null) return;
        if (currentIndoorAmbient.IsValid) return;   // เล่นอยู่แล้ว ไม่ต้องซ้อน

        currentIndoorAmbient = SoundManager.Instance.PlayLoopSFXForever(indoorAmbientSound, transform.position);

        if (!currentIndoorAmbient.IsValid) return;

        currentIndoorAmbient.SetVolumeMultiplier(0f);
        currentIndoorAmbient.FadeToVolumeMultiplier(1f, fadeDuration);
    }

    private void ExitZone()
    {
        if (LevelAudioManager.Instance != null)
            LevelAudioManager.Instance.MuffleOutsideAmbients(normalOutsideVolume, fadeDuration);

        currentIndoorAmbient.FadeOutAndStop(fadeDuration);
        currentIndoorAmbient = SFXHandle.None;
    }

    // ==========================================
    private bool IsTracked(string tagToCheck)
    {
        return InArray(normalTags, tagToCheck) || InArray(cutsceneTags, tagToCheck);
    }

    private bool InArray(string[] array, string value)
    {
        if (array == null) return false;

        foreach (string t in array)
        {
            if (t == value) return true;
        }
        return false;
    }
}