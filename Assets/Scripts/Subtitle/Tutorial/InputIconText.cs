using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// แปลง {E} {LMB} ในข้อความเป็นรูปไอคอนปุ่ม (TMP Sprite Asset)
// คนรวมซีนพิมพ์แค่ {ชื่อไอคอน} ไม่ต้องจำแท็ก <sprite name="..."> เอง
// ชื่อต้องขึ้นต้นด้วยตัวอักษร — {0} {1} ของ string.Format จะไม่โดนแปลง
public static class InputIconText
{
    private static readonly Regex Token = new(@"\{([A-Za-z][A-Za-z0-9_]*)\}");
    private static readonly HashSet<string> _warned = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _warned.Clear();

    /// <summary>ใส่ข้อความลง TMP พร้อมแปลงไอคอน — ใช้ Sprite Asset ของตัวหนังสือนั้น ไม่มีก็ใช้ค่าเริ่มต้นของโปรเจกต์</summary>
    public static void Apply(TMP_Text target, string raw)
    {
        if (target == null)
            return;

        TMP_SpriteAsset asset = target.spriteAsset != null ? target.spriteAsset : TMP_Settings.defaultSpriteAsset;
        string text = Convert(raw, asset, target);

        if (target.text != text)
            target.text = text;
    }

    public static string Convert(string raw, TMP_SpriteAsset asset, Object context = null)
    {
        if (string.IsNullOrEmpty(raw) || raw.IndexOf('{') < 0)
            return raw;

        return Token.Replace(raw, match =>
        {
            string name = match.Groups[1].Value;

            if (HasSprite(asset, name))
                return $"<sprite name=\"{name}\">";

            // ไม่มีรูป — โชว์เป็น [E] แทน เกมยังอ่านรู้เรื่อง แล้วเตือนครั้งเดียวพอ
            if (_warned.Add(name))
                Debug.LogWarning($"[InputIconText] ไม่มีไอคอนชื่อ '{name}' ใน Sprite Asset — เช็คตัวสะกด (ตัวเล็กตัวใหญ่ต้องตรง)", context);

            return $"[{name}]";
        });
    }

    /// <summary>หาชื่อที่พิมพ์ไว้แต่ไม่มีรูป — ใช้เตือนตอนแก้ข้อมูลใน Editor</summary>
    public static List<string> FindMissing(string raw, TMP_SpriteAsset asset)
    {
        var missing = new List<string>();
        if (string.IsNullOrEmpty(raw))
            return missing;

        foreach (Match match in Token.Matches(raw))
        {
            string name = match.Groups[1].Value;
            if (!HasSprite(asset, name) && !missing.Contains(name))
                missing.Add(name);
        }

        return missing;
    }

    private static bool HasSprite(TMP_SpriteAsset asset, string name)
    {
        if (asset == null)
            return false;

        if (asset.GetSpriteIndexFromName(name) >= 0)
            return true;

        if (asset.fallbackSpriteAssets != null)
        {
            foreach (TMP_SpriteAsset fallback in asset.fallbackSpriteAssets)
            {
                if (fallback != null && fallback.GetSpriteIndexFromName(name) >= 0)
                    return true;
            }
        }

        return false;
    }
}