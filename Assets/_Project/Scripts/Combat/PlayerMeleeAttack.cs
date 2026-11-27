using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using GameProject.Feedback;
using GameProject.Skills;

namespace GameProject.Combat
{
    /// <summary>
    /// Short-range basic melee attack triggered by Left Mouse Click (new Unity Input System).
    /// Designed to weave smoothly between movement-step combo skills.
    /// </summary>
    public class PlayerMeleeAttack : MonoBehaviour
    {
        [Header("Attack Tuning")]
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float attackCooldown = 0.45f;
        [SerializeField] private float attackForwardOffset = 1.35f;
        [SerializeField] private float attackRadius = 1.25f;
        [SerializeField] private float knockbackStrength = 6.5f;

        private InputAction _attackAction;
        private float _nextAttackTime;
        private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();

        public bool InputLocked { get; set; }

        private void Awake()
        {
            _attackAction = new InputAction("BasicAttack", InputActionType.Button, "<Mouse>/leftButton");
        }

        private void OnEnable()
        {
            if (_attackAction != null)
            {
                _attackAction.performed += OnAttackPerformed;
                _attackAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_attackAction != null)
            {
                _attackAction.performed -= OnAttackPerformed;
                _attackAction.Disable();
            }
        }

        private void OnDestroy()
        {
            _attackAction?.Dispose();
        }

        private void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            if (InputLocked || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            TryPerformAttack();
        }

        /// <summary>
        /// Performs a short-range melee strike in front of the Player if off cooldown.
        /// Returns the number of IDamageable targets hit.
        /// </summary>
        public int TryPerformAttack()
        {
            if (Time.time < _nextAttackTime)
            {
                return 0;
            }

            _nextAttackTime = Time.time + attackCooldown;
            SkillVisualEffect.SpawnMeleeSlash(transform);

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayAttackSwing();

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 hitCenter = transform.position + forward * attackForwardOffset + Vector3.up * 1.0f;
            Collider[] hits = Physics.OverlapSphere(hitCenter, attackRadius, ~0, QueryTriggerInteraction.Ignore);

            _hitTargets.Clear();
            int hitCount = 0;

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = hitTransform.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || !_hitTargets.Add(damageable))
                {
                    continue;
                }

                Vector3 knockDir = hitTransform.position - transform.position;
                knockDir.y = 0f;
                knockDir = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : forward;

                damageable.TakeDamage(attackDamage, knockDir * knockbackStrength);
                hitCount++;
            }

            return hitCount;
        }
    }
}
