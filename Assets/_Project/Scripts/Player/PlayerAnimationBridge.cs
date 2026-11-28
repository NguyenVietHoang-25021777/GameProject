using UnityEngine;
using GameProject.Combat;
using GameProject.Skills;

namespace GameProject.Player
{
    /// <summary>
    /// Bridges ThirdPersonPlayerController, PlayerMeleeAttack, and MovementComboDetector
    /// to the child 3D character's Animator (Player_Animator.controller).
    /// </summary>
    public class PlayerAnimationBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private ThirdPersonPlayerController playerController;
        [SerializeField] private PlayerMeleeAttack meleeAttack;
        [SerializeField] private MovementComboDetector comboDetector;

        [Header("Tuning")]
        [SerializeField] private float maxReferenceSpeed = 8.5f;
        [SerializeField] private float speedDampTime = 0.08f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int SkillHash = Animator.StringToHash("Skill");

        public Animator TargetAnimator => animator;

        private void Awake()
        {
            ResolveReferences();

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (meleeAttack != null)
            {
                meleeAttack.OnAttackPerformed += HandleBasicAttack;
            }

            if (comboDetector != null)
            {
                comboDetector.OnSkillActivated += HandleSkillActivated;
            }
        }

        private void OnDisable()
        {
            if (meleeAttack != null)
            {
                meleeAttack.OnAttackPerformed -= HandleBasicAttack;
            }

            if (comboDetector != null)
            {
                comboDetector.OnSkillActivated -= HandleSkillActivated;
            }
        }

        private void Update()
        {
            if (animator == null || playerController == null)
            {
                return;
            }

            float normalizedSpeed = maxReferenceSpeed > 0.01f
                ? Mathf.Clamp01(playerController.CurrentHorizontalSpeed / maxReferenceSpeed)
                : 0f;

            animator.SetFloat(SpeedHash, normalizedSpeed, speedDampTime, Time.deltaTime);
            animator.SetBool(IsGroundedHash, playerController.IsGrounded);
        }

        private void HandleBasicAttack()
        {
            if (animator != null)
            {
                animator.SetTrigger(AttackHash);
            }
        }

        private void HandleSkillActivated(CustomSkillData skill)
        {
            if (animator != null)
            {
                animator.SetTrigger(SkillHash);
            }
        }

        private void ResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<ThirdPersonPlayerController>();
            }

            if (meleeAttack == null)
            {
                meleeAttack = GetComponent<PlayerMeleeAttack>();
            }

            if (comboDetector == null)
            {
                comboDetector = GetComponent<MovementComboDetector>();
            }
        }
    }
}
