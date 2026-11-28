using UnityEngine;
using UnityEngine.UIElements;
using GameProject.Core;

namespace GameProject.UI
{
    /// <summary>
    /// Controls the Main Menu UI Toolkit screen (New Game, Continue, Quit),
    /// ensuring Time.timeScale = 1 and cursor unlocked/visible, integrated with SaveSystem.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string startingSceneName = "Zone_Village";
        [SerializeField] private string fallbackStartSceneName = "Sandbox_Test";

        private const string DisabledBtnClass = "menu-btn--disabled";

        private UIDocument _uiDocument;
        private SaveSystem _saveSystem;

        private Button _btnNewGame;
        private Button _btnContinue;
        private Button _btnQuit;
        private Label _saveStatusLabel;
        private bool _canContinue;

        public bool CanContinue => _canContinue;
        public VisualElement Root => _uiDocument != null ? _uiDocument.rootVisualElement : null;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            EnsureMenuState();
        }

        private void OnEnable()
        {
            EnsureMenuState();
            ResolveSaveSystem();
            BindUiElements();
            RefreshContinueButton();
        }

        private void Start()
        {
            EnsureMenuState();
            ResolveSaveSystem();
            if (_btnNewGame == null || _btnContinue == null)
            {
                BindUiElements();
            }

            RefreshContinueButton();
        }

        private void OnDisable()
        {
            UnbindUiElements();
        }

        public void RefreshContinueButton()
        {
            ResolveSaveSystem();
            string savedScene = null;
            _canContinue = _saveSystem != null && _saveSystem.HasValidSaveFile(out savedScene);

            if (_btnContinue != null)
            {
                _btnContinue.SetEnabled(_canContinue);
                _btnContinue.EnableInClassList(DisabledBtnClass, !_canContinue);
            }

            if (_saveStatusLabel != null)
            {
                _saveStatusLabel.text = _canContinue
                    ? $"Bản lưu gần nhất: {FormatSceneDisplayName(savedScene)}"
                    : "Chưa có dữ liệu lưu — Chọn 'Chơi mới' để bắt đầu hành trình.";
            }
        }

        public void OnNewGameClicked()
        {
            EnsureMenuState();
            ResolveSaveSystem();

            if (_saveSystem != null)
            {
                _saveSystem.StartNewGame(startingSceneName, fallbackStartSceneName);
            }
            else
            {
                string target = Application.CanStreamedLevelBeLoaded(startingSceneName)
                    ? startingSceneName
                    : fallbackStartSceneName;
                UnityEngine.SceneManagement.SceneManager.LoadScene(target);
            }
        }

        public void OnContinueClicked()
        {
            ResolveSaveSystem();
            if (_saveSystem == null || !_saveSystem.HasValidSaveFile())
            {
                RefreshContinueButton();
                return;
            }

            EnsureMenuState();
            _saveSystem.ContinueSavedGame();
        }

        public void OnQuitClicked()
        {
            Time.timeScale = 1f;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static void EnsureMenuState()
        {
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void ResolveSaveSystem()
        {
            if (_saveSystem == null)
            {
                _saveSystem = FindAnyObjectByType<SaveSystem>();
            }

            if (_saveSystem == null)
            {
                GameObject saveObj = new GameObject("SaveSystem");
                _saveSystem = saveObj.AddComponent<SaveSystem>();
            }
        }

        private void BindUiElements()
        {
            UnbindUiElements();

            VisualElement root = Root;
            if (root == null)
            {
                return;
            }

            _btnNewGame = root.Q<Button>("btn-new-game");
            _btnContinue = root.Q<Button>("btn-continue");
            _btnQuit = root.Q<Button>("btn-quit");
            _saveStatusLabel = root.Q<Label>("save-status-label");

            if (_btnNewGame != null) _btnNewGame.clicked += OnNewGameClicked;
            if (_btnContinue != null) _btnContinue.clicked += OnContinueClicked;
            if (_btnQuit != null) _btnQuit.clicked += OnQuitClicked;
        }

        private void UnbindUiElements()
        {
            if (_btnNewGame != null) _btnNewGame.clicked -= OnNewGameClicked;
            if (_btnContinue != null) _btnContinue.clicked -= OnContinueClicked;
            if (_btnQuit != null) _btnQuit.clicked -= OnQuitClicked;
        }

        private static string FormatSceneDisplayName(string sceneName)
        {
            switch (sceneName)
            {
                case "Zone_Village": return "Làng Khởi Đầu (Zone_Village)";
                case "Zone_MistyForest": return "Rừng Sương Mù (Zone_MistyForest)";
                case "Zone_AncientRuins": return "Di Tích Cổ Đại (Zone_AncientRuins)";
                case "Sandbox_Test": return "Sân Thử Nghiệm (Sandbox_Test)";
                case "DarkForest_Test": return "Rừng Tối (DarkForest_Test)";
                default: return sceneName;
            }
        }
    }
}
