using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using GameProject.Combat;
using GameProject.Enemies;
using GameProject.Feedback;
using GameProject.Player;
using GameProject.Stats;

namespace GameProject.Skills
{
    /// <summary>
    /// Records discrete movement/action steps within a rolling time window (default 1.5s)
    /// and triggers matching CustomSkillData combos using PlayerStats Stamina.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(ThirdPersonPlayerController))]
    public class MovementComboDetector : MonoBehaviour
    {
        private struct TimedStep
        {
            public MoveStep Step;
            public float Timestamp;
        }

        [Header("References")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private ThirdPersonPlayerController playerController;

        [Header("Combo Settings")]
        [SerializeField] private float comboWindowSeconds = 1.5f;
        [SerializeField] private int maxBufferLength = 6;

        [Header("Effect Tuning")]
        [SerializeField] private float lungeSpeed = 18.0f;
        [SerializeField] private float lungeUpwardLift = 1.5f;
        [SerializeField] private float lungeDamage = 25.0f;
        [SerializeField] private float lungeKnockback = 10.0f;
        [SerializeField] private float lungeHitRadius = 1.5f;
        [SerializeField] private float lungeHitDuration = 0.28f;
        [SerializeField] private float shockwaveRadius = 4.5f;
        [SerializeField] private float shockwaveDamage = 35.0f;
        [SerializeField] private float shockwaveKnockback = 12.0f;
        [SerializeField] private float shockwaveForce = 10.0f;
        [SerializeField] private float highLeapVelocity = 13.5f;
        [SerializeField] private float healBurstAmount = 30.0f;
        [SerializeField] private float flameNovaRadius = 4.8f;
        [SerializeField] private float flameNovaDamage = 28.0f;
        [SerializeField] private float flameBurnDuration = 3.0f;
        [SerializeField] private float flameBurnDps = 8.0f;
        [SerializeField] private float frostSlowRadius = 5.0f;
        [SerializeField] private float frostSlowDamage = 15.0f;
        [SerializeField] private float frostSlowDuration = 3.0f;
        [SerializeField] private float frostSpeedMultiplier = 0.5f;

        [Header("Unlocked Effects")]
        [SerializeField] private List<SkillEffectType> unlockedEffects = new List<SkillEffectType>
        {
            SkillEffectType.ForwardLunge,
            SkillEffectType.AreaShockwave,
            SkillEffectType.HighLeap,
            SkillEffectType.HealBurst
        };

        [Header("Equipped Skills")]
        [SerializeField] private List<CustomSkillData> equippedSkills = new List<CustomSkillData>();

        private readonly List<TimedStep> _timedBuffer = new List<TimedStep>();
        private readonly List<MoveStep> _publicBuffer = new List<MoveStep>();
        private readonly Dictionary<CustomSkillData, float> _cooldownReadyTimes = new Dictionary<CustomSkillData, float>();
        private readonly HashSet<IDamageable> _lungeHitTargets = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> _shockwaveHitTargets = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> _aoeHitTargets = new HashSet<IDamageable>();
        private float _activeLungeTimer;

        private InputAction _forwardAction;
        private InputAction _backwardAction;
        private InputAction _leftAction;
        private InputAction _rightAction;
        private InputAction _jumpAction;
        private InputAction _dashAction;

        public bool InputLocked { get; set; }
        public IReadOnlyList<CustomSkillData> EquippedSkills => equippedSkills;
        public IReadOnlyList<SkillEffectType> UnlockedEffects => unlockedEffects;
        public IReadOnlyList<MoveStep> CurrentBuffer => _publicBuffer;

        public event Action<CustomSkillData> OnSkillActivated;
        public event Action<CustomSkillData, string> OnSkillFailed;
        public event Action<IReadOnlyList<MoveStep>> OnComboBufferChanged;
        public event Action<IReadOnlyList<CustomSkillData>> OnSkillsListChanged;
        public event Action<SkillEffectType> OnEffectUnlocked;
        public event Action<IReadOnlyList<SkillEffectType>> OnUnlockedEffectsChanged;

        private void Awake()
        {
            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<ThirdPersonPlayerController>();
            }

            EnsureDefaultUnlockedEffects();
            EnsureDefaultSkills();
            InitializeStepActions();
        }

        private void OnEnable()
        {
            BindStepActions(true);
            EnableStepActions(true);
        }

        private void OnDisable()
        {
            BindStepActions(false);
            EnableStepActions(false);
        }

        private void OnDestroy()
        {
            _forwardAction?.Dispose();
            _backwardAction?.Dispose();
            _leftAction?.Dispose();
            _rightAction?.Dispose();
            _jumpAction?.Dispose();
            _dashAction?.Dispose();
        }

        private void Update()
        {
            PruneExpiredSteps();
            TickActiveLungeHitbox();
        }

        /// <summary>
        /// Records a movement step into the rolling combo buffer and checks for a matching skill.
        /// </summary>
        public void RecordStep(MoveStep step)
        {
            if (InputLocked)
            {
                return;
            }

            PruneExpiredSteps();

            _timedBuffer.Add(new TimedStep
            {
                Step = step,
                Timestamp = Time.time
            });

            while (_timedBuffer.Count > maxBufferLength)
            {
                _timedBuffer.RemoveAt(0);
            }

            SyncPublicBuffer();

            if (!TryMatchAndActivateSkill())
            {
                OnComboBufferChanged?.Invoke(_publicBuffer);
            }
        }

        /// <summary>
        /// Adds a new custom skill at runtime (or replaces an existing skill with the exact same combo sequence).
        /// </summary>
        public void AddOrUpdateSkill(CustomSkillData skill)
        {
            if (skill == null || !skill.IsValid())
            {
                return;
            }

            for (int i = 0; i < equippedSkills.Count; i++)
            {
                if (equippedSkills[i].HasSameCombo(skill.comboSteps))
                {
                    equippedSkills[i] = skill;
                    OnSkillsListChanged?.Invoke(equippedSkills);
                    return;
                }
            }

            equippedSkills.Add(skill);
            OnSkillsListChanged?.Invoke(equippedSkills);
        }

        public float GetRemainingCooldown(CustomSkillData skill)
        {
            if (skill != null && _cooldownReadyTimes.TryGetValue(skill, out float readyTime))
            {
                return Mathf.Max(0f, readyTime - Time.time);
            }

            return 0f;
        }

        public void ClearBuffer()
        {
            if (_timedBuffer.Count == 0)
            {
                return;
            }

            _timedBuffer.Clear();
            _publicBuffer.Clear();
            OnComboBufferChanged?.Invoke(_publicBuffer);
        }

        public List<CustomSkillData> ExportSkillsCopy()
        {
            List<CustomSkillData> copy = new List<CustomSkillData>();
            if (equippedSkills == null)
            {
                return copy;
            }

            for (int i = 0; i < equippedSkills.Count; i++)
            {
                CustomSkillData src = equippedSkills[i];
                if (src == null) continue;
                copy.Add(new CustomSkillData(src.skillName, src.comboSteps, src.effects, src.staminaCost, src.cooldown));
            }

            return copy;
        }

        public void ImportSkills(List<CustomSkillData> skills)
        {
            if (skills == null || skills.Count == 0)
            {
                return;
            }

            equippedSkills = new List<CustomSkillData>(skills.Count);
            for (int i = 0; i < skills.Count; i++)
            {
                CustomSkillData src = skills[i];
                if (src == null || !src.IsValid()) continue;
                equippedSkills.Add(new CustomSkillData(src.skillName, src.comboSteps, src.effects, src.staminaCost, src.cooldown));
            }

            _cooldownReadyTimes.Clear();
            OnSkillsListChanged?.Invoke(equippedSkills);
        }

        public bool IsEffectUnlocked(SkillEffectType effect)
        {
            return unlockedEffects != null && unlockedEffects.Contains(effect);
        }

        public bool UnlockEffect(SkillEffectType effect)
        {
            EnsureDefaultUnlockedEffects();
            if (unlockedEffects.Contains(effect))
            {
                return false;
            }

            unlockedEffects.Add(effect);
            OnEffectUnlocked?.Invoke(effect);
            OnUnlockedEffectsChanged?.Invoke(unlockedEffects);
            return true;
        }

        public List<SkillEffectType> ExportUnlockedEffectsCopy()
        {
            EnsureDefaultUnlockedEffects();
            return new List<SkillEffectType>(unlockedEffects);
        }

        public void ImportUnlockedEffects(List<SkillEffectType> effects)
        {
            unlockedEffects = new List<SkillEffectType>();
            EnsureDefaultUnlockedEffects();

            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    if (!unlockedEffects.Contains(effects[i]))
                    {
                        unlockedEffects.Add(effects[i]);
                    }
                }
            }

            OnUnlockedEffectsChanged?.Invoke(unlockedEffects);
        }

        private void EnsureDefaultUnlockedEffects()
        {
            if (unlockedEffects == null)
            {
                unlockedEffects = new List<SkillEffectType>();
            }

            if (!unlockedEffects.Contains(SkillEffectType.ForwardLunge)) unlockedEffects.Add(SkillEffectType.ForwardLunge);
            if (!unlockedEffects.Contains(SkillEffectType.AreaShockwave)) unlockedEffects.Add(SkillEffectType.AreaShockwave);
            if (!unlockedEffects.Contains(SkillEffectType.HighLeap)) unlockedEffects.Add(SkillEffectType.HighLeap);
            if (!unlockedEffects.Contains(SkillEffectType.HealBurst)) unlockedEffects.Add(SkillEffectType.HealBurst);
        }

        private void EnsureDefaultSkills()
        {
            if (equippedSkills != null && equippedSkills.Count > 0)
            {
                return;
            }

            equippedSkills = new List<CustomSkillData>
            {
                new CustomSkillData(
                    "Long Quyển",
                    new[] { MoveStep.Forward, MoveStep.Forward, MoveStep.Jump },
                    new[] { SkillEffectType.ForwardLunge, SkillEffectType.AreaShockwave },
                    30f,
                    2.5f),
                new CustomSkillData(
                    "Hồi Xuân Bộ",
                    new[] { MoveStep.Left, MoveStep.Right, MoveStep.Dash },
                    new[] { SkillEffectType.HealBurst },
                    25f,
                    3.0f)
            };
        }

        private void InitializeStepActions()
        {
            _forwardAction = new InputAction("StepForward", InputActionType.Button);
            _forwardAction.AddBinding("<Keyboard>/w");
            _forwardAction.AddBinding("<Keyboard>/upArrow");

            _backwardAction = new InputAction("StepBackward", InputActionType.Button);
            _backwardAction.AddBinding("<Keyboard>/s");
            _backwardAction.AddBinding("<Keyboard>/downArrow");

            _leftAction = new InputAction("StepLeft", InputActionType.Button);
            _leftAction.AddBinding("<Keyboard>/a");
            _leftAction.AddBinding("<Keyboard>/leftArrow");

            _rightAction = new InputAction("StepRight", InputActionType.Button);
            _rightAction.AddBinding("<Keyboard>/d");
            _rightAction.AddBinding("<Keyboard>/rightArrow");

            _jumpAction = new InputAction("StepJump", InputActionType.Button, "<Keyboard>/space");
            _dashAction = new InputAction("StepDash", InputActionType.Button, "<Keyboard>/leftShift");
        }

        private void BindStepActions(bool register)
        {
            if (register)
            {
                if (_forwardAction != null) _forwardAction.performed += OnForwardPerformed;
                if (_backwardAction != null) _backwardAction.performed += OnBackwardPerformed;
                if (_leftAction != null) _leftAction.performed += OnLeftPerformed;
                if (_rightAction != null) _rightAction.performed += OnRightPerformed;
                if (_jumpAction != null) _jumpAction.performed += OnJumpPerformed;
                if (_dashAction != null) _dashAction.performed += OnDashPerformed;
            }
            else
            {
                if (_forwardAction != null) _forwardAction.performed -= OnForwardPerformed;
                if (_backwardAction != null) _backwardAction.performed -= OnBackwardPerformed;
                if (_leftAction != null) _leftAction.performed -= OnLeftPerformed;
                if (_rightAction != null) _rightAction.performed -= OnRightPerformed;
                if (_jumpAction != null) _jumpAction.performed -= OnJumpPerformed;
                if (_dashAction != null) _dashAction.performed -= OnDashPerformed;
            }
        }

        private void EnableStepActions(bool enable)
        {
            if (enable)
            {
                _forwardAction?.Enable();
                _backwardAction?.Enable();
                _leftAction?.Enable();
                _rightAction?.Enable();
                _jumpAction?.Enable();
                _dashAction?.Enable();
            }
            else
            {
                _forwardAction?.Disable();
                _backwardAction?.Disable();
                _leftAction?.Disable();
                _rightAction?.Disable();
                _jumpAction?.Disable();
                _dashAction?.Disable();
            }
        }

        private void OnForwardPerformed(InputAction.CallbackContext ctx) => RecordStep(MoveStep.Forward);
        private void OnBackwardPerformed(InputAction.CallbackContext ctx) => RecordStep(MoveStep.Backward);
        private void OnLeftPerformed(InputAction.CallbackContext ctx) => RecordStep(MoveStep.Left);
        private void OnRightPerformed(InputAction.CallbackContext ctx) => RecordStep(MoveStep.Right);
        private void OnJumpPerformed(InputAction.CallbackContext ctx) => RecordStep(MoveStep.Jump);
        private void OnDashPerformed(InputAction.CallbackContext ctx) => RecordStep(MoveStep.Dash);

        private void PruneExpiredSteps()
        {
            if (_timedBuffer.Count == 0)
            {
                return;
            }

            float cutoff = Time.time - comboWindowSeconds;
            bool removedAny = false;

            while (_timedBuffer.Count > 0 && _timedBuffer[0].Timestamp < cutoff)
            {
                _timedBuffer.RemoveAt(0);
                removedAny = true;
            }

            if (removedAny)
            {
                SyncPublicBuffer();
                OnComboBufferChanged?.Invoke(_publicBuffer);
            }
        }

        private void SyncPublicBuffer()
        {
            _publicBuffer.Clear();
            for (int i = 0; i < _timedBuffer.Count; i++)
            {
                _publicBuffer.Add(_timedBuffer[i].Step);
            }
        }

        private bool TryMatchAndActivateSkill()
        {
            CustomSkillData bestMatch = null;
            int bestLength = 0;

            for (int i = 0; i < equippedSkills.Count; i++)
            {
                CustomSkillData skill = equippedSkills[i];
                if (skill == null || !skill.IsValid())
                {
                    continue;
                }

                int len = skill.comboSteps.Count;
                if (len > _publicBuffer.Count || len < bestLength)
                {
                    continue;
                }

                if (MatchesSuffix(skill.comboSteps))
                {
                    bestMatch = skill;
                    bestLength = len;
                }
            }

            if (bestMatch == null)
            {
                return false;
            }

            // Clear buffer so the combo does not double-fire
            _timedBuffer.Clear();
            _publicBuffer.Clear();
            OnComboBufferChanged?.Invoke(_publicBuffer);

            float remainingCooldown = GetRemainingCooldown(bestMatch);
            if (remainingCooldown > 0.05f)
            {
                OnSkillFailed?.Invoke(bestMatch, $"Đang hồi chiêu ({remainingCooldown:0.1}s)");
                return true;
            }

            if (playerStats != null && !playerStats.TryConsumeStamina(bestMatch.staminaCost))
            {
                OnSkillFailed?.Invoke(bestMatch, $"Không đủ Stamina (cần {Mathf.CeilToInt(bestMatch.staminaCost)})");
                return true;
            }

            _cooldownReadyTimes[bestMatch] = Time.time + bestMatch.cooldown;
            ExecuteEffects(bestMatch);
            OnSkillActivated?.Invoke(bestMatch);
            return true;
        }

        private bool MatchesSuffix(List<MoveStep> pattern)
        {
            int offset = _publicBuffer.Count - pattern.Count;
            for (int i = 0; i < pattern.Count; i++)
            {
                if (_publicBuffer[offset + i] != pattern[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ExecuteEffects(CustomSkillData skill)
        {
            for (int i = 0; i < skill.effects.Count; i++)
            {
                switch (skill.effects[i])
                {
                    case SkillEffectType.ForwardLunge:
                        ExecuteForwardLunge();
                        break;
                    case SkillEffectType.AreaShockwave:
                        ExecuteAreaShockwave();
                        break;
                    case SkillEffectType.HighLeap:
                        ExecuteHighLeap();
                        break;
                    case SkillEffectType.HealBurst:
                        ExecuteHealBurst();
                        break;
                    case SkillEffectType.FlameNova:
                        ExecuteFlameNova();
                        break;
                    case SkillEffectType.FrostSlow:
                        ExecuteFrostSlow();
                        break;
                }
            }
        }

        private void ExecuteForwardLunge()
        {
            if (playerController != null)
            {
                Vector3 forwardDir = transform.forward;
                forwardDir.y = 0f;
                if (forwardDir.sqrMagnitude < 0.001f)
                {
                    forwardDir = Vector3.forward;
                }

                playerController.ApplySkillImpulse(forwardDir.normalized * lungeSpeed, lungeUpwardLift);
            }

            _lungeHitTargets.Clear();
            _activeLungeTimer = lungeHitDuration;
            CheckLungeHits();

            VfxFeedbackManager vfx = FindAnyObjectByType<VfxFeedbackManager>();
            if (vfx != null) vfx.SpawnLungeDash(transform);
            SkillVisualEffect.SpawnLungeBurst(transform);

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayLungeDash();

            CameraShake shake = FindAnyObjectByType<CameraShake>();
            shake?.Shake(0.12f, 0.07f);
        }

        private void TickActiveLungeHitbox()
        {
            if (_activeLungeTimer <= 0f)
            {
                return;
            }

            _activeLungeTimer -= Time.deltaTime;
            CheckLungeHits();
        }

        private void CheckLungeHits()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 hitCenter = transform.position + forward * 1.1f + Vector3.up * 1.0f;
            Collider[] hits = Physics.OverlapSphere(hitCenter, lungeHitRadius, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = hitTransform.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || !_lungeHitTargets.Add(damageable))
                {
                    continue;
                }

                Vector3 pushDir = hitTransform.position - transform.position;
                pushDir.y = 0f;
                pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : forward;

                Vector3 knockback = pushDir * lungeKnockback + Vector3.up * 2.5f;
                damageable.TakeDamage(lungeDamage, knockback);
            }
        }

        private void ExecuteAreaShockwave()
        {
            Vector3 origin = transform.position;

            VfxFeedbackManager vfx = FindAnyObjectByType<VfxFeedbackManager>();
            if (vfx != null) vfx.SpawnShockwave(origin);
            SkillVisualEffect.SpawnShockwave(origin, shockwaveRadius);

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayShockwave();

            CameraShake shake = FindAnyObjectByType<CameraShake>();
            shake?.Shake(0.2f, 0.18f);

            _shockwaveHitTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(origin + Vector3.up * 0.5f, shockwaveRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = hitTransform.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive && _shockwaveHitTargets.Add(damageable))
                {
                    Vector3 pushDir = hitTransform.position - origin;
                    pushDir.y = 0f;
                    pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : transform.forward;

                    Vector3 knockback = pushDir * shockwaveKnockback + Vector3.up * 3.0f;
                    damageable.TakeDamage(shockwaveDamage, knockback);
                }

                Rigidbody rb = hits[i].attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(shockwaveForce, origin, shockwaveRadius, 0.4f, ForceMode.Impulse);
                }
            }
        }

        private void ExecuteHighLeap()
        {
            if (playerController != null)
            {
                playerController.ApplySkillImpulse(Vector3.zero, highLeapVelocity);
            }

            SkillVisualEffect.SpawnHighLeapRing(transform.position);

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayHighLeap();

            CameraShake shake = FindAnyObjectByType<CameraShake>();
            shake?.Shake(0.12f, 0.08f);
        }

        private void ExecuteHealBurst()
        {
            if (playerStats != null)
            {
                playerStats.Heal(healBurstAmount);
            }

            VfxFeedbackManager vfx = FindAnyObjectByType<VfxFeedbackManager>();
            if (vfx != null) vfx.SpawnHeal(transform);
            SkillVisualEffect.SpawnHealBurst(transform);

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayHeal();
        }

        private void ExecuteFlameNova()
        {
            Vector3 origin = transform.position;
            SkillVisualEffect.SpawnFlameNova(origin, flameNovaRadius);

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayFlameNova();

            CameraShake shake = FindAnyObjectByType<CameraShake>();
            shake?.Shake(0.18f, 0.14f);

            _aoeHitTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(origin + Vector3.up * 0.5f, flameNovaRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = hitTransform.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive && _aoeHitTargets.Add(damageable))
                {
                    Vector3 pushDir = hitTransform.position - origin;
                    pushDir.y = 0f;
                    pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : transform.forward;

                    damageable.TakeDamage(flameNovaDamage, pushDir * 7.5f + Vector3.up * 2.0f);

                    EnemyController enemy = hitTransform.GetComponentInParent<EnemyController>();
                    if (enemy != null && enemy.IsAlive)
                    {
                        enemy.ApplyBurn(flameBurnDuration, flameBurnDps);
                    }
                }
            }
        }

        private void ExecuteFrostSlow()
        {
            Vector3 origin = transform.position;
            SkillVisualEffect.SpawnFrostSlow(origin, frostSlowRadius);

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayFrostSlow();

            CameraShake shake = FindAnyObjectByType<CameraShake>();
            shake?.Shake(0.14f, 0.10f);

            _aoeHitTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(origin + Vector3.up * 0.5f, frostSlowRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = hitTransform.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive && _aoeHitTargets.Add(damageable))
                {
                    Vector3 pushDir = hitTransform.position - origin;
                    pushDir.y = 0f;
                    pushDir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : transform.forward;

                    damageable.TakeDamage(frostSlowDamage, pushDir * 4.0f);

                    EnemyController enemy = hitTransform.GetComponentInParent<EnemyController>();
                    if (enemy != null && enemy.IsAlive)
                    {
                        enemy.ApplySlow(frostSlowDuration, frostSpeedMultiplier);
                    }
                }
            }
        }
    }
}
