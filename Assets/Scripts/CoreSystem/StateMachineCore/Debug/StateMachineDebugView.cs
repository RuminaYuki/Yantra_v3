using System.Collections.Generic;
using UnityEngine;

public class StateMachineDebugView : MonoBehaviour
{
    private enum ScreenCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [SerializeField] private StateMachineController _controller;
    [SerializeField] private bool _showDebug = true;
    [SerializeField, Min(1)] private int _historySize = 10;

    [Header("Layout")]
    [SerializeField] private ScreenCorner _screenCorner = ScreenCorner.TopLeft;
    [SerializeField] private Vector2 _margin = new(20f, 20f);
    [SerializeField] private Vector2 _offset;
    [SerializeField, Min(100f)] private float _width = 500f;
    [SerializeField, Min(120f)] private float _maxHeight = 400f;
    [SerializeField, Min(16f)] private float _lineHeight = 24f;

    private readonly List<string> _history = new();
    private string _currentMainStateName = "None";
    private string _currentChildStateName = "None";
    private Vector2 _scrollPosition;

    private void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<StateMachineController>();
    }

    private void OnEnable()
    {
        if (_controller != null)
        {
            _controller.MainStateChanged += HandleMainStateChanged;
            _controller.ChildStateChanged += HandleChildStateChanged;
        }
    }

    private void OnDisable()
    {
        if (_controller != null)
        {
            _controller.MainStateChanged -= HandleMainStateChanged;
            _controller.ChildStateChanged -= HandleChildStateChanged;
        }
    }

    private void HandleMainStateChanged(
        string previousStateName,
        string currentStateName)
    {
        _currentMainStateName = currentStateName;
        AddHistory($"[Main] {previousStateName} -> {currentStateName}");
    }

    private void HandleChildStateChanged(
        string previousStateName,
        string currentStateName)
    {
        _currentChildStateName = currentStateName;
        AddHistory($"[Child] {previousStateName} -> {currentStateName}");
    }

    private void AddHistory(string message)
    {
        _history.Add($"{Time.time:F2}s  {message}");

        while (_history.Count > _historySize)
            _history.RemoveAt(0);
    }

    private void OnGUI()
    {
        if (!_showDebug || _controller == null)
            return;

        float historyContentHeight = Mathf.Max(
            _lineHeight,
            _history.Count * _lineHeight);

        float availableScreenHeight = Mathf.Max(
            120f,
            Screen.height - (_margin.y * 2f));

        float height = Mathf.Min(
            102f + historyContentHeight,
            _maxHeight,
            availableScreenHeight);

        Vector2 position = GetPanelPosition(height);
        float x = position.x;
        float y = position.y;

        GUI.Box(new Rect(x, y, _width, height), "State Machine Debug");

        GUI.Label(
            new Rect(x + 10f, y + 25f, _width - 20f, _lineHeight),
            $"Main: {_currentMainStateName}");

        GUI.Label(
            new Rect(x + 10f, y + 47f, _width - 20f, _lineHeight),
            $"Child: {_currentChildStateName}");

        GUI.Label(
            new Rect(x + 10f, y + 69f, _width - 20f, _lineHeight),
            "History:");

        Rect historyViewRect = new(
            x + 10f,
            y + 91f,
            _width - 20f,
            height - 101f);

        Rect historyContentRect = new(
            0f,
            0f,
            _width - 40f,
            historyContentHeight);

        _scrollPosition = GUI.BeginScrollView(
            historyViewRect,
            _scrollPosition,
            historyContentRect);

        for (int i = 0; i < _history.Count; i++)
        {
            GUI.Label(
                new Rect(
                    5f,
                    i * _lineHeight,
                    _width - 50f,
                    _lineHeight),
                _history[i]);
        }

        GUI.EndScrollView();
    }

    private Vector2 GetPanelPosition(float height)
    {
        bool alignRight =
            _screenCorner == ScreenCorner.TopRight ||
            _screenCorner == ScreenCorner.BottomRight;

        bool alignBottom =
            _screenCorner == ScreenCorner.BottomLeft ||
            _screenCorner == ScreenCorner.BottomRight;

        float x = alignRight
            ? Screen.width - _width - _margin.x
            : _margin.x;

        float y = alignBottom
            ? Screen.height - height - _margin.y
            : _margin.y;

        return new Vector2(x, y) + _offset;
    }
}
