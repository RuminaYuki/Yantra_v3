using UnityEngine;

// One place in the game, shown on the Load Save screen (name + background image).
// A save file only keeps locationId, so never change the id after saves exist.
// Use a fixed text id such as "temple_entrance", not the asset name.
[CreateAssetMenu(fileName = "New Location Data", menuName = "Save/Location Data", order = 50)]
public class LocationData : ScriptableObject
{
    public string locationId;
    public string locationName;
    public Sprite locationImage;
}
