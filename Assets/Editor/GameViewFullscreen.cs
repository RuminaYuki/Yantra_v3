// Place this file inside an Editor folder (e.g. Assets/Editor/).
// F11 toggles a borderless fullscreen Game view (no toolbar, no window frame).
// The fullscreen view closes automatically when exiting Play Mode.
// If it ever gets stuck: Window > Game View Fullscreen - Force Close.

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

[InitializeOnLoad]
public static class GameViewFullscreen
{
    static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");

    // The window title is serialized with the window, so it survives domain reloads.
    // We use it as a marker to find our fullscreen view again (a static field does not survive).
    const string MarkerTitle = "Game (Fullscreen)";

    // Set to true to go fullscreen automatically every time you press Play
    const bool AutoFullscreenOnPlay = false;

    static double lastToggleTime;
    static bool f11WasDown;

    static GameViewFullscreen()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

#if ENABLE_INPUT_SYSTEM
        // Domain reload is disabled on Play, so remove first to avoid subscribing twice
        InputSystem.onAfterUpdate -= OnInputUpdate;
        InputSystem.onAfterUpdate += OnInputUpdate;
#else
        EditorApplication.update += PollF11InPlayMode;
#endif
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                if (AutoFullscreenOnPlay) Open();
                break;

            // Close on both events: ExitingPlayMode can run before a domain reload,
            // EnteredEditMode is the safety net after it.
            case PlayModeStateChange.ExitingPlayMode:
            case PlayModeStateChange.EnteredEditMode:
                CloseAll();
                break;
        }
    }

    [MenuItem("Window/Game View Fullscreen _F11")]
    static void Toggle()
    {
        // Prevent a double toggle when both the menu shortcut and the play-mode poll fire
        double now = EditorApplication.timeSinceStartup;
        if (now - lastToggleTime < 0.25) return;
        lastToggleTime = now;

        if (FindFullscreenViews().Length > 0) CloseAll();
        else Open();
    }

    [MenuItem("Window/Game View Fullscreen - Force Close")]
    static void ForceClose() => CloseAll();

    // In Play Mode the focused Game view swallows keyboard input, so the editor
    // shortcut (_F11) never reaches the menu item. Read F11 from the game's input instead.
#if ENABLE_INPUT_SYSTEM
    // The Input System keeps separate state for the editor and for the game.
    // Polling from EditorApplication.update reads the editor state, which never sees keys
    // while the Game view has focus. Reading right after a game input update fixes that.
    static void OnInputUpdate()
    {
        if (!EditorApplication.isPlaying)
        {
            f11WasDown = false;
            return;
        }

        var updateType = InputState.currentUpdateType;
        if (updateType == InputUpdateType.Editor || updateType == InputUpdateType.BeforeRender)
            return;

        var kb = Keyboard.current;
        bool down = kb != null && kb.f11Key.isPressed;

        // Opening a window inside the input update is unsafe — do it on the next editor tick
        if (down && !f11WasDown) EditorApplication.delayCall += Toggle;
        f11WasDown = down;
    }
#else
    static void PollF11InPlayMode()
    {
        if (!EditorApplication.isPlaying)
        {
            f11WasDown = false;
            return;
        }

        bool down = Input.GetKey(KeyCode.F11);
        if (down && !f11WasDown) Toggle();
        f11WasDown = down;
    }
#endif

    static void Open()
    {
        if (GameViewType == null || FindFullscreenViews().Length > 0) return;

        var view = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
        view.titleContent = new GUIContent(MarkerTitle);

        // Hide the Game view toolbar (internal property)
        var showToolbar = GameViewType.GetProperty("showToolbar",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        showToolbar?.SetValue(view, false);

        var rect = new Rect(Vector2.zero, ScreenSizeInPoints());

        view.ShowPopup(); // borderless window
        view.minSize = rect.size;
        view.maxSize = rect.size;
        view.position = rect;
        view.Focus();
    }

    static void CloseAll()
    {
        foreach (var view in FindFullscreenViews())
        {
            if (view != null) view.Close();
        }
    }

    // Finds our fullscreen view(s) by marker title. Also catches "orphan" views created
    // by the old version of this script: undocked Game views that cover the whole screen.
    static EditorWindow[] FindFullscreenViews()
    {
        if (GameViewType == null) return Array.Empty<EditorWindow>();

        Vector2 screen = ScreenSizeInPoints();
        return Resources.FindObjectsOfTypeAll(GameViewType)
            .OfType<EditorWindow>()
            .Where(w => w != null &&
                        (w.titleContent.text == MarkerTitle ||
                         (!w.docked && SameSize(w.position.size, screen))))
            .ToArray();
    }

    // Main display size, converted from pixels to points so display scaling (125%, 150%...) works
    static Vector2 ScreenSizeInPoints()
    {
        Resolution res = Screen.currentResolution;
        float ppp = EditorGUIUtility.pixelsPerPoint;
        return new Vector2(res.width / ppp, res.height / ppp);
    }

    static bool SameSize(Vector2 a, Vector2 b) =>
        Mathf.Abs(a.x - b.x) < 2f && Mathf.Abs(a.y - b.y) < 2f;
}