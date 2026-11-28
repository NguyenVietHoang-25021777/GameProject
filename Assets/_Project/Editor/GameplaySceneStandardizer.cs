#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using GameProject.CameraSystem;
using GameProject.Combat;
using GameProject.Interaction;
using GameProject.Player;
using GameProject.Quests;
using GameProject.Skills;
using GameProject.Stats;
using GameProject.UI;
using GameProject.World;

namespace GameProject.EditorTools
{
    /// <summary>
    /// One-click Editor utility to verify and standardize all gameplay scenes:
    /// ensures HUD, SkillCreatorUI, DialogueUI, PauseMenuUI, and GameOverUI all have valid
    /// HudPanelSettings, UXML assets, sortingOrder, and live component references.
    /// </summary>
    public static class GameplaySceneStandardizer
    {
        private static readonly string[] GameplayScenes =
        {
            "Assets/_Project/Scenes/Zone_Village.unity",
            "Assets/_Project/Scenes/Zone_MistyForest.unity",
            "Assets/_Project/Scenes/Zone_AncientRuins.unity",
            "Assets/_Project/Scenes/Sandbox_Test.unity",
            "Assets/_Project/Scenes/DarkForest_Test.unity"
        };

        [MenuItem("Tools/GameProject/Standardize All Gameplay Scenes")]
        public static void StandardizeAllGameplayScenes()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GameplaySceneStandardizer] Exit Play Mode before standardizing scenes.");
                return;
            }

            string originalScenePath = SceneManager.GetActiveScene().path;
            int updatedCount = 0;

            foreach (string scenePath in GameplayScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // Always load UI Toolkit assets AFTER OpenScene so Unity never unloads them
                PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/UI/HudPanelSettings.asset");
                VisualTreeAsset hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/PlayerHud.uxml");
                VisualTreeAsset skillUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/SkillCreator.uxml");
                VisualTreeAsset dlgUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/DialoguePanel.uxml");
                VisualTreeAsset pauseUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/PauseMenu.uxml");
                VisualTreeAsset gameOverUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/GameOver.uxml");

                EnsureUiDoc<HudController>("HUD", panelSettings, hudUxml, 0);
                EnsureUiDoc<SkillCreatorController>("SkillCreatorUI", panelSettings, skillUxml, 10);
                EnsureUiDoc<DialogueUiController>("DialogueUI", panelSettings, dlgUxml, 20);
                EnsureUiDoc<PauseMenuController>("PauseMenuUI", panelSettings, pauseUxml, 30);
                GameOverController gameOverCtrl = EnsureUiDoc<GameOverController>("GameOverUI", panelSettings, gameOverUxml, 40);

                if (gameOverCtrl != null)
                {
                    SerializedObject goSo = new SerializedObject(gameOverCtrl);
                    goSo.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
                    goSo.FindProperty("playerStats").objectReferenceValue = Object.FindAnyObjectByType<PlayerStats>();
                    goSo.FindProperty("playerController").objectReferenceValue = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
                    goSo.FindProperty("cameraController").objectReferenceValue = Object.FindAnyObjectByType<ThirdPersonCameraController>();
                    goSo.FindProperty("comboDetector").objectReferenceValue = Object.FindAnyObjectByType<MovementComboDetector>();
                    goSo.FindProperty("meleeAttack").objectReferenceValue = Object.FindAnyObjectByType<PlayerMeleeAttack>();
                    goSo.FindProperty("playerInteractor").objectReferenceValue = Object.FindAnyObjectByType<PlayerInteractor>();
                    goSo.FindProperty("skillCreator").objectReferenceValue = Object.FindAnyObjectByType<SkillCreatorController>();
                    goSo.FindProperty("dialogueUi").objectReferenceValue = Object.FindAnyObjectByType<DialogueUiController>();
                    goSo.FindProperty("pauseMenu").objectReferenceValue = Object.FindAnyObjectByType<PauseMenuController>();
                    goSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(gameOverCtrl);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                updatedCount++;
            }

            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }

            Debug.Log($"[GameplaySceneStandardizer] Standardized {updatedCount} gameplay scenes with full UI Toolkit stack.");
        }

        private static T EnsureUiDoc<T>(string objName, PanelSettings ps, VisualTreeAsset uxml, float sortOrder) where T : Component
        {
            T existing = Object.FindAnyObjectByType<T>();
            GameObject obj = existing != null ? existing.gameObject : GameObject.Find(objName);
            if (obj == null)
            {
                obj = new GameObject(objName);
            }

            UIDocument doc = obj.GetComponent<UIDocument>();
            if (doc == null) doc = obj.AddComponent<UIDocument>();
            if (ps != null) doc.panelSettings = ps;
            if (uxml != null) doc.visualTreeAsset = uxml;
            doc.sortingOrder = sortOrder;
            EditorUtility.SetDirty(doc);

            T comp = obj.GetComponent<T>();
            if (comp == null) comp = obj.AddComponent<T>();
            EditorUtility.SetDirty(comp);
            return comp;
        }
    }
}
#endif
