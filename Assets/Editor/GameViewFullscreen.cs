// วางไฟล์นี้ไว้ในโฟลเดอร์ Assets/Editor/ (ต้องอยู่ในโฟลเดอร์ชื่อ Editor)
// กด F11 เพื่อเปิด/ปิด Game view แบบเต็มจอ (ไม่มี toolbar, ไม่มีขอบหน้าต่าง)
// ปิดอัตโนมัติเมื่อออกจาก Play Mode

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GameViewFullscreen
{
    static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
    static EditorWindow fullscreenView;

    // ตั้งเป็น true ถ้าอยากให้เต็มจออัตโนมัติทุกครั้งที่กด Play
    const bool AutoFullscreenOnPlay = false;

    static GameViewFullscreen()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && AutoFullscreenOnPlay)
                Open();
            else if (state == PlayModeStateChange.ExitingPlayMode)
                Close();
        };
    }

    [MenuItem("Window/Game View Fullscreen _F11")]
    static void Toggle()
    {
        if (fullscreenView != null) Close();
        else Open();
    }

    static void Open()
    {
        if (fullscreenView != null || GameViewType == null) return;

        fullscreenView = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);

        // ซ่อนแถบ toolbar ด้านบนของ Game view (property แบบ internal)
        var showToolbar = GameViewType.GetProperty("showToolbar",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        showToolbar?.SetValue(fullscreenView, false);

        // ขนาดจอหลัก แปลงจาก pixel เป็น point เพื่อรองรับจอที่ตั้ง Scale (125%, 150% ฯลฯ)
        Resolution res = Screen.currentResolution;
        float ppp = EditorGUIUtility.pixelsPerPoint;
        var rect = new Rect(0, 0, res.width / ppp, res.height / ppp);

        fullscreenView.ShowPopup();        // หน้าต่างไม่มีขอบ
        fullscreenView.minSize = rect.size;
        fullscreenView.maxSize = rect.size;
        fullscreenView.position = rect;
        fullscreenView.Focus();
    }

    static void Close()
    {
        if (fullscreenView == null) return;
        fullscreenView.Close();
        fullscreenView = null;
    }
}
