using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using GameProject.CameraSystem;
using GameProject.Combat;
using GameProject.Core;
using GameProject.Interaction;
using GameProject.Player;
using GameProject.Skills;
using GameProject.Stats;

namespace GameProject.UI
{
    /// <summary>
    /// Controls the in-game Pause Menu toggled via ESC (only when Skill Creator [K],
    /// NPC Dialogue, and Game Over are not active). Freezes Time.timeScale = 0, locks player/camera
    /// controls, unlocks the mouse cursor, and integrates with SaveSystem and MainMenu.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("References")]
        [SerializeField] private ThirdPersonPlayerController playerController;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private MovementComboDetector comboDetector;
        [SerializeField] private PlayerMeleeAttack meleeAttack;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private SkillCreatorController skillCreator;
        [SerializeField] private DialogueUiController dialogueUi;

        private const string HiddenOverlayClass = "pause-overlay--hidden";
        private const string HiddenStatusClass = "pause-status--hidden";

        private UIDocument _uiDocument;
        private VisualElement _overlay;
        private Label _statusLabel;
        private Button _btnResume;
        private Button _btnSave;
        private Button _btnMainMenu;
        private Button _btnQuit;

        private InputAction _escapeAction;
        private bool _isPaused;

        public bool IsPaused => _isPaused;
        public VisualElement Root => _uiDocument != null ? _uiDocument.rootVisualElement : null;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _escapeAction = new InputAction("PauseToggle", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindUiElements();

            if (_escapeAction != null)
            {
                _escapeAction.performed += OnEscapePressed;
                _escapeAction.Enable();
            }

            SetPaused(false, restoreCursor: false);
        }

        private void OnDisable()
        {
            if (_escapeAction != null)
            {
                _escapeAction.performed -= OnEscapePressed;
                _escapeAction.Disable();
            }

            UnbindUiElements();
        }

        private void OnDestroy()
        {
            if (_isPaused)
            {
                Time.timeScale = 1f;
            }

            _escapeAction?.Dispose();
        }

        public bool CanOpenPauseMenu()
        {
            ResolveReferences();

            if (skillCreator != null && (skillCreator.IsOpen || skillCreator.LastClosedFrame == Time.frameCount))
            {
                return false;
            }

            if (dialogueUi != null && (dialogueUi.IsOpen || dialogueUi.LastClosedFrame == Time.frameCount))
            {
                return false;
            }

            if (playerStats != null && !playerStats.IsAlive)
            {
                return false;
            }

            return true;
        }

        public void TogglePause()
        {
            if (_isPaused)
            {
                SetPaused(false);
            }
            else if (CanOpenPauseMenu())
            {
                SetPaused(true);
            }
        }

        public void SetPaused(bool paused, bool restoreCursor = true)
        {
            ResolveReferences();
            if (_overlay == null)
            {
                BindUiElements();
            }

            _isPaused = paused;
            Time.timeScale = _isPaused ? 0f : 1f;

            if (_overlay != null)
            {
                _overlay.EnableInClassList(HiddenOverlayClass, !_isPaused);
            }

            if (_statusLabel != null && !_isPaused)
            {
                _statusLabel.AddToClassList(HiddenStatusClass);
            }

            if (playerController != null) playerController.InputLocked = _isPaused;
            if (cameraController != null) cameraController.InputLocked = _isPaused;
            if (comboDetector != null) comboDetector.InputLocked = _isPaused;
            if (meleeAttack != null) meleeAttack.InputLocked = _isPaused;
            if (playerInteractor != null) playerInteractor.InputLocked = _isPaused;
            if (skillCreator != null) skillCreator.ExternalLock = _isPaused;

            if (_isPaused)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else if (restoreCursor)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        public void OnResumeClicked()
        {
            SetPaused(false);
        }

        public void OnSaveGameClicked()
        {
            SaveSystem saveSystem = FindAnyObjectByType<SaveSystem>();
            bool saved = saveSystem != null && saveSystem.SaveGame(showNotification: true);

            if (_statusLabel != null)
            {
                _statusLabel.text = saved ? "✓ Đã lưu game!" : "Không thể lưu game!";
                _statusLabel.RemoveFromClassList(HiddenStatusClass);
            }
        }

        public void OnMainMenuClicked()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        public void OnQuitClicked()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnEscapePressed(InputAction.CallbackContext ctx)
        {
            TogglePause();
        }

        private void BindUiElements()
        {
            UnbindUiElements();

            VisualElement root = Root;
            if (root == null)
            {
                return;
            }

            root.pickingMode = PickingMode.Ignore;

            root.pickingMode = PickingMode.Ignore;

            _overlay = root.Q<VisualElement>("pause-overlay");
            _statusLabel = root.Q<Label>("pause-status-label");
            _btnResume = root.Q<Button>("btn-resume");
            _btnSave = root.Q<Button>("btn-save");
            _btnMainMenu = root.Q<Button>("btn-main-menu");
            _btnQuit = root.Q<Button>("btn-quit");

            if (_btnResume != null) _btnResume.clicked += OnResumeClicked;
            if (_btnSave != null) _btnSave.clicked += OnSaveGameClicked;
            if (_btnMainMenu != null) _btnMainMenu.clicked += OnMainMenuClicked;
            if (_btnQuit != null) _btnQuit.clicked += OnQuitClicked;
        }

        private void UnbindUiElements()
        {
            if (_btnResume != null) _btnResume.clicked -= OnResumeClicked;
            if (_btnSave != null) _btnSave.clicked -= OnSaveGameClicked;
            if (_btnMainMenu != null) _btnMainMenu.clicked -= OnMainMenuClicked;
            if (_btnQuit != null) _btnQuit.clicked -= OnQuitClicked;
        }

        private void ResolveReferences()
        {
            if (playerController == null) playerController = FindAnyObjectByType<ThirdPersonPlayerController>();
            if (cameraController == null) cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            if (comboDetector == null) comboDetector = FindAnyObjectByType<MovementComboDetector>();
            if (meleeAttack == null) meleeAttack = FindAnyObjectByType<PlayerMeleeAttack>();
            if (playerInteractor == null) playerInteractor = FindAnyObjectByType<PlayerInteractor>();
            if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
            if (skillCreator == null) skillCreator = FindAnyObjectByType<SkillCreatorController>();
            if (dialogueUi == null) dialogueUi = FindAnyObjectByType<DialogueUiController>();
        }
    }
}
