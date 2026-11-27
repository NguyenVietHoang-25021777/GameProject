using UnityEngine;
using GameProject.Skills;
using GameProject.UI;
using GameProject.World;

namespace GameProject.Interaction
{
    /// <summary>
    /// Hidden glowing shrine revealed when the player earns the Hermit's trust.
    /// Interacting with [E] unlocks a secret 4-step movement skill directly into MovementComboDetector.
    /// </summary>
    public class SecretSkillShrine : MonoBehaviour, IInteractable
    {
        [Header("Shrine Info")]
        [SerializeField] private string shrineName = "Bệ Thưởng Chiêu Thức";
        [SerializeField] private Transform floatingCrystal;
        [SerializeField] private float crystalSpinSpeed = 70f;
        [SerializeField] private float crystalBobAmplitude = 0.12f;

        [Header("Reward Skill")]
        [SerializeField] private string rewardSkillName = "Thiên Lôi Trảm";
        [SerializeField] private float rewardStaminaCost = 30f;
        [SerializeField] private float rewardCooldown = 2.5f;

        private bool _claimed;
        private Vector3 _crystalBaseLocalPos;

        public string InteractionName => shrineName;
        public bool CanInteract => !_claimed && gameObject.activeInHierarchy;
        public Transform InteractionTransform => transform;

        private void Awake()
        {
            if (floatingCrystal == null)
            {
                floatingCrystal = transform.Find("Crystal");
            }

            if (floatingCrystal != null)
            {
                _crystalBaseLocalPos = floatingCrystal.localPosition;
            }
        }

        private void Update()
        {
            if (floatingCrystal != null && !_claimed)
            {
                floatingCrystal.Rotate(Vector3.up, crystalSpinSpeed * Time.deltaTime, Space.Self);
                floatingCrystal.localPosition = _crystalBaseLocalPos + Vector3.up * (Mathf.Sin(Time.time * 2.8f) * crystalBobAmplitude);
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (_claimed)
            {
                return;
            }

            _claimed = true;

            MovementComboDetector detector = interactor != null
                ? interactor.GetComponent<MovementComboDetector>()
                : FindAnyObjectByType<MovementComboDetector>();

            CustomSkillData secretSkill = new CustomSkillData(
                rewardSkillName,
                new[] { MoveStep.Forward, MoveStep.Left, MoveStep.Right, MoveStep.Jump },
                new[] { SkillEffectType.HighLeap, SkillEffectType.AreaShockwave },
                rewardStaminaCost,
                rewardCooldown);

            if (detector != null)
            {
                detector.AddOrUpdateSkill(secretSkill);
            }

            WorldStateManager worldState = FindAnyObjectByType<WorldStateManager>();
            if (worldState != null)
            {
                worldState.SetFlag("secret_skill_unlocked", true);
            }

            HudController hud = FindAnyObjectByType<HudController>();
            if (hud != null)
            {
                hud.ShowCustomBanner(
                    "MỞ KHÓA BÍ KÍP CHIÊU THỨC",
                    secretSkill.skillName.ToUpperInvariant(),
                    $"{secretSkill.FormatComboString()}  |  {secretSkill.FormatEffectsString()}");
            }

            SkillVisualEffect.SpawnHighLeapRing(transform.position);
            SkillVisualEffect.SpawnShockwave(transform.position, 4.0f);

            if (floatingCrystal != null)
            {
                floatingCrystal.gameObject.SetActive(false);
            }
        }
    }
}
