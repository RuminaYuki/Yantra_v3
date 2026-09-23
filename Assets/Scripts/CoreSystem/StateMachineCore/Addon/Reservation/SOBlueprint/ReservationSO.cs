using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New_Reservation",
    menuName = "YUKI Learning State Machine/ReservationSO")]
public class ReservationSO : ScriptableObject
{
    private readonly HashSet<object> _reservations = new();
    public int Count => _reservations.Count;
    public bool HasAny => _reservations.Count > 0;

    public void Claim(object owner) => _reservations.Add(owner);
    public void Release(object owner) => _reservations.Remove(owner);
    public bool IsClaimedBy(object owner) => _reservations.Contains(owner);
    public void Clear() => _reservations.Clear();
}
