using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameProject.Player
{
    /// <summary>
    /// Reads player controls from the Unity Input System (InputActionAsset "Player" map)
    /// with automatic programmatic fallback bindings if no asset is assigned.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Asset")]
        [Tooltip("Assign InputSystem_Actions.inputactions. Falls back to default keyboard/mouse bindings if empty.")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private bool _ownsFallbackActions;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsLookFromPointer { get; private set; }

        public event Action JumpPressed;

        private void Awake()
        {
            InitializeActions();
        }

        private void OnEnable()
        {
            BindCallbacks(true);
            EnableActions(true);
        }

        private void OnDisable()
        {
            BindCallbacks(false);
            EnableActions(false);
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            IsSprinting = false;
        }

        private void OnDestroy()
        {
            if (_ownsFallbackActions)
            {
                _moveAction?.Dispose();
                _lookAction?.Dispose();
                _jumpAction?.Dispose();
                _sprintAction?.Dispose();
            }
        }

        private void Update()
        {
            if (_moveAction != null)
            {
                MoveInput = _moveAction.ReadValue<Vector2>();
            }

            if (_lookAction != null)
            {
                LookInput = _lookAction.ReadValue<Vector2>();
                var activeControl = _lookAction.activeControl;
                IsLookFromPointer = activeControl == null || activeControl.device is Pointer;
            }

            if (_sprintAction != null)
            {
                IsSprinting = _sprintAction.IsPressed();
            }
        }

        private void InitializeActions()
        {
            InputActionMap map = inputActions != null ? inputActions.FindActionMap(actionMapName, throwIfNotFound: false) : null;

            if (map != null)
            {
                _moveAction = map.FindAction("Move", throwIfNotFound: false);
                _lookAction = map.FindAction("Look", throwIfNotFound: false);
                _jumpAction = map.FindAction("Jump", throwIfNotFound: false);
                _sprintAction = map.FindAction("Sprint", throwIfNotFound: false);
                _ownsFallbackActions = false;
            }

            if (_moveAction != null && _lookAction != null && _jumpAction != null && _sprintAction != null)
            {
                return;
            }

            // Fallback: build standard PC keyboard + mouse bindings programmatically
            _ownsFallbackActions = true;

            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _lookAction = new InputAction("Look", InputActionType.Value, binding: "<Pointer>/delta", expectedControlType: "Vector2");
            _jumpAction = new InputAction("Jump", InputActionType.Button, binding: "<Keyboard>/space");
            _sprintAction = new InputAction("Sprint", InputActionType.Button, binding: "<Keyboard>/leftShift");
        }

        private void BindCallbacks(bool register)
        {
            if (_jumpAction == null)
            {
                return;
            }

            if (register)
            {
                _jumpAction.performed += OnJumpPerformed;
            }
            else
            {
                _jumpAction.performed -= OnJumpPerformed;
            }
        }

        private void EnableActions(bool enable)
        {
            if (enable)
            {
                _moveAction?.Enable();
                _lookAction?.Enable();
                _jumpAction?.Enable();
                _sprintAction?.Enable();
            }
            else
            {
                _moveAction?.Disable();
                _lookAction?.Disable();
                _jumpAction?.Disable();
                _sprintAction?.Disable();
            }
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            JumpPressed?.Invoke();
        }
    }
}
