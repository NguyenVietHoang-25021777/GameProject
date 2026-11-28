using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using GameProject.Interaction;
using GameProject.Quests;
using GameProject.Skills;
using GameProject.Stats;

namespace GameProject.UI
{
    /// <summary>
    /// Binds PlayerStats (HP/Stamina), MovementComboDetector (skill banner, live combo buffer,
    /// equipped skill list), PlayerInteractor ([E] prompt), and QuestTrackerManager (Quest Tracker)
    /// to the UI Toolkit HUD (UIDocument).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HudController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private MovementComboDetector comboDetector;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private QuestTrackerManager questTracker;

        [Header("Skill Banner")]
        [SerializeField] private float bannerDisplayDuration = 2.6f;

        private const string ExhaustedClassName = "bar-fill--exhausted";
        private const string LowHealthClassName = "bar-fill--low";
        private const string BannerHiddenClass = "skill-banner--hidden";
        private const string BannerWarningClass = "skill-banner--warning";
        private const string KickerWarningClass = "skill-banner-kicker--warning";
        private const string BufferHiddenClass = "combo-buffer-bar--hidden";
        private const string PromptHiddenClass = "interact-prompt--hidden";

        private UIDocument _uiDocument;
        private VisualElement _hpFill;
        private Label _hpValueLabel;
        private VisualElement _staminaFill;
        private Label _staminaValueLabel;

        private VisualElement _skillsLegendList;
        private VisualElement _questPanel;
        private Label _questCategory;
        private Label _questTitle;
        private Label _questDesc;

        private VisualElement _skillBanner;
        private Label _skillBannerKicker;
        private Label _skillBannerName;
        private Label _skillBannerDetail;
        private VisualElement _interactPrompt;
        private Label _interactPromptText;
        private VisualElement _comboBufferBar;
        private Label _comboBufferText;

        private bool _isStatsSubscribed;
        private bool _isSkillsSubscribed;
        private bool _isInteractorSubscribed;
        private bool _isQuestSubscribed;
        private float _bannerTimer;

        public VisualElement Root => _uiDocument != null ? _uiDocument.rootVisualElement : null;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            CacheUiElements();
            BindPlayerStats();
            BindComboDetector();
            BindInteractor();
            BindQuestTracker();
        }

        private void Start()
        {
            if (!_isStatsSubscribed) BindPlayerStats();
            else if (playerStats != null)
            {
                SetHealth(playerStats.CurrentHealth, playerStats.MaxHealth);
                SetStamina(playerStats.CurrentStamina, playerStats.MaxStamina);
            }

            if (!_isSkillsSubscribed) BindComboDetector();
            else if (comboDetector != null) RefreshSkillsLegend(comboDetector.EquippedSkills);

            if (!_isInteractorSubscribed) BindInteractor();
            if (!_isQuestSubscribed) BindQuestTracker();
            else if (questTracker != null) UpdateQuestDisplay(questTracker.CurrentQuest);
        }

        private void OnDisable()
        {
            UnbindPlayerStats();
            UnbindComboDetector();
            UnbindInteractor();
            UnbindQuestTracker();
        }

        private void Update()
        {
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.deltaTime;
                if (_bannerTimer <= 0f && _skillBanner != null)
                {
                    _skillBanner.AddToClassList(BannerHiddenClass);
                }
            }
        }

        public void SetHealth(float current, float max)
        {
            float safeMax = Mathf.Max(1f, max);
            float clamped = Mathf.Clamp(current, 0f, safeMax);
            float percent = (clamped / safeMax) * 100f;

            if (_hpFill != null)
            {
                _hpFill.style.width = Length.Percent(percent);
                bool isLow = (clamped / safeMax) <= 0.25f;
                _hpFill.EnableInClassList(LowHealthClassName, isLow);
            }

            if (_hpValueLabel != null)
            {
                _hpValueLabel.text = $"{Mathf.CeilToInt(clamped)} / {Mathf.CeilToInt(safeMax)}";
            }
        }

        public void SetStamina(float current, float max)
        {
            float safeMax = Mathf.Max(1f, max);
            float clamped = Mathf.Clamp(current, 0f, safeMax);
            float percent = (clamped / safeMax) * 100f;

            if (_staminaFill != null)
            {
                _staminaFill.style.width = Length.Percent(percent);
                bool exhausted = playerStats != null && playerStats.IsExhausted;
                _staminaFill.EnableInClassList(ExhaustedClassName, exhausted);
            }

            if (_staminaValueLabel != null)
            {
                _staminaValueLabel.text = $"{Mathf.CeilToInt(clamped)} / {Mathf.CeilToInt(safeMax)}";
            }
        }

        public void ShowCustomBanner(string kicker, string title, string detail, float duration = 3.2f)
        {
            if (_skillBanner == null)
            {
                return;
            }

            _skillBanner.RemoveFromClassList(BannerHiddenClass);
            _skillBanner.RemoveFromClassList(BannerWarningClass);

            if (_skillBannerKicker != null)
            {
                _skillBannerKicker.RemoveFromClassList(KickerWarningClass);
                _skillBannerKicker.text = kicker;
            }

            if (_skillBannerName != null) _skillBannerName.text = title;
            if (_skillBannerDetail != null) _skillBannerDetail.text = detail;

            _bannerTimer = duration;
        }

        public void ShowSkillActivated(CustomSkillData skill)
        {
            if (skill == null)
            {
                return;
            }

            ShowCustomBanner(
                "KÍCH HOẠT CHIÊU THỨC",
                skill.skillName.ToUpperInvariant(),
                $"{skill.FormatComboString()}  |  {skill.FormatEffectsString()}",
                bannerDisplayDuration);
        }

        public void ShowSkillFailed(CustomSkillData skill, string reason)
        {
            if (skill == null || _skillBanner == null)
            {
                return;
            }

            _skillBanner.RemoveFromClassList(BannerHiddenClass);
            _skillBanner.AddToClassList(BannerWarningClass);

            if (_skillBannerKicker != null)
            {
                _skillBannerKicker.AddToClassList(KickerWarningClass);
                _skillBannerKicker.text = "KHÔNG THỂ KÍCH HOẠT";
            }

            if (_skillBannerName != null) _skillBannerName.text = skill.skillName.ToUpperInvariant();
            if (_skillBannerDetail != null) _skillBannerDetail.text = reason;

            _bannerTimer = 1.6f;
        }

        public void UpdateInteractPrompt(IInteractable target)
        {
            if (_interactPrompt == null || _interactPromptText == null)
            {
                return;
            }

            if (target == null || !target.CanInteract)
            {
                _interactPrompt.AddToClassList(PromptHiddenClass);
                return;
            }

            _interactPromptText.text = $"Nhấn [E] để tương tác với {target.InteractionName}";
            _interactPrompt.RemoveFromClassList(PromptHiddenClass);
        }

        public void UpdateQuestDisplay(QuestDisplayState quest)
        {
            if (_questCategory != null)
            {
                _questCategory.text = quest.CategoryLabel;
                _questCategory.EnableInClassList("quest-category--secret", quest.Tone == QuestTone.Secret);
                _questCategory.EnableInClassList("quest-category--danger", quest.Tone == QuestTone.Danger);
                _questCategory.EnableInClassList("quest-category--completed", quest.Tone == QuestTone.Completed);
            }

            if (_questPanel != null)
            {
                _questPanel.EnableInClassList("quest-panel--secret", quest.Tone == QuestTone.Secret);
                _questPanel.EnableInClassList("quest-panel--danger", quest.Tone == QuestTone.Danger);
                _questPanel.EnableInClassList("quest-panel--completed", quest.Tone == QuestTone.Completed);
            }

            if (_questTitle != null) _questTitle.text = quest.Title;
            if (_questDesc != null) _questDesc.text = quest.Description;
        }

        public void UpdateComboBufferDisplay(IReadOnlyList<MoveStep> steps)
        {
            if (_comboBufferBar == null || _comboBufferText == null)
            {
                return;
            }

            if (steps == null || steps.Count == 0)
            {
                _comboBufferBar.AddToClassList(BufferHiddenClass);
                _comboBufferText.text = string.Empty;
                return;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < steps.Count; i++)
            {
                if (i > 0) sb.Append("  →  ");
                sb.Append('[');
                sb.Append(SkillLabelUtility.GetStepShortKey(steps[i]));
                sb.Append(']');
            }

            _comboBufferText.text = sb.ToString();
            _comboBufferBar.RemoveFromClassList(BufferHiddenClass);
        }

        public void RefreshSkillsLegend(IReadOnlyList<CustomSkillData> skills)
        {
            if (_skillsLegendList == null)
            {
                return;
            }

            _skillsLegendList.Clear();

            if (skills == null)
            {
                return;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                CustomSkillData skill = skills[i];
                if (skill == null) continue;

                VisualElement row = new VisualElement();
                row.AddToClassList("legend-item");
                row.pickingMode = PickingMode.Ignore;

                Label nameLabel = new Label(skill.skillName);
                nameLabel.AddToClassList("legend-item-name");
                nameLabel.pickingMode = PickingMode.Ignore;

                Label comboLabel = new Label(skill.FormatComboString());
                comboLabel.AddToClassList("legend-item-combo");
                comboLabel.pickingMode = PickingMode.Ignore;

                row.Add(nameLabel);
                row.Add(comboLabel);
                _skillsLegendList.Add(row);
            }
        }

        private void CacheUiElements()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            VisualElement root = Root;
            if (root == null)
            {
                return;
            }

            root.pickingMode = PickingMode.Ignore;

            root.pickingMode = PickingMode.Ignore;

            _hpFill = root.Q<VisualElement>("hp-fill");
            _hpValueLabel = root.Q<Label>("hp-value");
            _staminaFill = root.Q<VisualElement>("stamina-fill");
            _staminaValueLabel = root.Q<Label>("stamina-value");

            _skillsLegendList = root.Q<VisualElement>("skills-legend-list");
            _questPanel = root.Q<VisualElement>("quest-panel");
            _questCategory = root.Q<Label>("quest-category");
            _questTitle = root.Q<Label>("quest-title");
            _questDesc = root.Q<Label>("quest-desc");

            _skillBanner = root.Q<VisualElement>("skill-banner");
            _skillBannerKicker = root.Q<Label>("skill-banner-kicker");
            _skillBannerName = root.Q<Label>("skill-banner-name");
            _skillBannerDetail = root.Q<Label>("skill-banner-detail");

            _interactPrompt = root.Q<VisualElement>("interact-prompt");
            _interactPromptText = root.Q<Label>("interact-prompt-text");
            _comboBufferBar = root.Q<VisualElement>("combo-buffer-bar");
            _comboBufferText = root.Q<Label>("combo-buffer-text");
        }

        private void BindPlayerStats()
        {
            if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
            if (playerStats == null || _isStatsSubscribed) return;

            playerStats.OnHealthChanged += SetHealth;
            playerStats.OnStaminaChanged += SetStamina;
            _isStatsSubscribed = true;

            SetHealth(playerStats.CurrentHealth, playerStats.MaxHealth);
            SetStamina(playerStats.CurrentStamina, playerStats.MaxStamina);
        }

        private void UnbindPlayerStats()
        {
            if (playerStats != null && _isStatsSubscribed)
            {
                playerStats.OnHealthChanged -= SetHealth;
                playerStats.OnStaminaChanged -= SetStamina;
            }
            _isStatsSubscribed = false;
        }

        private void BindComboDetector()
        {
            if (comboDetector == null) comboDetector = FindAnyObjectByType<MovementComboDetector>();
            if (comboDetector == null || _isSkillsSubscribed) return;

            comboDetector.OnSkillActivated += ShowSkillActivated;
            comboDetector.OnSkillFailed += ShowSkillFailed;
            comboDetector.OnComboBufferChanged += UpdateComboBufferDisplay;
            comboDetector.OnSkillsListChanged += RefreshSkillsLegend;
            _isSkillsSubscribed = true;

            RefreshSkillsLegend(comboDetector.EquippedSkills);
            UpdateComboBufferDisplay(comboDetector.CurrentBuffer);
        }

        private void UnbindComboDetector()
        {
            if (comboDetector != null && _isSkillsSubscribed)
            {
                comboDetector.OnSkillActivated -= ShowSkillActivated;
                comboDetector.OnSkillFailed -= ShowSkillFailed;
                comboDetector.OnComboBufferChanged -= UpdateComboBufferDisplay;
                comboDetector.OnSkillsListChanged -= RefreshSkillsLegend;
            }
            _isSkillsSubscribed = false;
        }

        private void BindInteractor()
        {
            if (playerInteractor == null) playerInteractor = FindAnyObjectByType<PlayerInteractor>();
            if (playerInteractor == null || _isInteractorSubscribed) return;

            playerInteractor.OnFocusTargetChanged += UpdateInteractPrompt;
            _isInteractorSubscribed = true;
            UpdateInteractPrompt(playerInteractor.CurrentTarget);
        }

        private void UnbindInteractor()
        {
            if (playerInteractor != null && _isInteractorSubscribed)
            {
                playerInteractor.OnFocusTargetChanged -= UpdateInteractPrompt;
            }
            _isInteractorSubscribed = false;
        }

        private void BindQuestTracker()
        {
            if (questTracker == null) questTracker = FindAnyObjectByType<QuestTrackerManager>();
            if (questTracker == null || _isQuestSubscribed) return;

            questTracker.OnQuestUpdated += UpdateQuestDisplay;
            _isQuestSubscribed = true;
            UpdateQuestDisplay(questTracker.CurrentQuest);
        }

        private void UnbindQuestTracker()
        {
            if (questTracker != null && _isQuestSubscribed)
            {
                questTracker.OnQuestUpdated -= UpdateQuestDisplay;
            }
            _isQuestSubscribed = false;
        }
    }
}
