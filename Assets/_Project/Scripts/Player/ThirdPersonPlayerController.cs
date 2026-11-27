using UnityEngine;
using GameProject.Stats;

namespace GameProject.Player
{
    /// <summary>
    /// Third-person RPG/Adventure character movement using CharacterController
    /// and Unity Input System (WASD move, Left Shift sprint with Stamina, Space jump).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class ThirdPersonPlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference camera for camera-relative movement. Defaults to Camera.main if unassigned.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Optional PlayerStats component for stamina-gated sprinting. Auto-resolved on this GameObject if unassigned.")]
        [SerializeField] private PlayerStats playerStats;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5.0f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float speedChangeRate = 12.0f;
        [SerializeField] private float rotationSmoothTime = 0.1f;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpHeight = 1.6f;
        [SerializeField] private float gravity = -20.0f;
        [SerializeField] private float groundedStickVelocity = -2.0f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Skill Impulse")]
        [SerializeField] private float externalImpulseDamping = 6.5f;

        private CharacterController _controller;
        private PlayerInputReader _input;

        private float _currentSpeed;
        private float _verticalVelocity;
        private float _rotationVelocity;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private Vector3 _externalHorizontalVelocity;

        public bool InputLocked { get; set; }
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsSprinting { get; private set; }
        public float CurrentHorizontalSpeed => _currentSpeed;
        public Vector3 HorizontalVelocity { get; private set; }

        /// <summary>
        /// Applies an instant horizontal velocity burst and/or upward vertical launch (used by movement skills).
        /// </summary>
        public void ApplySkillImpulse(Vector3 horizontalVelocity, float verticalLaunchVelocity = 0f)
        {
            _externalHorizontalVelocity += new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
            if (verticalLaunchVelocity > 0f)
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity, verticalLaunchVelocity);
                _coyoteTimer = 0f;
                _jumpBufferTimer = 0f;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();

            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>();
            }

            if (cameraTransform == null && UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.JumpPressed += HandleJumpPressed;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.JumpPressed -= HandleJumpPressed;
            }
        }

        private void Update()
        {
            if (cameraTransform == null && UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }

            UpdateTimers();
            HandleMovementAndGravity();
        }

        private void HandleJumpPressed()
        {
            if (InputLocked)
            {
                return;
            }

            _jumpBufferTimer = jumpBufferTime;
        }

        private void UpdateTimers()
        {
            if (_controller.isGrounded)
            {
                _coyoteTimer = coyoteTime;
            }
            else if (_coyoteTimer > 0f)
            {
                _coyoteTimer -= Time.deltaTime;
            }

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= Time.deltaTime;
            }
        }

        private void HandleMovementAndGravity()
        {
            Vector2 moveInput = InputLocked ? Vector2.zero : _input.MoveInput;
            bool hasInput = moveInput.sqrMagnitude > 0.001f;

            bool wantsSprint = !InputLocked && hasInput && _input.IsSprinting;
            bool canSprint = playerStats == null || playerStats.CanSprint;
            IsSprinting = wantsSprint && canSprint;

            if (playerStats != null)
            {
                playerStats.SetSprinting(IsSprinting);
            }

            float targetSpeed = 0f;
            if (hasInput)
            {
                targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
            }

            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, 1f - Mathf.Exp(-speedChangeRate * Time.deltaTime));
            if (!hasInput && _currentSpeed < 0.05f)
            {
                _currentSpeed = 0f;
            }

            Vector3 moveDirection = Vector3.zero;
            if (hasInput)
            {
                Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                float cameraYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
                float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraYaw;

                float smoothedAngle = Mathf.SmoothDampAngle(
                    transform.eulerAngles.y,
                    targetAngle,
                    ref _rotationVelocity,
                    rotationSmoothTime);

                transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
                moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            }

            // Grounded stick & jump execution (with coyote time + jump buffer)
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = groundedStickVelocity;
            }

            if (!InputLocked && _jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }

            _verticalVelocity += gravity * Time.deltaTime;

            // Smoothly damp any skill-applied horizontal impulse (e.g. ForwardLunge)
            if (_externalHorizontalVelocity.sqrMagnitude > 0.01f)
            {
                _externalHorizontalVelocity = Vector3.Lerp(
                    _externalHorizontalVelocity,
                    Vector3.zero,
                    1f - Mathf.Exp(-externalImpulseDamping * Time.deltaTime));
            }
            else
            {
                _externalHorizontalVelocity = Vector3.zero;
            }

            HorizontalVelocity = moveDirection.normalized * _currentSpeed + _externalHorizontalVelocity;
            Vector3 finalVelocity = HorizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(finalVelocity * Time.deltaTime);
        }
    }
}
