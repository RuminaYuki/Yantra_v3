using System;
using System.Collections.Generic;
using Unity.Collections;
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
        for (int i = 0; i < _radialMenuButtonsData.Count; i++)
        {
            _radialMenuButtonsData[i].ID = i;
        }
    }

    #region API
    public List<RadialMenuButtonData> GetListData() => _radialMenuButtonsData;
    public void ClearListData() => _radialMenuButtonsData.Clear();
    #endregion
}

[Serializable]
public class RadialMenuButtonData
{
    public int ID;

    public string buttonName;
    public Sprite buttonIcon;
}