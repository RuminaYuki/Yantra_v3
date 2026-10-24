using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "New Radial Menu Data",
    menuName = "Radial Menu/Radial Menu Data")]
public class RadialMenuDataSO : ScriptableObject
{
    public IntEventChannelSO IDEventChannel;

    [SerializeField]
    private List<RadialMenuButtonData> _radialMenuButtonsData = new();

    private void OnValidate()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < _radialMenuButtonsData.Count; i++)
        {
            RadialMenuButtonData data = _radialMenuButtonsData[i];
            if (data == null) continue;

            data.ID = i;

            if (string.IsNullOrEmpty(data.spellId) && !string.IsNullOrEmpty(data.buttonName))
                data.spellId = data.buttonName;

            if (!string.IsNullOrEmpty(data.spellId) && !seen.Add(data.spellId))
                Debug.LogWarning($"[RadialMenu] {name}: spellId '{data.spellId}' is used twice.", this);
        }
    }

    #region API
    public List<RadialMenuButtonData> GetListData() => _radialMenuButtonsData;
    public void ClearListData() => _radialMenuButtonsData.Clear();
    public void AddButtonData(RadialMenuButtonData buttonData) => _radialMenuButtonsData.Add(buttonData);

    public bool TryGetById(string spellId, out RadialMenuButtonData data)
    {
        foreach (RadialMenuButtonData d in _radialMenuButtonsData)
        {
            if (d != null && d.spellId == spellId)
            {
                data = d;
                return true;
            }
        }
        data = null;
        return false;
    }
    #endregion
}

[Serializable]
public class RadialMenuButtonData
{
    public int ID;

    public string buttonName;
    public Texture buttonIcon;

    [Header("Spell")]
    [Tooltip("ID ถาวร ใช้ตอนปลดล็อคและตอนเซฟ ว่างไว้ = ใช้ buttonName\nห้ามเปลี่ยนหลังปล่อยเกม ไม่งั้นเซฟเก่าจะหาไม่เจอ")]
    public string spellId;
    [Tooltip("Template ของการร่ายที่จะสร้างเมื่อเลือกปุ่มนี้")]
    public GameObject spellPrefab;
    [Tooltip("โชว์ในเมนูตั้งแต่เริ่มเกม ไม่ต้องปลดล็อค")]
    public bool unlockedByDefault;

    [Header("Item")]
    [Tooltip("Item needed to pick this button. Empty = always available.")]
    public ItemData itemData;
    [Tooltip("Minimum amount of itemData in the inventory.")]
    [Min(0)] public int itemReq;
}
