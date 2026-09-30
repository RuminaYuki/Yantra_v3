// Append new values at the END only. Unity serializes enums as numbers,
// so inserting in the middle would change the category of existing items.
public enum ItemCategory
{
    Misc,
    Weapon,
    Consumable,
    Material,
    Quest
}
