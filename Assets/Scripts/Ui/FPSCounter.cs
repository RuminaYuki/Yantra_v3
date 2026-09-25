using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(TMP_Text))]
public class FPSCounter : MonoBehaviour
{
    [Tooltip("อัปเดตตัวเลขทุกกี่วินาที — ถี่ไปตัวเลขจะกระพริบจนอ่านไม่ทัน")]
    [SerializeField] private float _updateInterval = 0.5f;

    [Tooltip("โชว์ด้วยว่าเกมรันบน DX11 หรือ DX12")]
    [SerializeField] private bool _showGraphicsApi = true;

    private TMP_Text _text;
    private string _apiLabel = "";
    private int _frames;
    private float _elapsed;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();

        // API เปลี่ยนระหว่างเล่นไม่ได้ อ่านครั้งเดียวพอ
        if (_showGraphicsApi)
            _apiLabel = ApiName(SystemInfo.graphicsDeviceType) + " | ";
    }

    private void OnEnable()
    {
        _frames = 0;
        _elapsed = 0f;
        _text.text = _apiLabel + "-- FPS";
    }

    private void Update()
    {
        // unscaled — ตอน pause หรือ hit stop (timeScale = 0) ตัวเลขยังต้องเดิน
        _frames++;
        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed < _updateInterval) return;

        // นับเฟรมทั้งช่วงแล้วหารเวลา ได้ค่าเฉลี่ยจริง ไม่เด้งไปมาเหมือน 1/deltaTime
        int fps = Mathf.RoundToInt(_frames / _elapsed);
        _text.text = $"{_apiLabel}{fps} FPS";

        _frames = 0;
        _elapsed = 0f;
    }

    private static string ApiName(GraphicsDeviceType type)
    {
        switch (type)
        {
            case GraphicsDeviceType.Direct3D11: return "DX11";
            case GraphicsDeviceType.Direct3D12: return "DX12";
            default: return type.ToString();
        }
    }
}