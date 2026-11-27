using UnityEngine;

namespace GameProject.Interaction
{
    /// <summary>
    /// Shared interface for NPCs, shrines, chests, and world props that Player can interact with using [E].
    /// </summary>
    public interface IInteractable
    {
        string InteractionName { get; }
        bool CanInteract { get; }
        Transform InteractionTransform { get; }
        void Interact(PlayerInteractor interactor);
    }
}
