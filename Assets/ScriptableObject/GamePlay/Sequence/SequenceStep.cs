using System.Collections;
using UnityEngine;

public abstract class SequenceStep : MonoBehaviour
{
    public abstract IEnumerator Run();

    // sequence จบหรือถูก Stop แล้ว ให้ step เก็บกวาด/คืนค่าที่ค้างไว้
    public virtual void OnSequenceEnd() { }
}