using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameProject.Interaction
{
    /// <summary>
    /// Attached to Player. Scans within 3m for the nearest IInteractable, notifies HUD prompt listeners,
    /// and invokes Interact() when the player presses [E] (new Unity Input System).
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Interaction Detection")]
        [SerializeField] private float interactionRadius = 3.0f;
        [SerializeField] private LayerMask interactableMask = ~0;

        private InputAction _interactAction;
        private IInteractable _currentTarget;
        private bool _inputLocked;

        public IInteractable CurrentTarget => _currentTarget;

        public bool InputLocked
        {
            get => _inputLocked;
            set
            {
                if (_inputLocked == value)
                {
                    return;
                }

                _inputLocked = value;
                if (_inputLocked)
                {
                    SetCurrentTarget(null);
                }
            }
        }

        public event Action<IInteractable> OnFocusTargetChanged;

        private void Awake()
        {
            _interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
        }

        private void OnEnable()
        {
            if (_interactAction != null)
            {
                _interactAction.performed += OnInteractPerformed;
                _interactAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_interactAction != null)
            {
                _interactAction.performed -= OnInteractPerformed;
                _interactAction.Disable();
            }

            SetCurrentTarget(null);
        }

        private void OnDestroy()
        {
            _interactAction?.Dispose();
        }

        private void Update()
        {
            if (_inputLocked)
            {
                return;
            }

            IInteractable best = FindBestInteractable();
            SetCurrentTarget(best);
        }

        /// <summary>
        /// Triggers interaction with the currently focused target (if any).
        /// </summary>
        public bool TryInteractCurrent()
        {
            if (_inputLocked)
            {
                return false;
            }

            IInteractable target = _currentTarget ?? FindBestInteractable();
            if (target == null || !target.CanInteract)
            {
                return false;
            }

            target.Interact(this);
            return true;
        }

        private void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            TryInteractCurrent();
        }

        private IInteractable FindBestInteractable()
        {
            Vector3 center = transform.position + Vector3.up * 1.0f;
            Collider[] hits = Physics.OverlapSphere(center, interactionRadius, interactableMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Transform t = hits[i].transform;
                if (t == transform || t.IsChildOf(transform))
                {
                    continue;
                }

                IInteractable interactable = t.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract)
                {
                    continue;
                }

                Transform targetTf = interactable.InteractionTransform != null ? interactable.InteractionTransform : t;
                Vector3 toTarget = targetTf.position - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                if (dist > interactionRadius + 0.5f)
                {
                    continue;
                }

                // Slightly prefer targets in front of the Player
                float forwardBonus = 0f;
                if (dist > 0.01f)
                {
                    float dot = Vector3.Dot(transform.forward, toTarget.normalized);
                    forwardBonus = (1f - dot) * 0.6f;
                }

                float score = dist + forwardBonus;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = interactable;
                }
            }

            return best;
        }

        private void SetCurrentTarget(IInteractable target)
        {
            if (ReferenceEquals(_currentTarget, target))
            {
                return;
            }

            _currentTarget = target;
            OnFocusTargetChanged?.Invoke(_currentTarget);
        }
    }
}
