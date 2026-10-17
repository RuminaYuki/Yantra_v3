using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[Serializable]
public struct TutorialPage
{
    [Tooltip("รูปประกอบ — เว้นว่างได้ ช่องรูปจะซ่อนเอง")]
    public Sprite Image;

    [Tooltip("คลิปสาธิต — ใส่แล้วเล่นแทนรูป วนซ้ำ ไม่มีเสียง (แนะนำ 3-6 วิ, 1280x720, MP4)\nเว้นว่าง = ใช้รูปตามปกติ")]
    public VideoClip Video;

    public string Title;

    [TextArea(3, 8)]
    public string Body;
}

// tutorial 1 ชุด — กด + ที่ Pages เพื่อเพิ่มหน้า
[CreateAssetMenu(fileName = "NewTutorial", menuName = "Yantra/Tutorial/Tutorial Data")]
public class TutorialDataSO : ScriptableObject
{
    [Tooltip("ปุ่มถัดไป/ปิด จะกดไม่ได้กี่วินาทีในแต่ละหน้า — บังคับให้ผู้เล่นได้อ่านก่อน")]
    [SerializeField, Min(0f)] private float _lockSeconds = 2f;

    [SerializeField] private TutorialPage[] _pages;

    public float LockSeconds => _lockSeconds;
    public IReadOnlyList<TutorialPage> Pages => _pages ?? Array.Empty<TutorialPage>();

#if UNITY_EDITOR
    // เตือนตั้งแต่ตอนพิมพ์ ถ้าใส่ {ชื่อ} ที่ไม่มีไอคอน — ไม่ต้องรอกด Play ไปเจอ
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= CheckIcons;
        UnityEditor.EditorApplication.delayCall += CheckIcons;
    }

    private void CheckIcons()
    {
        if (this == null || _pages == null)
            return;

        TMPro.TMP_SpriteAsset asset = TMPro.TMP_Settings.defaultSpriteAsset;

        for (int i = 0; i < _pages.Length; i++)
        {
            var missing = InputIconText.FindMissing(_pages[i].Title + " " + _pages[i].Body, asset);
            if (missing.Count == 0)
                continue;

            string names = "{" + string.Join("}, {", missing) + "}";
            Debug.LogWarning($"[{name}] หน้า {i + 1}: ไม่มีไอคอน {names} — เช็คตัวสะกด (ตัวเล็กตัวใหญ่ต้องตรง)", this);
        }
    }
#endif
}