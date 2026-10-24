using System.Collections.Generic;
using SDFcl.GamePlay.Interactable;
using UnityEngine;

// ใส่ที่วัตถุที่ interact ได้ (เช่น ศาล, คัมภีร์) พอผู้เล่น interact จะปลดล็อคการร่ายตาม spellIds
public class UnlockSpellAddition : MonoBehaviour
{
    [Tooltip("ต้องตรงกับ spellId ใน RadialMenuDataSO")]
    [SerializeField] List<string> _spellIds = new();

    Iinteractor _interactor;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        if (_interactor != null)
            _interactor.OnInteract += HandleInteract;
    }

    private void OnDisable()
    {
        if (_interactor != null)
            _interactor.OnInteract -= HandleInteract;
    }

    public void HandleInteract(GameObject rootPlayer)
    {
        if (rootPlayer == null) return;

        SpellUnlocks unlocks = rootPlayer.GetComponentInChildren<SpellUnlocks>();
        if (unlocks == null)
        {
            Debug.LogWarning($"[UnlockSpell] {rootPlayer.name} has no SpellUnlocks.", this);
            return;
        }

        foreach (string id in _spellIds)
            unlocks.Unlock(id, this);
    }
}
