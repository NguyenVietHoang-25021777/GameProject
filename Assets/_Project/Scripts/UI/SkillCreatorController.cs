using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using GameProject.CameraSystem;
using GameProject.Combat;
using GameProject.Interaction;
using GameProject.Player;
using GameProject.Skills;

namespace GameProject.UI
{
    /// <summary>
    /// Controls the UI Toolkit Skill Creator modal (toggled with K).
    /// Locks camera & player input while open and saves new CustomSkillData directly to MovementComboDetector.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SkillCreatorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MovementComboDetector comboDetector;
        [SerializeField] private ThirdPersonPlayerController playerController;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private PlayerMeleeAttack meleeAttack;
        [SerializeField] private PlayerInteractor playerInteractor;

        private const int MinSteps = 3;
        private const int MaxSteps = 4;
        private const int MaxEffects = 2;

        private UIDocument _uiDocument;
        private VisualElement _overlay;
        private TextField _nameInput;
        private Label _stepPreviewLabel;
        private Label _costPreviewLabel;
        private Label _statusMessage;
        private ScrollView _equippedList;

        private Toggle _toggleLunge;
        private Toggle _toggleShockwave;
        private Toggle _toggleLeap;
        private Toggle _toggleHeal;

        private Button _btnClose;
        private Button _btnUndoStep;
        private Button _btnClearSteps;
        private Button _btnSaveSkill;
        private Button _btnForward;
        private Button _btnBackward;
        private Button _btnLeft;
        private Button _btnRight;
        private Button _btnJump;
        private Button _btnDash;

        private InputAction _toggleMenuAction;
        private InputAction _escapeCloseAction;

        private readonly List<MoveStep> _draftSteps = new List<MoveStep>();
        private bool _isOpen;
        private bool _suppressToggleCallback;

        public bool IsOpen => _isOpen;
        public bool ExternalLock { get; set; }
        public VisualElement Root => _uiDocument != null ? _uiDocument.rootVisualElement : null;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _toggleMenuAction = new InputAction("ToggleSkillCreator", InputActionType.Button, "<Keyboard>/k");
            _escapeCloseAction = new InputAction("CloseSkillCreator", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindUiElements();

            _toggleMenuAction.performed += OnToggleKeyPressed;
            _escapeCloseAction.performed += OnEscapePressed;
            _toggleMenuAction.Enable();
            _escapeCloseAction.Enable();

            if (comboDetector != null)
            {
                comboDetector.OnSkillsListChanged += RefreshEquippedList;
            }

            SetOpen(false);
        }

        private void OnDisable()
        {
            UnbindUiElements();

            if (_toggleMenuAction != null)
            {
                _toggleMenuAction.performed -= OnToggleKeyPressed;
                _toggleMenuAction.Disable();
            }

            if (_escapeCloseAction != null)
            {
                _escapeCloseAction.performed -= OnEscapePressed;
                _escapeCloseAction.Disable();
            }

            if (comboDetector != null)
            {
                comboDetector.OnSkillsListChanged -= RefreshEquippedList;
            }
        }

        private void OnDestroy()
        {
            _toggleMenuAction?.Dispose();
            _escapeCloseAction?.Dispose();
        }

        public void SetOpen(bool open)
        {
            _isOpen = open;

            if (_overlay != null)
            {
                _overlay.EnableInClassList("creator-overlay--hidden", !_isOpen);
            }

            if (playerController != null)
            {
                playerController.InputLocked = _isOpen;
            }

            if (cameraController != null)
            {
                cameraController.InputLocked = _isOpen;
            }

            if (meleeAttack != null)
            {
                meleeAttack.InputLocked = _isOpen;
            }

            if (playerInteractor != null)
            {
                playerInteractor.InputLocked = _isOpen;
            }

            if (comboDetector != null)
            {
                comboDetector.InputLocked = _isOpen;
                if (_isOpen)
                {
                    comboDetector.ClearBuffer();
                    RefreshEquippedList(comboDetector.EquippedSkills);
                }
            }
        }

        public void ToggleOpen()
        {
            SetOpen(!_isOpen);
        }

        private void ResolveReferences()
        {
            if (comboDetector == null)
            {
                comboDetector = FindAnyObjectByType<MovementComboDetector>();
            }

            if (playerController == null)
            {
                playerController = FindAnyObjectByType<ThirdPersonPlayerController>();
            }

            if (cameraController == null)
            {
                cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            }

            if (meleeAttack == null)
            {
                meleeAttack = FindAnyObjectByType<PlayerMeleeAttack>();
            }

            if (playerInteractor == null)
            {
                playerInteractor = FindAnyObjectByType<PlayerInteractor>();
            }
        }

        private void OnToggleKeyPressed(InputAction.CallbackContext ctx)
        {
            if (ExternalLock)
            {
                return;
            }

            // Do not close the modal when the user is typing 'k' inside the skill name TextField
            if (_isOpen && IsTextFieldFocused())
            {
                return;
            }

            ToggleOpen();
        }

        private void OnEscapePressed(InputAction.CallbackContext ctx)
        {
            if (_isOpen)
            {
                SetOpen(false);
            }
        }

        private bool IsTextFieldFocused()
        {
            VisualElement root = Root;
            return root?.focusController?.focusedElement == _nameInput
                || (root?.focusController?.focusedElement is VisualElement el && _nameInput != null && _nameInput.Contains(el));
        }

        private void BindUiElements()
        {
            VisualElement root = Root;
            if (root == null)
            {
                return;
            }

            _overlay = root.Q<VisualElement>("creator-overlay");
            _nameInput = root.Q<TextField>("skill-name-input");
            _stepPreviewLabel = root.Q<Label>("step-preview-label");
            _costPreviewLabel = root.Q<Label>("cost-preview-label");
            _statusMessage = root.Q<Label>("status-message");
            _equippedList = root.Q<ScrollView>("equipped-list");

            _toggleLunge = root.Q<Toggle>("toggle-lunge");
            _toggleShockwave = root.Q<Toggle>("toggle-shockwave");
            _toggleLeap = root.Q<Toggle>("toggle-leap");
            _toggleHeal = root.Q<Toggle>("toggle-heal");

            _btnClose = root.Q<Button>("btn-close");
            _btnUndoStep = root.Q<Button>("btn-undo-step");
            _btnClearSteps = root.Q<Button>("btn-clear-steps");
            _btnSaveSkill = root.Q<Button>("btn-save-skill");

            _btnForward = root.Q<Button>("btn-step-forward");
            _btnBackward = root.Q<Button>("btn-step-backward");
            _btnLeft = root.Q<Button>("btn-step-left");
            _btnRight = root.Q<Button>("btn-step-right");
            _btnJump = root.Q<Button>("btn-step-jump");
            _btnDash = root.Q<Button>("btn-step-dash");

            if (_btnClose != null) _btnClose.clicked += OnCloseClicked;
            if (_btnUndoStep != null) _btnUndoStep.clicked += OnUndoStepClicked;
            if (_btnClearSteps != null) _btnClearSteps.clicked += OnClearStepsClicked;
            if (_btnSaveSkill != null) _btnSaveSkill.clicked += OnSaveSkillClicked;

            if (_btnForward != null) _btnForward.clicked += OnAddForward;
            if (_btnBackward != null) _btnBackward.clicked += OnAddBackward;
            if (_btnLeft != null) _btnLeft.clicked += OnAddLeft;
            if (_btnRight != null) _btnRight.clicked += OnAddRight;
            if (_btnJump != null) _btnJump.clicked += OnAddJump;
            if (_btnDash != null) _btnDash.clicked += OnAddDash;

            RegisterEffectToggle(_toggleLunge);
            RegisterEffectToggle(_toggleShockwave);
            RegisterEffectToggle(_toggleLeap);
            RegisterEffectToggle(_toggleHeal);

            UpdateStepPreview();
            UpdateCostPreview();
        }

        private void UnbindUiElements()
        {
            if (_btnClose != null) _btnClose.clicked -= OnCloseClicked;
            if (_btnUndoStep != null) _btnUndoStep.clicked -= OnUndoStepClicked;
            if (_btnClearSteps != null) _btnClearSteps.clicked -= OnClearStepsClicked;
            if (_btnSaveSkill != null) _btnSaveSkill.clicked -= OnSaveSkillClicked;

            if (_btnForward != null) _btnForward.clicked -= OnAddForward;
            if (_btnBackward != null) _btnBackward.clicked -= OnAddBackward;
            if (_btnLeft != null) _btnLeft.clicked -= OnAddLeft;
            if (_btnRight != null) _btnRight.clicked -= OnAddRight;
            if (_btnJump != null) _btnJump.clicked -= OnAddJump;
            if (_btnDash != null) _btnDash.clicked -= OnAddDash;

            UnregisterEffectToggle(_toggleLunge);
            UnregisterEffectToggle(_toggleShockwave);
            UnregisterEffectToggle(_toggleLeap);
            UnregisterEffectToggle(_toggleHeal);
        }

        private void RegisterEffectToggle(Toggle toggle)
        {
            if (toggle != null)
            {
                toggle.RegisterValueChangedCallback(OnEffectToggleChanged);
            }
        }

        private void UnregisterEffectToggle(Toggle toggle)
        {
            if (toggle != null)
            {
                toggle.UnregisterValueChangedCallback(OnEffectToggleChanged);
            }
        }

        private void OnCloseClicked() => SetOpen(false);
        private void OnAddForward() => AddStep(MoveStep.Forward);
        private void OnAddBackward() => AddStep(MoveStep.Backward);
        private void OnAddLeft() => AddStep(MoveStep.Left);
        private void OnAddRight() => AddStep(MoveStep.Right);
        private void OnAddJump() => AddStep(MoveStep.Jump);
        private void OnAddDash() => AddStep(MoveStep.Dash);

        private void AddStep(MoveStep step)
        {
            if (_draftSteps.Count >= MaxSteps)
            {
                SetStatus("Tối đa 4 bước cho mỗi chiêu thức. Bấm 'Xóa 1 bước' để chỉnh sửa.", isError: true);
                return;
            }

            _draftSteps.Add(step);
            UpdateStepPreview();
            SetStatus($"Đã thêm bước {SkillLabelUtility.GetStepDisplayName(step)} ({_draftSteps.Count}/{MaxSteps}).", isError: false);
        }

        private void OnUndoStepClicked()
        {
            if (_draftSteps.Count > 0)
            {
                _draftSteps.RemoveAt(_draftSteps.Count - 1);
                UpdateStepPreview();
            }
        }

        private void OnClearStepsClicked()
        {
            _draftSteps.Clear();
            UpdateStepPreview();
        }

        private void OnEffectToggleChanged(ChangeEvent<bool> evt)
        {
            if (_suppressToggleCallback)
            {
                return;
            }

            List<SkillEffectType> selected = CollectSelectedEffects();
            if (evt.newValue && selected.Count > MaxEffects && evt.target is Toggle changedToggle)
            {
                _suppressToggleCallback = true;
                changedToggle.value = false;
                _suppressToggleCallback = false;
                SetStatus("Chỉ chọn tối đa 2 hiệu ứng cho mỗi chiêu thức.", isError: true);
                return;
            }

            UpdateCostPreview();
        }

        private void OnSaveSkillClicked()
        {
            string skillName = _nameInput != null ? _nameInput.value.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(skillName))
            {
                SetStatus("Vui lòng nhập tên chiêu thức!", isError: true);
                return;
            }

            if (_draftSteps.Count < MinSteps || _draftSteps.Count > MaxSteps)
            {
                SetStatus($"Cần chọn từ {MinSteps} đến {MaxSteps} bước di chuyển (hiện có {_draftSteps.Count}).", isError: true);
                return;
            }

            List<SkillEffectType> effects = CollectSelectedEffects();
            if (effects.Count < 1 || effects.Count > MaxEffects)
            {
                SetStatus($"Vui lòng chọn 1 đến {MaxEffects} hiệu ứng kết hợp!", isError: true);
                return;
            }

            ResolveReferences();
            if (comboDetector == null)
            {
                SetStatus("Không tìm thấy MovementComboDetector trên Player!", isError: true);
                return;
            }

            float staminaCost = effects.Count == 1 ? 25f : 35f;
            float cooldown = effects.Count == 1 ? 2.5f : 3.5f;

            CustomSkillData newSkill = new CustomSkillData(
                skillName,
                _draftSteps,
                effects,
                staminaCost,
                cooldown);

            comboDetector.AddOrUpdateSkill(newSkill);
            RefreshEquippedList(comboDetector.EquippedSkills);

            SetStatus(
                $"Đã lưu '{newSkill.skillName}' ({newSkill.FormatComboString()})! Nhấn K để đóng và thử ngay.",
                isError: false,
                isSuccess: true);

            _draftSteps.Clear();
            UpdateStepPreview();
        }

        private List<SkillEffectType> CollectSelectedEffects()
        {
            List<SkillEffectType> list = new List<SkillEffectType>();
            if (_toggleLunge != null && _toggleLunge.value) list.Add(SkillEffectType.ForwardLunge);
            if (_toggleShockwave != null && _toggleShockwave.value) list.Add(SkillEffectType.AreaShockwave);
            if (_toggleLeap != null && _toggleLeap.value) list.Add(SkillEffectType.HighLeap);
            if (_toggleHeal != null && _toggleHeal.value) list.Add(SkillEffectType.HealBurst);
            return list;
        }

        private void UpdateStepPreview()
        {
            if (_stepPreviewLabel == null)
            {
                return;
            }

            if (_draftSteps.Count == 0)
            {
                _stepPreviewLabel.text = "Chưa chọn bước nào (chọn 3–4 bước bên dưới)";
                return;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _draftSteps.Count; i++)
            {
                if (i > 0) sb.Append("  →  ");
                sb.Append('[');
                sb.Append(SkillLabelUtility.GetStepShortKey(_draftSteps[i]));
                sb.Append(']');
            }

            _stepPreviewLabel.text = sb.ToString();
        }

        private void UpdateCostPreview()
        {
            if (_costPreviewLabel == null)
            {
                return;
            }

            int count = CollectSelectedEffects().Count;
            float stamina = count <= 1 ? 25f : 35f;
            float cd = count <= 1 ? 2.5f : 3.5f;
            _costPreviewLabel.text = $"Stamina: {stamina:0}  |  Hồi chiêu: {cd:0.1}s";
        }

        private void RefreshEquippedList(IReadOnlyList<CustomSkillData> skills)
        {
            if (_equippedList == null)
            {
                return;
            }

            _equippedList.Clear();

            if (skills == null)
            {
                return;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                CustomSkillData skill = skills[i];
                if (skill == null) continue;

                VisualElement card = new VisualElement();
                card.AddToClassList("equipped-card");

                Label nameLabel = new Label(skill.skillName);
                nameLabel.AddToClassList("equipped-card-name");

                Label comboLabel = new Label(skill.FormatComboString());
                comboLabel.AddToClassList("equipped-card-combo");

                Label metaLabel = new Label($"{skill.FormatEffectsString()}  •  ST {skill.staminaCost:0}  •  CD {skill.cooldown:0.1}s");
                metaLabel.AddToClassList("equipped-card-meta");

                card.Add(nameLabel);
                card.Add(comboLabel);
                card.Add(metaLabel);
                _equippedList.Add(card);
            }
        }

        private void SetStatus(string message, bool isError, bool isSuccess = false)
        {
            if (_statusMessage == null)
            {
                return;
            }

            _statusMessage.text = message;
            _statusMessage.EnableInClassList("status-message--error", isError);
            _statusMessage.EnableInClassList("status-message--success", isSuccess);
        }
    }
}
