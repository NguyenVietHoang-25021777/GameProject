using UnityEngine;
using GameProject.Skills;

namespace GameProject.World
{
    /// <summary>
    /// Shows or hides a target GameObject (such as the Hidden Secret Manual Pedestal in Zone_AncientRuins)
    /// based on a WorldStateManager key/value or flag condition, reacting live to OnWorldStateChanged.
    /// Place this component on an active parent/controller object so it stays subscribed even while targetObject is hidden.
    /// </summary>
    public class WorldConditionVisibility : MonoBehaviour
    {
        [Header("Condition")]
        [SerializeField] private string requiredKey = "traveler_saved";
        [SerializeField] private int requiredValue = 1;
        [SerializeField] private bool acceptMatchingFlag = true;

        [Header("Target")]
        [SerializeField] private GameObject targetObject;
        [SerializeField] private bool playRevealVfx = true;

        private WorldStateManager _worldState;
        private bool _wasVisible;

        public GameObject TargetObject => targetObject;
        public string RequiredKey => requiredKey;
        public int RequiredValue => requiredValue;

        private void OnEnable()
        {
            Subscribe();
            EvaluateAndApply(allowVfx: false);
        }

        private void Start()
        {
            if (_worldState == null)
            {
                Subscribe();
            }

            EvaluateAndApply(allowVfx: false);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public bool IsConditionMet()
        {
            if (_worldState == null)
            {
                _worldState = WorldStateManager.Resolve();
            }

            if (_worldState == null || string.IsNullOrEmpty(requiredKey))
            {
                return false;
            }

            if (_worldState.GetInt(requiredKey, 0) == requiredValue)
            {
                return true;
            }

            return acceptMatchingFlag && requiredValue == 1 && _worldState.HasFlag(requiredKey);
        }

        public void EvaluateAndApply(bool allowVfx = true)
        {
            if (targetObject == null)
            {
                return;
            }

            bool shouldBeVisible = IsConditionMet();
            bool revealedNow = shouldBeVisible && !_wasVisible && allowVfx;

            if (targetObject.activeSelf != shouldBeVisible)
            {
                targetObject.SetActive(shouldBeVisible);
            }

            _wasVisible = shouldBeVisible;

            if (revealedNow && playRevealVfx)
            {
                SkillVisualEffect.SpawnHighLeapRing(targetObject.transform.position);
            }
        }

        private void Subscribe()
        {
            _worldState = WorldStateManager.Resolve();
            if (_worldState != null)
            {
                _worldState.OnWorldStateChanged -= HandleWorldStateChanged;
                _worldState.OnWorldStateChanged += HandleWorldStateChanged;
            }
        }

        private void Unsubscribe()
        {
            if (_worldState != null)
            {
                _worldState.OnWorldStateChanged -= HandleWorldStateChanged;
            }
        }

        private void HandleWorldStateChanged(string key, int value)
        {
            if (key == requiredKey || key == "_world_state_loaded")
            {
                EvaluateAndApply(allowVfx: key == requiredKey);
            }
        }
    }
}
