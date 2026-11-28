using UnityEngine;
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
    /// Controls the UI Toolkit Game Over overlay when PlayerStats.OnDied fires.
    /// Freezes gameplay, unlocks the mouse cursor, locks other menus, and lets the player
    /// retry the current scene, load the latest SaveSystem file, or return to MainMenu.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameOverController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("References")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private ThirdPersonPlayerController playerController;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private MovementComboDetector comboDetector;
        [SerializeField] private PlayerMeleeAttack meleeAttack;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private SkillCreatorController skillCreator;
        [SerializeField] private DialogueUiController dialogueUi;
        [SerializeField] private PauseMenuController pauseMenu;

        private const string HiddenOverlayClass = "gameover-overlay--hidden";
        private const string DisabledBtnClass = "gameover-btn--disabled";

        private UIDocument _uiDocument;
        private VisualElement _overlay;
        private Button _btnRetry;
        private Button _btnLoadSave;
        private Button _btnMainMenu;

        private bool _isGameOver;
        private bool _isSubscribed;

        public bool IsGameOver => _isGameOver;
        public VisualElement Root => _uiDocument != null ? _uiDocument.rootVisualElement : null;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureInActiveGameplayScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInActiveGameplayScene();
        }

        private static void EnsureInActiveGameplayScene()
        {
            string activeScene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(activeScene) || activeScene == "MainMenu")
            {
                return;
            }

            if (FindAnyObjectByType<PlayerStats>() == null)
            {
                return;
            }

            if (FindAnyObjectByType<GameOverController>() != null)
            {
                return;
            }

            PanelSettings sharedPanelSettings = null;
            UIDocument[] existingDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
            for (int i = 0; i < existingDocs.Length; i++)
            {
                if (existingDocs[i] != null && existingDocs[i].panelSettings != null)
                {
                    sharedPanelSettings = existingDocs[i].panelSettings;
                    break;
                }
            }

            if (sharedPanelSettings == null)
            {
                return;
            }

            GameObject go = new GameObject("GameOverUI");
            UIDocument doc = go.AddComponent<UIDocument>();
            doc.panelSettings = sharedPanelSettings;
            doc.sortingOrder = 40;
            go.AddComponent<GameOverController>();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindUiElements();
            SubscribeStats();
            HideOverlay();
        }

        private void Start()
        {
            if (!_isSubscribed)
            {
                ResolveReferences();
                SubscribeStats();
            }

            if (playerStats != null && !playerStats.IsAlive)
            {
                ShowGameOver();
            }
        }

        private void OnDisable()
        {
            UnsubscribeStats();
            UnbindUiElements();
        }

        private void OnDestroy()
        {
            if (_isGameOver)
            {
                Time.timeScale = 1f;
            }
        }

        public void ShowGameOver()
        {
            if (_isGameOver)
            {
                return;
            }

            ResolveReferences();
            if (_overlay == null)
            {
                BindUiElements();
            }

            _isGameOver = true;

            if (pauseMenu != null && pauseMenu.IsPaused) pauseMenu.SetPaused(false, restoreCursor: false);
            if (skillCreator != null && skillCreator.IsOpen) skillCreator.SetOpen(false);
            if (dialogueUi != null && dialogueUi.IsOpen) dialogueUi.CloseDialogue();

            Time.timeScale = 0f;

            if (playerController != null) playerController.InputLocked = true;
            if (cameraController != null) cameraController.InputLocked = true;
            if (comboDetector != null) comboDetector.InputLocked = true;
            if (meleeAttack != null) meleeAttack.InputLocked = true;
            if (playerInteractor != null) playerInteractor.InputLocked = true;
            if (skillCreator != null) skillCreator.ExternalLock = true;

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            RefreshLoadSaveButton();
            if (_overlay != null)
            {
                _overlay.RemoveFromClassList(HiddenOverlayClass);
                _overlay.style.display = DisplayStyle.Flex;
            }
        }

        public void OnRetryClicked()
        {
            _isGameOver = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void OnLoadSaveClicked()
        {
            SaveSystem save = FindAnyObjectByType<SaveSystem>();
            if (save == null || !save.HasValidSaveFile())
            {
                RefreshLoadSaveButton();
                return;
            }

            _isGameOver = false;
            Time.timeScale = 1f;
            save.ContinueSavedGame();
        }

        public void OnMainMenuClicked()
        {
            _isGameOver = false;
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        private void HideOverlay()
        {
            _isGameOver = false;
            if (_overlay != null)
            {
                _overlay.AddToClassList(HiddenOverlayClass);
                _overlay.style.display = DisplayStyle.None;
            }
        }

        private void RefreshLoadSaveButton()
        {
            if (_btnLoadSave == null)
            {
                return;
            }

            SaveSystem save = FindAnyObjectByType<SaveSystem>();
            bool hasSave = save != null && save.HasValidSaveFile();
            _btnLoadSave.SetEnabled(hasSave);
            _btnLoadSave.EnableInClassList(DisabledBtnClass, !hasSave);
        }

        private void SubscribeStats()
        {
            if (playerStats == null || _isSubscribed)
            {
                return;
            }

            playerStats.OnDied += ShowGameOver;
            _isSubscribed = true;
        }

        private void UnsubscribeStats()
        {
            if (playerStats != null && _isSubscribed)
            {
                playerStats.OnDied -= ShowGameOver;
            }

            _isSubscribed = false;
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
            _overlay = root.Q<VisualElement>("gameover-overlay");
            if (_overlay == null)
            {
                BuildFallbackUi(root);
            }

            _btnRetry = root.Q<Button>("btn-retry");
            _btnLoadSave = root.Q<Button>("btn-load-save");
            _btnMainMenu = root.Q<Button>("btn-gameover-menu");

            if (_btnRetry != null) _btnRetry.clicked += OnRetryClicked;
            if (_btnLoadSave != null) _btnLoadSave.clicked += OnLoadSaveClicked;
            if (_btnMainMenu != null) _btnMainMenu.clicked += OnMainMenuClicked;
        }

        private void BuildFallbackUi(VisualElement root)
        {
            VisualElement overlay = new VisualElement { name = "gameover-overlay" };
            overlay.AddToClassList("gameover-overlay");
            overlay.AddToClassList(HiddenOverlayClass);
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0.06f, 0.015f, 0.03f, 0.78f);
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.display = DisplayStyle.None;

            VisualElement card = new VisualElement();
            card.style.width = 420;
            card.style.backgroundColor = new Color(0.08f, 0.045f, 0.06f, 0.96f);
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 2;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new Color(0.92f, 0.30f, 0.34f, 0.78f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 14;
            card.style.paddingTop = 28;
            card.style.paddingBottom = 24;
            card.style.paddingLeft = 30;
            card.style.paddingRight = 30;
            card.style.alignItems = Align.Center;

            Label title = new Label("GAME OVER");
            title.style.fontSize = 32;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(1f, 0.42f, 0.45f, 1f);
            title.style.marginBottom = 8;

            Label sub = new Label("HP đã cạn kiệt. Hãy thử lại hoặc quay về bản lưu gần nhất.");
            sub.style.fontSize = 13;
            sub.style.color = new Color(0.82f, 0.76f, 0.78f, 1f);
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            sub.style.marginBottom = 20;

            Button retry = CreateFallbackButton("btn-retry", "Chơi lại màn này", new Color(0.69f, 0.18f, 0.21f, 0.95f));
            Button load = CreateFallbackButton("btn-load-save", "Tải bản lưu gần nhất", new Color(0.14f, 0.14f, 0.20f, 0.95f));
            Button menu = CreateFallbackButton("btn-gameover-menu", "Về Main Menu", new Color(0.18f, 0.12f, 0.16f, 0.95f));
            menu.style.marginBottom = 0;

            card.Add(title);
            card.Add(sub);
            card.Add(retry);
            card.Add(load);
            card.Add(menu);
            overlay.Add(card);
            root.Add(overlay);
            _overlay = overlay;
        }

        private static Button CreateFallbackButton(string btnName, string text, Color bg)
        {
            Button btn = new Button { name = btnName, text = text };
            btn.style.width = Length.Percent(100);
            btn.style.height = 44;
            btn.style.marginBottom = 10;
            btn.style.backgroundColor = bg;
            btn.style.color = Color.white;
            btn.style.fontSize = 15;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 9;
            return btn;
        }

        private void UnbindUiElements()
        {
            if (_btnRetry != null) _btnRetry.clicked -= OnRetryClicked;
            if (_btnLoadSave != null) _btnLoadSave.clicked -= OnLoadSaveClicked;
            if (_btnMainMenu != null) _btnMainMenu.clicked -= OnMainMenuClicked;
        }

        private void ResolveReferences()
        {
            if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
            if (playerController == null) playerController = FindAnyObjectByType<ThirdPersonPlayerController>();
            if (cameraController == null) cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            if (comboDetector == null) comboDetector = FindAnyObjectByType<MovementComboDetector>();
            if (meleeAttack == null) meleeAttack = FindAnyObjectByType<PlayerMeleeAttack>();
            if (playerInteractor == null) playerInteractor = FindAnyObjectByType<PlayerInteractor>();
            if (skillCreator == null) skillCreator = FindAnyObjectByType<SkillCreatorController>();
            if (dialogueUi == null) dialogueUi = FindAnyObjectByType<DialogueUiController>();
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenuController>();
        }
    }
}
