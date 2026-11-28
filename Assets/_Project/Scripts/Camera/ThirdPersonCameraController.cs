using UnityEngine;
using UnityEngine.InputSystem;
using GameProject.Feedback;
using GameProject.Player;

namespace GameProject.CameraSystem
{
    /// <summary>
    /// Smooth third-person orbit camera that follows a player target, rotates via
    /// mouse/gamepad Look input (new Unity Input System), gently aligns behind movement
    /// when idle, and avoids clipping through solid geometry.
    /// </summary>
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Assign Player's CameraPivot (or Player root).")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private Vector3 pivotOffset = Vector3.zero;

        [Header("Orbit & Distance")]
        [SerializeField] private float distance = 5.5f;
        [SerializeField] private float minDistance = 2.5f;
        [SerializeField] private float maxDistance = 9.0f;
        [SerializeField] private float zoomSpeed = 2.0f;
        [SerializeField] private float mouseSensitivity = 0.18f;
        [SerializeField] private float gamepadSensitivity = 140.0f;
        [SerializeField] private float minPitch = -25.0f;
        [SerializeField] private float maxPitch = 70.0f;
        [SerializeField] private bool invertY = false;

        [Header("Smoothing")]
        [SerializeField] private float positionSmoothTime = 0.08f;
        [SerializeField] private float rotationSmoothTime = 0.05f;

        [Header("Auto-Align Behind Movement")]
        [Tooltip("Seconds after mouse stops moving before camera gently follows movement direction.")]
        [SerializeField] private float autoAlignDelay = 1.2f;
        [Tooltip("How gently the camera aligns behind the character while moving. Set 0 to disable.")]
        [SerializeField] private float autoAlignSpeed = 1.25f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private float collisionPadding = 0.15f;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnStart = true;

        private float _targetYaw;
        private float _targetPitch = 18.0f;
        private float _currentYaw;
        private float _currentPitch = 18.0f;
        private float _yawVelocity;
        private float _pitchVelocity;

        private float _targetDistance;
        private float _currentDistance;
        private Vector3 _smoothedPivotPosition;
        private Vector3 _pivotVelocity;
        private float _timeSinceManualLook;
        private Transform _playerRoot;
        private bool _inputLocked;
        private int _lastUnlockFrame = -1;
        private CameraShake _cameraShake;

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
                    SetCursorLocked(false);
                }
                else
                {
                    _lastUnlockFrame = Time.frameCount;
                    if (lockCursorOnStart)
                    {
                        SetCursorLocked(true);
                    }
                }
            }
        }

        private void Awake()
        {
            _cameraShake = GetComponent<CameraShake>();
            _targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            _currentDistance = _targetDistance;
        }

        private void Start()
        {
            ResolveReferences();

            if (target != null)
            {
                _smoothedPivotPosition = GetRawPivotPosition();
                _targetYaw = target.eulerAngles.y;
                _currentYaw = _targetYaw;
            }
            else
            {
                Vector3 euler = transform.eulerAngles;
                _targetYaw = euler.y;
                _currentYaw = euler.y;
                _targetPitch = Mathf.Clamp(euler.x, minPitch, maxPitch);
                _currentPitch = _targetPitch;
            }

            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void LateUpdate()
        {
            if (_inputLocked)
            {
                if (target != null)
                {
                    UpdateCameraTransform();
                }

                return;
            }

            HandleCursorLockToggle();

            if (target == null)
            {
                ResolveReferences();
                if (target == null)
                {
                    return;
                }
            }

            HandleZoom();
            HandleLookInput();
            HandleAutoAlign();
            UpdateCameraTransform();
        }

        private void ResolveReferences()
        {
            if (inputReader == null)
            {
                if (target != null)
                {
                    inputReader = target.GetComponentInParent<PlayerInputReader>();
                }

                if (inputReader == null)
                {
                    inputReader = FindAnyObjectByType<PlayerInputReader>();
                }
            }

            if (target == null && inputReader != null)
            {
                Transform pivot = inputReader.transform.Find("CameraPivot");
                target = pivot != null ? pivot : inputReader.transform;
            }

            _playerRoot = inputReader != null ? inputReader.transform : (target != null ? target.root : null);
        }

        private void HandleCursorLockToggle()
        {
            if (Time.frameCount == _lastUnlockFrame)
            {
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleZoom()
        {
            if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                _targetDistance = Mathf.Clamp(_targetDistance - Mathf.Sign(scrollY) * zoomSpeed * 0.5f, minDistance, maxDistance);
            }
        }

        private void HandleLookInput()
        {
            Vector2 look = inputReader != null ? inputReader.LookInput : Vector2.zero;

            // Ignore mouse movement when cursor is unlocked
            if (inputReader != null && inputReader.IsLookFromPointer && Cursor.lockState != CursorLockMode.Locked)
            {
                look = Vector2.zero;
            }

            if (look.sqrMagnitude > 0.0001f)
            {
                float yawDelta;
                float pitchDelta;

                if (inputReader != null && inputReader.IsLookFromPointer)
                {
                    yawDelta = look.x * mouseSensitivity;
                    pitchDelta = look.y * mouseSensitivity;
                }
                else
                {
                    yawDelta = look.x * gamepadSensitivity * Time.deltaTime;
                    pitchDelta = look.y * gamepadSensitivity * Time.deltaTime;
                }

                _targetYaw += yawDelta;
                _targetPitch += invertY ? pitchDelta : -pitchDelta;
                _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
                _timeSinceManualLook = 0f;
            }
            else
            {
                _timeSinceManualLook += Time.deltaTime;
            }
        }

        private void HandleAutoAlign()
        {
            if (autoAlignSpeed <= 0f || inputReader == null || _playerRoot == null)
            {
                return;
            }

            if (_timeSinceManualLook < autoAlignDelay)
            {
                return;
            }

            // Gently drift camera yaw toward the player's facing direction while moving laterally
            Vector2 move = inputReader.MoveInput;
            if (move.sqrMagnitude > 0.1f && Mathf.Abs(move.x) > 0.25f)
            {
                float playerYaw = _playerRoot.eulerAngles.y;
                _targetYaw = Mathf.LerpAngle(_targetYaw, playerYaw, autoAlignSpeed * Time.deltaTime);
            }
        }

        private void UpdateCameraTransform()
        {
            Vector3 desiredPivot = GetRawPivotPosition();
            _smoothedPivotPosition = Vector3.SmoothDamp(
                _smoothedPivotPosition,
                desiredPivot,
                ref _pivotVelocity,
                positionSmoothTime);

            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, rotationSmoothTime);
            _currentPitch = Mathf.SmoothDamp(_currentPitch, _targetPitch, ref _pitchVelocity, rotationSmoothTime);

            Quaternion orbitRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 orbitDirection = orbitRotation * Vector3.back;

            float resolvedDistance = ResolveCameraCollision(_smoothedPivotPosition, orbitDirection, _targetDistance);
            _currentDistance = Mathf.Lerp(_currentDistance, resolvedDistance, 1f - Mathf.Exp(-15f * Time.deltaTime));

            Vector3 shakeLocalOffset = _cameraShake != null ? _cameraShake.CurrentOffset : Vector3.zero;
            Vector3 shakeWorldOffset = orbitRotation * shakeLocalOffset;

            transform.position = _smoothedPivotPosition + orbitDirection * _currentDistance + shakeWorldOffset;
            transform.rotation = orbitRotation;
        }

        private float ResolveCameraCollision(Vector3 pivot, Vector3 direction, float desiredDistance)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                pivot,
                collisionRadius,
                direction,
                desiredDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            float closest = desiredDistance;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (_playerRoot != null && (hitTransform == _playerRoot || hitTransform.IsChildOf(_playerRoot)))
                {
                    continue;
                }

                float safeDistance = Mathf.Max(0.5f, hits[i].distance - collisionPadding);
                if (safeDistance < closest)
                {
                    closest = safeDistance;
                }
            }

            return closest;
        }

        private Vector3 GetRawPivotPosition()
        {
            return target.position + pivotOffset;
        }
    }
}
