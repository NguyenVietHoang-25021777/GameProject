using UnityEngine;

namespace GameProject.Feedback
{
    /// <summary>
    /// Smooth camera shake component providing a damped local offset that ThirdPersonCameraController
    /// composes cleanly without conflicting with orbit positioning or rotation.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Header("Shake Tuning")]
        [SerializeField] private float noiseFrequency = 28f;
        [SerializeField] private float maxMagnitude = 0.45f;

        private float _duration;
        private float _remaining;
        private float _magnitude;
        private float _seedX;
        private float _seedY;
        private Vector3 _currentOffset;

        public Vector3 CurrentOffset => _currentOffset;
        public bool IsShaking => _remaining > 0f;

        private void Awake()
        {
            _seedX = Random.Range(0f, 100f);
            _seedY = Random.Range(100f, 200f);
        }

        private void Update()
        {
            if (_remaining <= 0f)
            {
                _currentOffset = Vector3.zero;
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
            float normalizedRemaining = _duration > 0.0001f ? (_remaining / _duration) : 0f;
            float damper = normalizedRemaining * normalizedRemaining;

            float t = Time.unscaledTime * noiseFrequency;
            float x = (Mathf.PerlinNoise(_seedX, t) * 2f - 1f) * _magnitude * damper;
            float y = (Mathf.PerlinNoise(_seedY, t) * 2f - 1f) * _magnitude * damper;

            _currentOffset = new Vector3(x, y, 0f);
        }

        /// <summary>
        /// Triggers a smooth screen shake that decays cleanly back to zero offset.
        /// </summary>
        public void Shake(float duration, float magnitude)
        {
            if (duration <= 0f || magnitude <= 0f)
            {
                return;
            }

            float clampedMag = Mathf.Min(magnitude, maxMagnitude);

            if (_remaining <= 0f)
            {
                _duration = duration;
                _remaining = duration;
                _magnitude = clampedMag;
            }
            else
            {
                // Preserve stronger active shake while extending duration smoothly
                _magnitude = Mathf.Max(_magnitude, clampedMag);
                _remaining = Mathf.Max(_remaining, duration);
                _duration = Mathf.Max(_duration, _remaining);
            }
        }
    }
}
