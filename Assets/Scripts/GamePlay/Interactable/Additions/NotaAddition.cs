using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class NotaAddition : MonoBehaviour
{
    [SerializeField] ExamineData data;
    private Iinteractor interactor;

    private void Awake()
    {
        interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        interactor.OnInteract += HandleInteract;
        interactor.OnEndInteract += HandleEndInteract;
    }

    private void OnDisable()
    {
        interactor.OnInteract -= HandleInteract;
        interactor.OnEndInteract -= HandleEndInteract;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        // เปิด UI
        ExamineUI examineUI = rootplayer.GetComponentInChildren<ExamineUI>();
        if (examineUI != null)
        {
            examineUI.Open(data);
        }
    }

    private void HandleEndInteract(GameObject rootplayer)
    {
        // ปิด UI
        ExamineUI examineUI = rootplayer.GetComponentInChildren<ExamineUI>();
        if (examineUI != null)
        {
            examineUI.Close();
        }
    }
}
