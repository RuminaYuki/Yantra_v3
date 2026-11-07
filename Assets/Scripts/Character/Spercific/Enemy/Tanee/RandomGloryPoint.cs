using System.Collections.Generic;
using UnityEngine;
using Yuki.Learning.StateMachine;

public class RandomGloryPoint : MonoBehaviour
{
    public Transform[] GloryPoint;

    [SerializeField] private Provider _provider;
    [SerializeField] private AnchorIDSO _gloryPointAnchorID;

    // index ที่ยังไม่ถูกสุ่มในรอบนี้
    private readonly List<int> _remaining = new List<int>();

    public Transform GetRandomGloryPoint()
    {
        if (GloryPoint == null || GloryPoint.Length == 0) return null;

        // ใช้ครบทุกจุดแล้ว (หรือยังไม่เคยเริ่ม) -> เริ่มรอบใหม่
        if (_remaining.Count == 0)
        {
            for (int i = 0; i < GloryPoint.Length; i++)
                _remaining.Add(i);
        }

        int pick = Random.Range(0, _remaining.Count);
        int index = _remaining[pick];
        _remaining.RemoveAt(pick);
        return GloryPoint[index];
    }

    // สุ่มจุดแล้วเอาไปใส่ใน Provider ด้วย AnchorID เดิม
    public void RandomizeProviderGloryPoint()
    {
        if (_provider == null || _gloryPointAnchorID == null) return;

        Transform point = GetRandomGloryPoint();
        if (point == null) return;

        _provider.Set(_gloryPointAnchorID, point.gameObject);
    }
}
