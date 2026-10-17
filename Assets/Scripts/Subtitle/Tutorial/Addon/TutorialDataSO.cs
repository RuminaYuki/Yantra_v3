using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct TutorialPage
{
    [Tooltip("รูปประกอบ — เว้นว่างได้ ช่องรูปจะซ่อนเอง")]
    public Sprite Image;

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
}