using System;
using System.Collections.Generic;
using UnityEngine;

// ค่าของ Item Icon Generator — เซฟเป็นไฟล์ ทั้งทีมจะได้ไอคอนหน้าตาเดียวกัน
public class ItemIconProfile : ScriptableObject
{
    [Serializable]
    public class ItemSettings
    {
        public ItemData item;
        public Vector3 rotation;
        public bool autoRoll = true;
        public float roll;
        public float zoom = 1f;
        public Vector2 offset;
    }

    public string outputFolder = "Assets/Ui/Inventory/Icons";
    public int pixelsPerCell = 240;
    public int supersample = 2;
    public float padding = 0.06f;
    public float cameraElevation = 20f;

    public float brightness = 0f;
    public Color keyColor = new Color(1f, 0.9f, 0.76f);
    public float keyIntensity = 3.2f;
    public Color fillColor = new Color(0.82f, 0.88f, 1f);
    public float fillIntensity = 0.8f;
    public Color rimColor = new Color(1f, 0.82f, 0.6f);
    public float rimIntensity = 2.5f;
    public float ambient = 1f;
    public bool selfShadows = true;

    public bool dropShadow = true;
    public float shadowOpacity = 0.55f;
    public Vector2 shadowOffset = new Vector2(0.02f, 0.035f);
    public float shadowBlur = 0.03f;

    public bool assignToItem = true;
    public bool keepHandmadeIcons = true;
    public bool skipBags = true;

    public List<ItemSettings> items = new List<ItemSettings>();

    public ItemSettings Find(ItemData item)
    {
        foreach (ItemSettings s in items)
            if (s.item == item) return s;
        return null;
    }

    public ItemSettings GetOrAdd(ItemData item)
    {
        ItemSettings s = Find(item);
        if (s != null) return s;

        s = new ItemSettings { item = item, rotation = item.previewRotation };
        items.Add(s);
        return s;
    }

    public void ResetItem(ItemSettings s)
    {
        s.rotation = s.item != null ? s.item.previewRotation : Vector3.zero;
        s.autoRoll = true;
        s.roll = 0f;
        s.zoom = 1f;
        s.offset = Vector2.zero;
    }
}
