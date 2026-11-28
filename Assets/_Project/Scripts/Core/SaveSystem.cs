using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using GameProject.Skills;
using GameProject.Stats;
using GameProject.UI;
using GameProject.World;

namespace GameProject.Core
{
    [Serializable]
    public class GameSaveData
    {
        public string sceneName;
        public float currentHealth = 100f;
        public float currentStamina = 100f;
        public List<CustomSkillData> equippedSkills = new List<CustomSkillData>();
        public List<SkillEffectType> unlockedEffects = new List<SkillEffectType>();
        public List<IntStateEntry> worldIntStates = new List<IntStateEntry>();
        public List<string> worldFlags = new List<string>();
    }

    /// <summary>
    /// Persistent JSON Save/Load and cross-scene state manager.
    /// Survives scene loads via DontDestroyOnLoad, supports F5 Quick Save / F9 Quick Load,
    /// and automatically saves & restores PlayerStats, CustomSkillData, and WorldStateManager across portals.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        [Header("Save File")]
        [SerializeField] private string saveFileName = "gameproject_save.json";

        private InputAction _quickSaveAction;
        private InputAction _quickLoadAction;

        private GameSaveData _pendingSceneRestore;
        private string _pendingToastTitle;
        private string _pendingToastDetail;
        private bool _isPersistentInstance;

        public string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

        private void Awake()
        {
            // Keep a single persistent SaveSystem across scene loads
            SaveSystem[] all = FindObjectsByType<SaveSystem>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != this && all[i]._isPersistentInstance)
                {
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                    return;
                }
            }

            _isPersistentInstance = true;
            DontDestroyOnLoad(gameObject);

            _quickSaveAction = new InputAction("QuickSave", InputActionType.Button, "<Keyboard>/f5");
            _quickLoadAction = new InputAction("QuickLoad", InputActionType.Button, "<Keyboard>/f9");
        }

        private void OnEnable()
        {
            if (!_isPersistentInstance)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

            if (_quickSaveAction != null)
            {
                _quickSaveAction.performed += OnQuickSavePerformed;
                _quickSaveAction.Enable();
            }

            if (_quickLoadAction != null)
            {
                _quickLoadAction.performed += OnQuickLoadPerformed;
                _quickLoadAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (!_isPersistentInstance)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_quickSaveAction != null)
            {
                _quickSaveAction.performed -= OnQuickSavePerformed;
                _quickSaveAction.Disable();
            }

            if (_quickLoadAction != null)
            {
                _quickLoadAction.performed -= OnQuickLoadPerformed;
                _quickLoadAction.Disable();
            }
        }

        private void OnDestroy()
        {
            _quickSaveAction?.Dispose();
            _quickLoadAction?.Dispose();
        }

        private void OnQuickSavePerformed(InputAction.CallbackContext ctx)
        {
            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                return;
            }

            SaveGame(showNotification: true);
        }

        private void OnQuickLoadPerformed(InputAction.CallbackContext ctx)
        {
            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                return;
            }

            LoadGame(showNotification: true);
        }

        public bool HasValidSaveFile()
        {
            return HasValidSaveFile(out _);
        }

        public bool HasValidSaveFile(out string savedSceneName)
        {
            savedSceneName = null;
            if (!File.Exists(SaveFilePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null || string.IsNullOrWhiteSpace(data.sceneName) || data.sceneName == "MainMenu")
                {
                    return false;
                }

                if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
                {
                    return false;
                }

                savedSceneName = data.sceneName;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteSaveFile()
        {
            _pendingSceneRestore = null;
            _pendingToastTitle = null;
            _pendingToastDetail = null;

            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to delete save file: {ex.Message}");
                return false;
            }
        }

        public void StartNewGame(string preferredStartScene = "Zone_Village", string fallbackStartScene = "Sandbox_Test")
        {
            DeleteSaveFile();

            WorldStateManager worldState = WorldStateManager.Resolve();
            if (worldState != null)
            {
                worldState.ResetState();
            }

            Time.timeScale = 1f;

            string targetScene = preferredStartScene;
            if (string.IsNullOrWhiteSpace(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
            {
                targetScene = fallbackStartScene;
            }

            if (!string.IsNullOrWhiteSpace(targetScene) && Application.CanStreamedLevelBeLoaded(targetScene))
            {
                SceneManager.LoadScene(targetScene);
            }
            else
            {
                Debug.LogError($"[SaveSystem] Cannot start new game: neither '{preferredStartScene}' nor '{fallbackStartScene}' is in Build Settings.");
            }
        }

        public bool ContinueSavedGame()
        {
            if (!HasValidSaveFile())
            {
                return false;
            }

            Time.timeScale = 1f;
            return LoadGame(showNotification: true);
        }

        /// <summary>
        /// Captures current PlayerStats, MovementComboDetector skills, and WorldStateManager into JSON.
        /// </summary>
        public bool SaveGame(bool showNotification = true, string overrideSceneName = null)
        {
            GameSaveData data = CaptureCurrentState(overrideSceneName);
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string dir = Path.GetDirectoryName(SaveFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(SaveFilePath, json);

                if (showNotification)
                {
                    ShowHudToast("HỆ THỐNG LƯU TRỮ", "ĐÃ LƯU GAME", $"F5 Lưu nhanh  •  {data.equippedSkills.Count} chiêu thức");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to save JSON: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads saved JSON from disk and applies it to the current scene (or switches to the saved scene).
        /// </summary>
        public bool LoadGame(bool showNotification = true)
        {
            if (!File.Exists(SaveFilePath))
            {
                if (showNotification)
                {
                    ShowHudToast("HỆ THỐNG LƯU TRỮ", "CHƯA CÓ BẢN LƯU", "Nhấn F5 để lưu game trước khi tải lại.");
                }
                return false;
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null)
                {
                    return false;
                }

                string activeScene = SceneManager.GetActiveScene().name;
                if (!string.IsNullOrWhiteSpace(data.sceneName) && data.sceneName != activeScene)
                {
                    _pendingSceneRestore = data;
                    _pendingToastTitle = showNotification ? "ĐÃ TẢI LẠI GAME" : null;
                    _pendingToastDetail = showNotification ? $"F9 Tải nhanh  •  Khu vực: {data.sceneName}" : null;
                    SceneManager.LoadScene(data.sceneName);
                    return true;
                }

                ApplyStateToCurrentScene(data);

                if (showNotification)
                {
                    ShowHudToast("HỆ THỐNG LƯU TRỮ", "ĐÃ TẢI LẠI GAME", $"F9 Tải nhanh  •  HP {Mathf.CeilToInt(data.currentHealth)} | {data.equippedSkills.Count} chiêu thức");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to load JSON: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Auto-saves current progress to disk & memory, then loads targetSceneName.
        /// </summary>
        public void TransitionToScene(string targetSceneName, string destinationLabel = null)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                return;
            }

            GameSaveData snapshot = CaptureCurrentState(overrideSceneName: targetSceneName);
            SaveGame(showNotification: false, overrideSceneName: targetSceneName);

            _pendingSceneRestore = snapshot;
            _pendingToastTitle = "ĐÃ LƯU & CHUYỂN VÙNG";
            _pendingToastDetail = string.IsNullOrWhiteSpace(destinationLabel)
                ? $"Khu vực hiện tại: {targetSceneName}"
                : $"Khu vực hiện tại: {destinationLabel}";

            SceneManager.LoadScene(targetSceneName);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_pendingSceneRestore == null)
            {
                return;
            }

            GameSaveData dataToApply = _pendingSceneRestore;
            string toastTitle = _pendingToastTitle;
            string toastDetail = _pendingToastDetail;

            _pendingSceneRestore = null;
            _pendingToastTitle = null;
            _pendingToastDetail = null;

            ApplyStateToCurrentScene(dataToApply);

            if (!string.IsNullOrWhiteSpace(toastTitle))
            {
                ShowHudToast("HỆ THỐNG LƯU TRỮ", toastTitle, toastDetail);
            }
        }

        private GameSaveData CaptureCurrentState(string overrideSceneName = null)
        {
            GameSaveData data = new GameSaveData
            {
                sceneName = !string.IsNullOrWhiteSpace(overrideSceneName)
                    ? overrideSceneName
                    : SceneManager.GetActiveScene().name
            };

            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null)
            {
                data.currentHealth = stats.CurrentHealth;
                data.currentStamina = stats.CurrentStamina;
            }

            MovementComboDetector detector = FindAnyObjectByType<MovementComboDetector>();
            if (detector != null)
            {
                data.equippedSkills = detector.ExportSkillsCopy();
                data.unlockedEffects = detector.ExportUnlockedEffectsCopy();
            }

            WorldStateManager worldState = WorldStateManager.Resolve();
            if (worldState != null)
            {
                worldState.ExportState(out data.worldIntStates, out data.worldFlags);
            }

            return data;
        }

        private void ApplyStateToCurrentScene(GameSaveData data)
        {
            if (data == null)
            {
                return;
            }

            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null)
            {
                stats.RestoreValues(data.currentHealth, data.currentStamina);
            }

            MovementComboDetector detector = FindAnyObjectByType<MovementComboDetector>();
            if (detector != null)
            {
                if (data.unlockedEffects != null && data.unlockedEffects.Count > 0)
                {
                    detector.ImportUnlockedEffects(data.unlockedEffects);
                }

                if (data.equippedSkills != null && data.equippedSkills.Count > 0)
                {
                    detector.ImportSkills(data.equippedSkills);
                }
            }

            WorldStateManager worldState = WorldStateManager.Resolve();
            if (worldState != null)
            {
                worldState.ImportState(data.worldIntStates, data.worldFlags, notifyListeners: true);
            }
        }

        private static void ShowHudToast(string kicker, string title, string detail)
        {
            HudController hud = FindAnyObjectByType<HudController>();
            if (hud != null)
            {
                hud.ShowCustomBanner(kicker, title, detail, 2.5f);
            }
        }
    }
}
