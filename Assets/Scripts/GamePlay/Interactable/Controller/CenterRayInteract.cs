using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.GamePlay.Interactable
{
    public class CenterRayInteract : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject playerRoot;

        [Header("Highlight Detection")]
        [SerializeField] private float highlightDistance = 3f;
        [SerializeField] private LayerMask interactableLayer;

        [Header("Focus Detection")]
        [SerializeField] private float focusDistance = 2f;
        [SerializeField, Range(0f, 1f)]
        private float focusScreenRadius = 0.15f;

        [Header("Line Of Sight")]
        [SerializeField] private LayerMask obstacleLayer;

        public GameObject currentInteractableDebug;
        private Iinteractor currentInteractable;

        public Iinteractor CurrentInteractable
        {
            get
            {
                if (currentInteractable is MonoBehaviour target && target == null)
                {
                    currentInteractable = null;
                    currentInteractableDebug = null;
                }

                return currentInteractable;
            }
        }

        private readonly Collider[] interactableBuffer = new Collider[32];

        private readonly HashSet<Iinteractor> highlightedInteractors = new();

        private bool isInteractableInSight = false;

        void Awake()
        {
            highlightedInteractors.Clear();
        }

        private void Update()
        {
            if(isInteractableInSight)
            {
                StopHighlightingAll();
                return;
            }

            DetectNearbyInteractables();
            DetectInteractable();

            currentInteractableDebug = (currentInteractable as MonoBehaviour)?.gameObject;
        }

        private void DetectNearbyInteractables()
        {
            int count = Physics.OverlapSphereNonAlloc(
                playerRoot.transform.position,
                highlightDistance,
                interactableBuffer,
                interactableLayer,
                QueryTriggerInteraction.Collide
            );

            HashSet<Iinteractor> detectedThisFrame = new();

            for (int i = 0; i < count; i++)
            {
                Collider collider = interactableBuffer[i];

                if (collider == null)
                    continue;

                Iinteractor interactable =
                    collider.GetComponentInParent<Iinteractor>();

                if (interactable == null) continue;

                if (!IsAlive(interactable))
                    continue;

                detectedThisFrame.Add(interactable);

                if (!highlightedInteractors.Contains(interactable) && interactable.CanInteract)
                {
                    interactable.ShowHighlight();
                    highlightedInteractors.Add(interactable);
                }
            }

            // Remove highlights from interactables that are no longer detected
            foreach (Iinteractor interactable in highlightedInteractors)
            {
                if (!detectedThisFrame.Contains(interactable))
                {
                    interactable.HideHighlight();
                }
            }

            highlightedInteractors.RemoveWhere(
                interactable => !detectedThisFrame.Contains(interactable)
            );
        }

        private void DetectInteractable()
        {
            Iinteractor bestInteractable = null;

            float bestScore = float.MaxValue;

            foreach (Iinteractor interactable in highlightedInteractors)
            {
                if (!IsAlive(interactable))
                    continue;

                MonoBehaviour target = interactable as MonoBehaviour;

                if (target == null)
                    continue;

                Vector3 targetPosition = target.transform.position;

                float distance =
                    Vector3.Distance(
                        playerCamera.transform.position,
                        targetPosition
                    );

                if (distance > focusDistance)
                    continue;

                Vector3 viewportPosition =
                    playerCamera.WorldToViewportPoint(targetPosition);

                if (viewportPosition.z <= 0f)
                    continue;

                Vector2 screenCenter = new Vector2(0.5f, 0.5f);

                Vector2 targetScreenPosition =
                    new Vector2(
                        viewportPosition.x,
                        viewportPosition.y
                    );

                float screenDistance =
                    Vector2.Distance(
                        screenCenter,
                        targetScreenPosition
                    );

                if (screenDistance > focusScreenRadius)
                    continue;

                if (Physics.Linecast(
                    playerCamera.transform.position,
                    targetPosition,
                    obstacleLayer))
                {
                    continue;
                }

                if (screenDistance < bestScore)
                {
                    bestScore = screenDistance;
                    bestInteractable = interactable;
                }
            }

            if (bestInteractable == null)
            {
                ClearCurrentInteractable();
                return;
            }

            SetFocus(bestInteractable);
        }

        private void SetFocus(Iinteractor interactable)
        {
            if (!IsAlive(interactable) || interactable == currentInteractable)
                return;

            ClearCurrentInteractable();

            currentInteractable = interactable;
            currentInteractable.OnFocus();
        }

        private void ClearCurrentInteractable()
        {
            if (!IsAlive(currentInteractable))
            {
                currentInteractable = null;
                currentInteractableDebug = null;
                return;
            }

            //Debug.Log($"Lost focus on {currentInteractable as MonoBehaviour}");
            currentInteractable.OnLoseFocus();
            currentInteractable = null;
        }

        private static bool IsAlive(Iinteractor interactable)
        {
            return interactable is MonoBehaviour target && target != null;
        }

        public void SetInteractEnabled(bool enabled)
        {
            isInteractableInSight = !enabled;

            if (!enabled)
            {
                ClearCurrentInteractable();
                currentInteractableDebug = null;
            }
        }

        public void StopHighlightingAll()
        {
            foreach (Iinteractor interactable in highlightedInteractors)
            {
                if (IsAlive(interactable))
                    interactable.HideHighlight();
            }

            highlightedInteractors.Clear();
            ClearCurrentInteractable();
            currentInteractableDebug = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (playerCamera == null || playerRoot == null)
                return;

            Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(
                playerRoot.transform.position,
                highlightDistance
            );

            Gizmos.color = Color.red;

            Vector3 origin = playerCamera.transform.position;

            Gizmos.DrawRay(
                origin,
                playerCamera.transform.forward * focusDistance
            );
        }
    }
}
