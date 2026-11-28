using System.Collections;
using UnityEngine;
using GameProject.Combat;
using GameProject.Feedback;
using GameProject.Interaction;
using GameProject.Skills;
using GameProject.Stats;
using GameProject.World;

namespace GameProject.Enemies
{
    /// <summary>
    /// Ground-based 3-state Enemy AI (Patrol, Chase, Attack) with IDamageable support,
    /// red hit flash (0.15s), knockback velocity, overhead health bar, and loot drop on defeat.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        public enum EnemyState
        {
            Patrol,
            Chase,
            Attack
        }

        [Header("Health")]
        [SerializeField] private float maxHealth = 80f;
        [SerializeField] private EnemyHealthBar healthBar;

        [Header("Visual Feedback & Animation")]
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Animator enemyAnimator;
        [SerializeField] private Color hitFlashColor = new Color(1.0f, 0.12f, 0.12f, 1.0f);
        [SerializeField] private Color burnTintColor = new Color(1.0f, 0.45f, 0.10f, 1.0f);
        [SerializeField] private Color slowTintColor = new Color(0.28f, 0.78f, 1.0f, 1.0f);
        [SerializeField] private float hitFlashDuration = 0.15f;
        [SerializeField] private float deathAnimationDuration = 1.0f;
        [SerializeField] private bool dropLootOnDeath = true;
        [Tooltip("Skill Shard prefabs (e.g. Shard_Flame, Shard_Frost) dropped randomly when this enemy dies.")]
        [SerializeField] private GameObject[] skillShardPrefabs;

        [Header("Patrol (No NavMesh Required)")]
        [Tooltip("Optional explicit scene waypoints. If empty, uses patrolLocalOffsets around spawn position.")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private Vector3[] patrolLocalOffsets = new Vector3[]
        {
            new Vector3(-3.0f, 0f, 0f),
            new Vector3(3.0f, 0f, 0f)
        };
        [SerializeField] private float patrolSpeed = 2.2f;
        [SerializeField] private float patrolWaitTime = 0.8f;
        [SerializeField] private float waypointReachDistance = 0.45f;

        [Header("Detection & Chase")]
        [SerializeField] private float detectionRadius = 10.0f;
        [SerializeField] private float loseAggroRadius = 14.0f;
        [SerializeField] private float chaseSpeed = 4.2f;
        [SerializeField] private float turnSpeed = 9.0f;

        [Header("Attack")]
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float attackDamage = 12.0f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float attackKnockbackToPlayer = 4.0f;

        [Header("Physics")]
        [SerializeField] private float gravity = -20.0f;
        [SerializeField] private float knockbackDamping = 7.0f;

        private CharacterController _controller;
        private Transform _playerTransform;
        private PlayerStats _playerStats;

        private float _currentHealth;
        private EnemyState _state = EnemyState.Patrol;
        private Vector3[] _resolvedPatrolPoints;
        private int _currentPatrolIndex;
        private float _patrolWaitTimer;
        private float _attackCooldownTimer;

        private float _verticalVelocity;
        private Vector3 _knockbackVelocity;

        private float _hitFlashTimer;
        private Color _originalBodyColor = new Color(0.55f, 0.12f, 0.14f, 1f);
        private MaterialPropertyBlock _propBlock;

        private float _burnTimer;
        private float _burnDps;
        private float _burnTickTimer;
        private float _slowTimer;
        private float _slowSpeedMultiplier = 1f;
        private bool _isDying;
        private float _currentMoveSpeed;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
        private static readonly int AnimAttackHash = Animator.StringToHash("Attack");
        private static readonly int AnimGetHitHash = Animator.StringToHash("GetHit");
        private static readonly int AnimDieHash = Animator.StringToHash("Die");

        public bool IsAlive => !_isDying && _currentHealth > 0f;
        public bool IsDying => _isDying;
        public bool IsAmbushEnemy { get; set; }
        public bool IsBurning => _burnTimer > 0f;
        public bool IsSlowed => _slowTimer > 0f;
        public float CurrentSpeedMultiplier => _slowTimer > 0f ? _slowSpeedMultiplier : 1f;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public EnemyState CurrentState => _state;
        public Animator EnemyAnimator => enemyAnimator;

        public void ForceAggro()
        {
            if (_playerTransform == null)
            {
                ResolvePlayer();
            }

            if (_playerStats != null && _playerStats.IsAlive)
            {
                _state = EnemyState.Chase;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            maxHealth = Mathf.Max(1f, maxHealth);
            _currentHealth = maxHealth;

            if (healthBar == null)
            {
                healthBar = GetComponentInChildren<EnemyHealthBar>();
            }

            if (enemyAnimator == null)
            {
                enemyAnimator = GetComponentInChildren<Animator>();
            }

            if (enemyAnimator != null)
            {
                enemyAnimator.applyRootMotion = false;
            }

            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                if (bodyRenderer == null)
                {
                    Transform bodyTf = transform.Find("Visuals/Body");
                    if (bodyTf != null)
                    {
                        bodyRenderer = bodyTf.GetComponent<MeshRenderer>();
                    }
                }
            }

            _propBlock = new MaterialPropertyBlock();
            if (bodyRenderer != null && bodyRenderer.sharedMaterial != null)
            {
                if (bodyRenderer.sharedMaterial.HasProperty(BaseColorId))
                {
                    _originalBodyColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);
                }
                else if (bodyRenderer.sharedMaterial.HasProperty(ColorId))
                {
                    _originalBodyColor = bodyRenderer.sharedMaterial.GetColor(ColorId);
                }
            }

            BuildPatrolPoints();
            healthBar?.SetNormalizedHealth(1f);
        }

        private void Start()
        {
            ResolvePlayer();
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            if (_playerTransform == null)
            {
                ResolvePlayer();
            }

            _currentMoveSpeed = 0f;
            UpdateStatusEffects();
            UpdateHitFlash();
            if (!IsAlive)
            {
                return;
            }

            UpdateStateMachine();
            ApplyGravityAndKnockback();
            UpdateAnimatorSpeed();
        }

        public void ApplyBurn(float duration, float damagePerSecond)
        {
            if (!IsAlive || duration <= 0f || damagePerSecond <= 0f)
            {
                return;
            }

            _burnTimer = Mathf.Max(_burnTimer, duration);
            _burnDps = Mathf.Max(_burnDps, damagePerSecond);
            if (_burnTickTimer <= 0f)
            {
                _burnTickTimer = 0.5f;
            }
            RefreshBodyTint();
        }

        public void ApplySlow(float duration, float speedMultiplier)
        {
            if (!IsAlive || duration <= 0f)
            {
                return;
            }

            _slowTimer = Mathf.Max(_slowTimer, duration);
            _slowSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1f);
            RefreshBodyTint();
        }

        public void TakeDamage(float amount, Vector3 knockbackForce)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            healthBar?.SetNormalizedHealth(_currentHealth / maxHealth);

            TriggerHitFlash();
            TriggerHitFeedback();

            _knockbackVelocity += new Vector3(knockbackForce.x, 0f, knockbackForce.z);
            if (knockbackForce.y > 0f && _controller != null && _controller.isGrounded)
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity, knockbackForce.y);
            }

            // Immediately aggro toward Player when hit from afar
            if (_state == EnemyState.Patrol && _playerStats != null && _playerStats.IsAlive)
            {
                _state = EnemyState.Chase;
            }

            if (_currentHealth <= 0f)
            {
                Die();
            }
            else if (enemyAnimator != null)
            {
                enemyAnimator.SetTrigger(AnimGetHitHash);
            }
        }

        private void TriggerHitFeedback()
        {
            Vector3 hitPos = transform.position + Vector3.up * 1.0f;

            VfxFeedbackManager vfx = FindAnyObjectByType<VfxFeedbackManager>();
            if (vfx != null)
            {
                vfx.SpawnHitSpark(hitPos);
            }
            else
            {
                SkillVisualEffect.SpawnHitSpark(hitPos);
            }

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayHitImpact();

            CameraShake shake = FindAnyObjectByType<CameraShake>();
            shake?.Shake(0.1f, 0.08f);
        }

        private void ResolvePlayer()
        {
            _playerStats = FindAnyObjectByType<PlayerStats>();
            if (_playerStats != null)
            {
                _playerTransform = _playerStats.transform;
            }
        }

        private void BuildPatrolPoints()
        {
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                _resolvedPatrolPoints = new Vector3[patrolPoints.Length];
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    _resolvedPatrolPoints[i] = patrolPoints[i] != null ? patrolPoints[i].position : transform.position;
                }

                return;
            }

            if (patrolLocalOffsets != null && patrolLocalOffsets.Length > 0)
            {
                Vector3 origin = transform.position;
                _resolvedPatrolPoints = new Vector3[patrolLocalOffsets.Length];
                for (int i = 0; i < patrolLocalOffsets.Length; i++)
                {
                    _resolvedPatrolPoints[i] = origin + patrolLocalOffsets[i];
                }

                return;
            }

            _resolvedPatrolPoints = new[] { transform.position };
        }

        private void UpdateStateMachine()
        {
            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }

            bool hasValidTarget = _playerTransform != null && _playerStats != null && _playerStats.IsAlive;
            float distToPlayer = hasValidTarget ? HorizontalDistance(transform.position, _playerTransform.position) : float.MaxValue;

            switch (_state)
            {
                case EnemyState.Patrol:
                    if (hasValidTarget && distToPlayer <= detectionRadius)
                    {
                        _state = EnemyState.Chase;
                        break;
                    }

                    TickPatrol();
                    break;

                case EnemyState.Chase:
                    if (!hasValidTarget || distToPlayer > loseAggroRadius)
                    {
                        _state = EnemyState.Patrol;
                        break;
                    }

                    if (distToPlayer <= attackRange)
                    {
                        _state = EnemyState.Attack;
                        break;
                    }

                    MoveToward(_playerTransform.position, chaseSpeed);
                    break;

                case EnemyState.Attack:
                    if (!hasValidTarget)
                    {
                        _state = EnemyState.Patrol;
                        break;
                    }

                    if (distToPlayer > attackRange * 1.18f)
                    {
                        _state = EnemyState.Chase;
                        break;
                    }

                    FaceTarget(_playerTransform.position);
                    if (_attackCooldownTimer <= 0f)
                    {
                        PerformAttack();
                    }
                    break;
            }
        }

        private void TickPatrol()
        {
            if (_resolvedPatrolPoints == null || _resolvedPatrolPoints.Length == 0)
            {
                return;
            }

            if (_patrolWaitTimer > 0f)
            {
                _patrolWaitTimer -= Time.deltaTime;
                return;
            }

            Vector3 targetPoint = _resolvedPatrolPoints[_currentPatrolIndex];
            float dist = HorizontalDistance(transform.position, targetPoint);
            if (dist <= waypointReachDistance)
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % _resolvedPatrolPoints.Length;
                _patrolWaitTimer = patrolWaitTime;
                return;
            }

            MoveToward(targetPoint, patrolSpeed);
        }

        private void MoveToward(Vector3 worldTarget, float speed)
        {
            Vector3 toTarget = worldTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f)
            {
                return;
            }

            Vector3 dir = toTarget.normalized;
            FaceDirection(dir);
            float effectiveSpeed = speed * CurrentSpeedMultiplier;
            _currentMoveSpeed = effectiveSpeed;
            _controller.Move(dir * (effectiveSpeed * Time.deltaTime));
        }

        private void FaceTarget(Vector3 worldTarget)
        {
            Vector3 toTarget = worldTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                FaceDirection(toTarget.normalized);
            }
        }

        private void FaceDirection(Vector3 direction)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        private void PerformAttack()
        {
            _attackCooldownTimer = attackCooldown;
            if (enemyAnimator != null)
            {
                enemyAnimator.SetTrigger(AnimAttackHash);
            }

            SkillVisualEffect.SpawnEnemyAttackFlash(transform);

            if (_playerStats != null && _playerStats.IsAlive)
            {
                Vector3 knockDir = _playerTransform.position - transform.position;
                knockDir.y = 0f;
                knockDir = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : transform.forward;
                _playerStats.TakeDamage(attackDamage, knockDir * attackKnockbackToPlayer);
            }
        }

        private void ApplyGravityAndKnockback()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity += gravity * Time.deltaTime;

            if (_knockbackVelocity.sqrMagnitude > 0.01f)
            {
                _knockbackVelocity = Vector3.Lerp(
                    _knockbackVelocity,
                    Vector3.zero,
                    1f - Mathf.Exp(-knockbackDamping * Time.deltaTime));
            }
            else
            {
                _knockbackVelocity = Vector3.zero;
            }

            Vector3 motion = _knockbackVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        private void TriggerHitFlash()
        {
            _hitFlashTimer = hitFlashDuration;
            ApplyBodyColor(hitFlashColor);
        }

        private void UpdateStatusEffects()
        {
            bool tintNeedsRefresh = false;

            if (_slowTimer > 0f)
            {
                _slowTimer = Mathf.Max(0f, _slowTimer - Time.deltaTime);
                if (_slowTimer <= 0f)
                {
                    _slowSpeedMultiplier = 1f;
                    tintNeedsRefresh = true;
                }
            }

            if (_burnTimer > 0f)
            {
                _burnTimer = Mathf.Max(0f, _burnTimer - Time.deltaTime);
                _burnTickTimer -= Time.deltaTime;

                if (_burnTickTimer <= 0f && IsAlive)
                {
                    _burnTickTimer = 0.5f;
                    float burnDamage = _burnDps * 0.5f;
                    _currentHealth = Mathf.Max(0f, _currentHealth - burnDamage);
                    healthBar?.SetNormalizedHealth(_currentHealth / maxHealth);
                    TriggerHitFlash();

                    if (_currentHealth <= 0f)
                    {
                        Die();
                        return;
                    }
                }

                if (_burnTimer <= 0f)
                {
                    tintNeedsRefresh = true;
                }
            }

            if (tintNeedsRefresh && _hitFlashTimer <= 0f)
            {
                RefreshBodyTint();
            }
        }

        private void UpdateHitFlash()
        {
            if (_hitFlashTimer <= 0f)
            {
                return;
            }

            _hitFlashTimer -= Time.deltaTime;
            if (_hitFlashTimer <= 0f)
            {
                RefreshBodyTint();
            }
        }

        private void RefreshBodyTint()
        {
            if (_hitFlashTimer > 0f)
            {
                ApplyBodyColor(hitFlashColor);
            }
            else if (_slowTimer > 0f)
            {
                ApplyBodyColor(slowTintColor);
            }
            else if (_burnTimer > 0f)
            {
                ApplyBodyColor(burnTintColor);
            }
            else
            {
                ApplyBodyColor(_originalBodyColor);
            }
        }

        private void ApplyBodyColor(Color color)
        {
            if (bodyRenderer == null)
            {
                return;
            }

            bodyRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, color);
            _propBlock.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(_propBlock);
        }

        private void UpdateAnimatorSpeed()
        {
            if (enemyAnimator == null)
            {
                return;
            }

            float normalized = chaseSpeed > 0.01f ? Mathf.Clamp01(_currentMoveSpeed / chaseSpeed) : 0f;
            enemyAnimator.SetFloat(AnimSpeedHash, normalized, 0.1f, Time.deltaTime);
        }

        private void Die()
        {
            if (_isDying)
            {
                return;
            }

            _isDying = true;
            _burnTimer = 0f;
            _slowTimer = 0f;
            _currentMoveSpeed = 0f;

            if (_controller != null)
            {
                _controller.enabled = false;
            }

            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(false);
            }

            if (enemyAnimator != null)
            {
                enemyAnimator.SetFloat(AnimSpeedHash, 0f);
                enemyAnimator.SetTrigger(AnimDieHash);
            }

            StartCoroutine(DeathSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            if (deathAnimationDuration > 0f)
            {
                yield return new WaitForSeconds(deathAnimationDuration);
            }

            WorldStateManager worldState = WorldStateManager.Resolve();
            if (worldState != null)
            {
                worldState.IncrementInt("enemies_defeated", 1);
                if (IsAmbushEnemy)
                {
                    worldState.IncrementInt("ambush_enemies_defeated", 1);
                }
            }

            SkillVisualEffect.SpawnHitSpark(transform.position + Vector3.up * 1.0f);

            if (dropLootOnDeath)
            {
                SpawnDeathDrop();
            }

            Destroy(gameObject);
        }

        private void SpawnDeathDrop()
        {
            if (skillShardPrefabs != null && skillShardPrefabs.Length > 0)
            {
                GameObject chosenPrefab = PickShardPrefab();
                if (chosenPrefab != null)
                {
                    Vector3 spawnPos = transform.position + Vector3.up * 0.6f;
                    GameObject shardObj = Instantiate(chosenPrefab, spawnPos, Quaternion.identity);
                    SkillShardPickup pickup = shardObj.GetComponent<SkillShardPickup>();
                    if (pickup != null)
                    {
                        pickup.LaunchHop(transform.position.y);
                    }
                    return;
                }
            }

            EnemyLootDrop.Spawn(transform.position);
        }

        private GameObject PickShardPrefab()
        {
            MovementComboDetector detector = FindAnyObjectByType<MovementComboDetector>();

            // Prefer dropping a shard whose effect is still locked so players unlock new elements cleanly
            if (detector != null)
            {
                var lockedCandidates = new System.Collections.Generic.List<GameObject>();
                for (int i = 0; i < skillShardPrefabs.Length; i++)
                {
                    GameObject candidate = skillShardPrefabs[i];
                    if (candidate == null) continue;
                    SkillShardPickup pickup = candidate.GetComponent<SkillShardPickup>();
                    if (pickup != null && !detector.IsEffectUnlocked(pickup.EffectToUnlock))
                    {
                        lockedCandidates.Add(candidate);
                    }
                }

                if (lockedCandidates.Count > 0)
                {
                    return lockedCandidates[Random.Range(0, lockedCandidates.Count)];
                }
            }

            return skillShardPrefabs[Random.Range(0, skillShardPrefabs.Length)];
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
