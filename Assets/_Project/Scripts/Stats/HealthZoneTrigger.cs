using UnityEngine;

namespace GameProject.Stats
{
    /// <summary>
    /// Simple sandbox test trigger that damages or heals a PlayerStats component on contact.
    /// </summary>
    public class HealthZoneTrigger : MonoBehaviour
    {
        public enum ZoneEffect
        {
            Damage,
            Heal
        }

        [Header("Effect")]
        [SerializeField] private ZoneEffect effect = ZoneEffect.Damage;
        [SerializeField] private float amount = 20f;
        [SerializeField] private float cooldownSeconds = 0.75f;

        [Header("Visual Feedback")]
        [SerializeField] private float pulseScaleMultiplier = 1.12f;
        [SerializeField] private float pulseRecoverySpeed = 8f;

        private Vector3 _baseScale;
        private float _nextTriggerTime;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            if (transform.localScale != _baseScale)
            {
                transform.localScale = Vector3.Lerp(
                    transform.localScale,
                    _baseScale,
                    1f - Mathf.Exp(-pulseRecoverySpeed * Time.deltaTime));
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryApplyEffect(other);
        }

        private void TryApplyEffect(Collider other)
        {
            if (Time.time < _nextTriggerTime)
            {
                return;
            }

            PlayerStats stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null)
            {
                return;
            }

            _nextTriggerTime = Time.time + cooldownSeconds;

            if (effect == ZoneEffect.Damage)
            {
                stats.TakeDamage(amount);
            }
            else
            {
                stats.Heal(amount);
            }

            transform.localScale = _baseScale * pulseScaleMultiplier;
        }
    }
}
