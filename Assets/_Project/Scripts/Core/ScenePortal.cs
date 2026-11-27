using UnityEngine;
using GameProject.Interaction;
using GameProject.Player;

namespace GameProject.Core
{
    /// <summary>
    /// Glowing purple scene portal supporting BOTH [E] interaction (IInteractable)
    /// and direct walk-in Trigger activation, automatically saving progress via SaveSystem.
    /// </summary>
    public class ScenePortal : MonoBehaviour, IInteractable
    {
        [Header("Destination")]
        [SerializeField] private string targetSceneName = "DarkForest_Test";
        [SerializeField] private string portalDisplayName = "Cổng Dịch Chuyển → Rừng Tối";
        [SerializeField] private string destinationDisplayName = "Rừng Tối (DarkForest_Test)";
        [SerializeField] private bool allowWalkInTrigger = true;
        [SerializeField] private float entryGraceSeconds = 1.5f;

        [Header("Visual Pulse")]
        [SerializeField] private Transform portalCore;
        [SerializeField] private float pulseSpeed = 2.6f;
        [SerializeField] private float pulseAmplitude = 0.06f;

        private Vector3 _coreBaseScale;
        private float _readyTime;
        private bool _isTransitioning;

        public string InteractionName => portalDisplayName;
        public bool CanInteract => !_isTransitioning && !string.IsNullOrWhiteSpace(targetSceneName);
        public Transform InteractionTransform => transform;

        private void Awake()
        {
            if (portalCore == null)
            {
                portalCore = transform.Find("PortalCore");
            }

            if (portalCore != null)
            {
                _coreBaseScale = portalCore.localScale;
            }
        }

        private void Start()
        {
            _readyTime = Time.time + entryGraceSeconds;
        }

        private void Update()
        {
            if (portalCore != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
                portalCore.localScale = new Vector3(_coreBaseScale.x * pulse, _coreBaseScale.y, _coreBaseScale.z);
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            ActivatePortal();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!allowWalkInTrigger || _isTransitioning || Time.time < _readyTime)
            {
                return;
            }

            if (other.GetComponentInParent<ThirdPersonPlayerController>() == null)
            {
                return;
            }

            ActivatePortal();
        }

        public void ActivatePortal()
        {
            if (_isTransitioning || string.IsNullOrWhiteSpace(targetSceneName))
            {
                return;
            }

            _isTransitioning = true;

            SaveSystem saveSystem = FindAnyObjectByType<SaveSystem>();
            if (saveSystem != null)
            {
                saveSystem.TransitionToScene(targetSceneName, destinationDisplayName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
            }
        }
    }
}
