using UnityEngine;
using System;
using Unity.VisualScripting;

public class PretGroupTracker : MonoBehaviour
{
    [SerializeField] private GameObject[] allNak;
    [SerializeField] GameObject[] barriers;
    [SerializeField] StateFlagsAccess pret;
    [SerializeField] FlagSO pretOnTrigger;

    private bool playerEntered;

    private void Awake()
    {
        // หาเองเฉพาะตอนที่ยังไม่ได้กด Set Up List ไว้
        if (IsMissing(allNak) || IsMissing(barriers) || pret == null)
            FindReferences();
    }

    private void OnEnable()
    {
        pret.FlagChanged += FlageventHandle;
    }

    private void OnDisable()
    {
        pret.FlagChanged -= FlageventHandle;
    }

    private bool IsMissing(GameObject[] arr)
    {
        return arr == null || arr.Length == 0;
    }

    void Start()
    {
        SetWhenBarriersLocked(false);
    }
    private void FlageventHandle(FlagSO flagSO, bool enable)
    {
        if(flagSO == pretOnTrigger)
        {
            SetWhenBarriersLocked(enable);
        }
    }

    private void SetWhenBarriersLocked(bool enable)
    {
        if (barriers == null) return;

        foreach (var barrier in barriers)
        {
            if (barrier == null) continue;

            foreach (var particle in barrier.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (enable) particle.Play();
                else particle.Stop();
            }

            foreach (var col in barrier.GetComponentsInChildren<Collider>(true))
                col.isTrigger = !enable;
        }

        //Set nak
        if (allNak == null) return;

        foreach (GameObject nak in allNak)
        {
            nak.SetActive(enable);
        }
    }

    [ContextMenu("Set Up List")]
    public void SetUpList()
    {
        // ปุ่มใน Editor: บันทึก Undo + ให้ Save ค่าลงฉาก
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Set up List");
#endif
        FindReferences();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // หาของจริง ใช้ได้ทั้งตอนกดปุ่มและตอน Awake
    private void FindReferences()
    {
        // ลูกทุกชั้นใน Hierarchy (true = รวมตัวที่ถูกปิดอยู่)
        Transform[] children = GetComponentsInChildren<Transform>(true);

        allNak   = Array.ConvertAll(Array.FindAll(children, t => t.CompareTag("Nak")),     t => t.gameObject);
        barriers = Array.ConvertAll(Array.FindAll(children, t => t.CompareTag("Barrier")), t => t.gameObject);

        Transform pretTransform = Array.Find(children, t => t.CompareTag("Pret"));   // มีตัวเดียว เอาตัวแรกที่เจอ
        pret = pretTransform != null ? pretTransform.GetComponent<StateFlagsAccess>() : null;
    }
}
