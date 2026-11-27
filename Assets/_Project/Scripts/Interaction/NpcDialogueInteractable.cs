using UnityEngine;
using GameProject.UI;

namespace GameProject.Interaction
{
    /// <summary>
    /// Interactable NPC component that opens a branching DialogueData conversation on [E].
    /// </summary>
    public class NpcDialogueInteractable : MonoBehaviour, IInteractable
    {
        [Header("NPC Info")]
        [SerializeField] private string npcDisplayName = "Ẩn Sĩ";
        [SerializeField] private DialogueData dialogueData;
        [SerializeField] private DialogueUiController dialogueUi;

        public string InteractionName => npcDisplayName;
        public bool CanInteract => dialogueData != null && (dialogueUi == null || !dialogueUi.IsOpen);
        public Transform InteractionTransform => transform;

        private void Awake()
        {
            if (dialogueUi == null)
            {
                dialogueUi = FindAnyObjectByType<DialogueUiController>();
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (dialogueData == null)
            {
                return;
            }

            if (dialogueUi == null)
            {
                dialogueUi = FindAnyObjectByType<DialogueUiController>();
            }

            if (interactor != null)
            {
                Vector3 toPlayer = interactor.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                }
            }

            if (dialogueUi != null)
            {
                dialogueUi.StartDialogue(dialogueData);
            }
        }
    }
}
