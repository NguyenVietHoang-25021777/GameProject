using System;
using UnityEngine;
using GameProject.Combat;
using GameProject.Player;

namespace GameProject.Stats
{
    /// <summary>
    /// Manages Player Health (HP) and Stamina with C# events for UI/gameplay listeners,
    /// sprint drain, exhaustion hysteresis, delayed stamina regeneration, and IDamageable support.
    /// </summary>
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float startingHealth = 100f;

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float startingStamina = 100f;
        [SerializeField] private float sprintDrainPerSecond = 25f;
        [SerializeField] private float staminaRegenPerSecond = 22f;
        [SerializeField] private float staminaRegenDelay = 1.5f;
        [Tooltip("Minimum stamina required before sprinting can resume after fully exhausting stamina.")]
        [SerializeField] private float minStaminaAfterExhaustion = 15f;

        private float _currentHealth;
        private float _currentStamina;
        private float _regenDelayTimer;
        private bool _isSprinting;
        private bool _isExhausted;
        private ThirdPersonPlayerController _playerController;

        public bool IsAlive => _currentHealth > 0f;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthNormalized => maxHealth > 0f ? _currentHealth / maxHealth : 0f;

        public float CurrentStamina => _currentStamina;
        public float MaxStamina => maxStamina;
        public float StaminaNormalized => maxStamina > 0f ? _currentStamina / maxStamina : 0f;

        public bool IsExhausted => _isExhausted;
        public bool CanSprint => !_isExhausted && _currentStamina > 0.01f;

        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnStaminaChanged;
        public event Action OnDied;

        private void Awake()
        {
            _playerController = GetComponent<ThirdPersonPlayerController>();
            maxHealth = Mathf.Max(1f, maxHealth);
            maxStamina = Mathf.Max(1f, maxStamina);
            _currentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
            _currentStamina = Mathf.Clamp(startingStamina, 0f, maxStamina);
            _isExhausted = _currentStamina <= 0.01f;
        }

        private void Start()
        {
            NotifyAll();
        }

        private void Update()
        {
            UpdateStamina(Time.deltaTime);
        }

        /// <summary>
        /// Called each frame by the player movement controller to indicate active sprinting.
        /// </summary>
        public void SetSprinting(bool sprinting)
        {
            if (sprinting && !CanSprint)
            {
                _isSprinting = false;
                return;
            }

            _isSprinting = sprinting;
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, Vector3.zero);
        }

        public void TakeDamage(float amount, Vector3 knockbackForce)
        {
            if (amount <= 0f || _currentHealth <= 0f)
            {
                return;
            }

            float previous = _currentHealth;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            if (knockbackForce.sqrMagnitude > 0.01f && _playerController != null)
            {
                _playerController.ApplySkillImpulse(knockbackForce, 0f);
            }

            if (!Mathf.Approximately(previous, _currentHealth))
            {
                OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            }

            if (_currentHealth <= 0f)
            {
                OnDied?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || _currentHealth >= maxHealth)
            {
                return;
            }

            float previous = _currentHealth;
            _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);

            if (!Mathf.Approximately(previous, _currentHealth))
            {
                OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            }
        }

        /// <summary>
        /// Attempts to spend an instant chunk of stamina (e.g. for skills).
        /// Returns true if enough stamina was available and deducted.
        /// </summary>
        public bool TryConsumeStamina(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (_currentStamina + 0.01f < amount)
            {
                return false;
            }

            float previous = _currentStamina;
            _currentStamina = Mathf.Max(0f, _currentStamina - amount);
            _regenDelayTimer = staminaRegenDelay;

            if (_currentStamina <= 0.01f)
            {
                _currentStamina = 0f;
                _isExhausted = true;
                _isSprinting = false;
            }

            if (!Mathf.Approximately(previous, _currentStamina))
            {
                OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
            }

            return true;
        }

        public void NotifyAll()
        {
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
        }

        public void RestoreValues(float health, float stamina)
        {
            _currentHealth = Mathf.Clamp(health, 0f, maxHealth);
            _currentStamina = Mathf.Clamp(stamina, 0f, maxStamina);
            _isExhausted = _currentStamina <= 0.01f;
            _regenDelayTimer = 0f;
            NotifyAll();
        }

        private void UpdateStamina(float deltaTime)
        {
            if (_isSprinting)
            {
                float previous = _currentStamina;
                _currentStamina = Mathf.Max(0f, _currentStamina - sprintDrainPerSecond * deltaTime);
                _regenDelayTimer = staminaRegenDelay;

                if (_currentStamina <= 0.01f)
                {
                    _currentStamina = 0f;
                    _isExhausted = true;
                    _isSprinting = false;
                }

                if (!Mathf.Approximately(previous, _currentStamina))
                {
                    OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
                }

                return;
            }

            if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer = Mathf.Max(0f, _regenDelayTimer - deltaTime);
                return;
            }

            if (_currentStamina < maxStamina)
            {
                float previous = _currentStamina;
                _currentStamina = Mathf.Min(maxStamina, _currentStamina + staminaRegenPerSecond * deltaTime);

                if (_isExhausted && _currentStamina >= Mathf.Min(minStaminaAfterExhaustion, maxStamina))
                {
                    _isExhausted = false;
                }

                if (!Mathf.Approximately(previous, _currentStamina))
                {
                    OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
                }
            }
        }
    }
}
