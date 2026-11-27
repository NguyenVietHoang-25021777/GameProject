using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using GameProject.CameraSystem;
using GameProject.Combat;
using GameProject.Interaction;
using GameProject.Player;
using GameProject.Skills;
using GameProject.World;

namespace GameProject.UI
{
    /// <summary>
    /// Controls the UI Toolkit NPC Dialogue panel.
    /// Unlocks mouse cursor and locks movement/camera/combat while conversing,
    /// filters branching choices via WorldStateManager conditions, and applies state mutations on click.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DialogueUiController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldStateManager worldState;
        [SerializeField] private ThirdPersonPlayerController playerController;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private MovementComboDetector comboDetector;
        [SerializeField] private PlayerMeleeAttack meleeAttack;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private SkillCreatorController skillCreator;

        private UIDocument _uiDocument;
        private VisualElement _overlay;
        private Label _speakerLabel;
        private Label _bodyLabel;
        private VisualElement _choicesContainer;

        private InputAction _escapeCloseAction;
        private DialogueData _activeDialogue;
        private DialogueNode _currentNode;
        private readonly List<DialogueChoice> _visibleChoices = new List<DialogueChoice>();
        private bool _isOpen;

        public bool IsOpen => _isOpen;
        public VisualElement Root => _uiDocument != null ? _uiDocument.rootVisualElement : null;
        public IReadOnlyList<DialogueChoice> VisibleChoices => _visibleChoices;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _escapeCloseAction = new InputAction("CloseDialogue", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheUiElements();

            _escapeCloseAction.performed += OnEscapePressed;
            _escapeCloseAction.Enable();

            CloseDialogue();
        }

        private void OnDisable()
        {
            if (_escapeCloseAction != null)
            {
                _escapeCloseAction.performed -= OnEscapePressed;
                _escapeCloseAction.Disable();
            }
        }

        private void OnDestroy()
        {
            _escapeCloseAction?.Dispose();
        }

        public void StartDialogue(DialogueData dialogue)
        {
            if (dialogue == null)
            {
                return;
            }

            ResolveReferences();
            if (_overlay == null)
            {
                CacheUiElements();
            }

            DialogueNode startNode = dialogue.GetNode(dialogue.startNodeId);
            if (startNode == null && dialogue.nodes != null && dialogue.nodes.Count > 0)
            {
                startNode = dialogue.nodes[0];
            }

            if (startNode == null)
            {
                return;
            }

            _activeDialogue = dialogue;
            SetOpenState(true);
            DisplayNode(startNode);
        }

        public void CloseDialogue()
        {
            _activeDialogue = null;
            _currentNode = null;
            _visibleChoices.Clear();
            _choicesContainer?.Clear();
            SetOpenState(false);
        }

        /// <summary>
        /// Programmatically selects a currently visible choice by index (used by buttons and automated tests).
        /// </summary>
        public void SelectChoice(int visibleIndex)
        {
            if (!_isOpen || visibleIndex < 0 || visibleIndex >= _visibleChoices.Count)
            {
                return;
            }

            DialogueChoice choice = _visibleChoices[visibleIndex];

            if (worldState != null && choice.stateEffects != null && choice.stateEffects.Count > 0)
            {
                worldState.ApplyMutations(choice.stateEffects);
            }

            if (string.IsNullOrWhiteSpace(choice.nextNodeId) || _activeDialogue == null)
            {
                CloseDialogue();
                return;
            }

            DialogueNode nextNode = _activeDialogue.GetNode(choice.nextNodeId);
            if (nextNode == null)
            {
                CloseDialogue();
                return;
            }

            DisplayNode(nextNode);
        }

        private void DisplayNode(DialogueNode node)
        {
            _currentNode = node;
            _visibleChoices.Clear();

            string speaker = !string.IsNullOrWhiteSpace(node.speakerOverride)
                ? node.speakerOverride
                : (_activeDialogue != null ? _activeDialogue.speakerName : "NPC");

            if (_speakerLabel != null)
            {
                _speakerLabel.text = speaker.ToUpperInvariant();
            }

            if (_bodyLabel != null)
            {
                _bodyLabel.text = node.bodyText;
            }

            if (_choicesContainer == null)
            {
                return;
            }

            _choicesContainer.Clear();

            if (node.choices != null)
            {
                for (int i = 0; i < node.choices.Count; i++)
                {
                    DialogueChoice choice = node.choices[i];
                    if (choice == null) continue;

                    bool passes = worldState == null || worldState.EvaluateAllConditions(choice.conditions);
                    if (!passes)
                    {
                        continue;
                    }

                    int choiceIndex = _visibleChoices.Count;
                    _visibleChoices.Add(choice);

                    Button btn = new Button(() => SelectChoice(choiceIndex));
                    btn.text = choice.choiceText;
                    btn.AddToClassList("dialogue-choice-btn");
                    _choicesContainer.Add(btn);
                }
            }

            if (_visibleChoices.Count == 0)
            {
                Button closeBtn = new Button(CloseDialogue);
                closeBtn.text = "Kết thúc trò chuyện";
                closeBtn.AddToClassList("dialogue-choice-btn");
                _choicesContainer.Add(closeBtn);
            }
        }

        private void SetOpenState(bool open)
        {
            _isOpen = open;

            if (_overlay != null)
            {
                _overlay.EnableInClassList("dialogue-overlay--hidden", !_isOpen);
            }

            if (playerController != null) playerController.InputLocked = _isOpen;
            if (cameraController != null) cameraController.InputLocked = _isOpen;
            if (comboDetector != null) comboDetector.InputLocked = _isOpen;
            if (meleeAttack != null) meleeAttack.InputLocked = _isOpen;
            if (playerInteractor != null) playerInteractor.InputLocked = _isOpen;
            if (skillCreator != null) skillCreator.ExternalLock = _isOpen;

            if (_isOpen)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        private void OnEscapePressed(InputAction.CallbackContext ctx)
        {
            if (_isOpen)
            {
                CloseDialogue();
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

            _overlay = root.Q<VisualElement>("dialogue-overlay");
            _speakerLabel = root.Q<Label>("speaker-name");
            _bodyLabel = root.Q<Label>("dialogue-body");
            _choicesContainer = root.Q<VisualElement>("choices-container");
        }

        private void ResolveReferences()
        {
            if (worldState == null) worldState = FindAnyObjectByType<WorldStateManager>();
            if (playerController == null) playerController = FindAnyObjectByType<ThirdPersonPlayerController>();
            if (cameraController == null) cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            if (comboDetector == null) comboDetector = FindAnyObjectByType<MovementComboDetector>();
            if (meleeAttack == null) meleeAttack = FindAnyObjectByType<PlayerMeleeAttack>();
            if (playerInteractor == null) playerInteractor = FindAnyObjectByType<PlayerInteractor>();
            if (skillCreator == null) skillCreator = FindAnyObjectByType<SkillCreatorController>();
        }
    }
}
