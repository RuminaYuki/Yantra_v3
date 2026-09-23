using System.Collections.Generic;
using UnityEngine;

public class ReservationController : MonoBehaviour
{
    public List<ReservationSO> reservationSO;
    void OnEnable() => Claer();
    void OnDisable() => Claer();
    private void Claer()
    {
        foreach(ReservationSO reservationSO in reservationSO)
        {
            reservationSO.Clear();
        }
    }
}
