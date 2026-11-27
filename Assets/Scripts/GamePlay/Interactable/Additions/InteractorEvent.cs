using UnityEngine;
using UnityEngine.Events;

namespace SDFcl.GamePlay.Interactable
{
    public class InteractorEvent : MonoBehaviour
    {
        [SerializeField] UnityEvent<GameObject> OnInteract;
        [SerializeField] UnityEvent<GameObject> OnEndInteract;

        Iinteractor interactor;

        private void Awake() => interactor = GetComponent<Iinteractor>();

        private void OnEnable()
        {
            interactor.OnInteract += HandleEnter;
            interactor.OnEndInteract += HandleExit;
        }

        private void OnDisable()
        {
            interactor.OnInteract -= HandleEnter;
            interactor.OnEndInteract -= HandleExit;
        }

        private void HandleEnter(GameObject who) => OnInteract?.Invoke(who);
        private void HandleExit(GameObject who) => OnEndInteract?.Invoke(who);

        [ContextMenu("Invoke Interact")]
        public void InvokeInteract() => OnInteract?.Invoke(null);

        [ContextMenu("Invoke End Interact")]
        public void InvokeEndInteract() => OnEndInteract?.Invoke(null);
    }
}
