using UnityEngine;
using GameProject.Feedback;
using GameProject.Skills;
using GameProject.Stats;
using GameProject.UI;

namespace GameProject.Interaction
{
    /// <summary>
    /// Collectible Skill Shard dropped by defeated enemies.
    /// Hops upward and settles onto the floor, rotates gently, and unlocks a SkillEffectType on touch.
    /// </summary>
    public class SkillShardPickup : MonoBehaviour
    {
        [Header("Unlock Reward")]
        [SerializeField] private SkillEffectType effectToUnlock = SkillEffectType.FlameNova;
        [SerializeField] private string shardDisplayName = "Mảnh Hỏa Bộc";
        [SerializeField] private Color pickupBurstColor = new Color(1.0f, 0.45f, 0.12f, 1f);

        [Header("Hop & Idle Animation")]
        [SerializeField] private float hopUpwardVelocity = 5.4f;
        [SerializeField] private float hopHorizontalSpeed = 1.6f;
        [SerializeField] private float hopGravity = -16f;
        [SerializeField] private float restingHeightOffset = 0.62f;
        [SerializeField] private float pickupDelay = 0.35f;
        [SerializeField] private float spinSpeed = 115f;
        [SerializeField] private float bobAmplitude = 0.14f;
        [SerializeField] private float bobFrequency = 3.2f;

        private Vector3 _hopVelocity;
        private float _floorY;
        private bool _isHopping;
        private float _spawnTime;
        private Vector3 _restPosition;
        private bool _collected;

        public SkillEffectType EffectToUnlock => effectToUnlock;

        private void Awake()
        {
            _spawnTime = Time.time;
            _restPosition = transform.position;
            _floorY = Mathf.Max(0f, transform.position.y - restingHeightOffset);
        }

        /// <summary>
        /// Launches the shard into a short celebratory arc from a defeated enemy before resting on the floor.
        /// </summary>
        public void LaunchHop(float groundY)
        {
            _floorY = groundY;
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            if (randomDir.sqrMagnitude < 0.01f)
            {
                randomDir = Vector2.right;
            }

            _hopVelocity = new Vector3(
                randomDir.x * hopHorizontalSpeed,
                hopUpwardVelocity,
                randomDir.y * hopHorizontalSpeed);

            _isHopping = true;
            _spawnTime = Time.time;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            if (_isHopping)
            {
                _hopVelocity.y += hopGravity * Time.deltaTime;
                transform.position += _hopVelocity * Time.deltaTime;

                float targetRestY = _floorY + restingHeightOffset;
                if (transform.position.y <= targetRestY && _hopVelocity.y <= 0f)
                {
                    Vector3 landed = transform.position;
                    landed.y = targetRestY;
                    transform.position = landed;
                    _restPosition = landed;
                    _isHopping = false;
                }
            }
            else
            {
                float bob = Mathf.Sin((Time.time - _spawnTime) * bobFrequency) * bobAmplitude;
                transform.position = _restPosition + Vector3.up * bob;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryCollect(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryCollect(other);
        }

        public bool Collect(MovementComboDetector detector)
        {
            if (_collected || detector == null)
            {
                return false;
            }

            _collected = true;
            bool newlyUnlocked = detector.UnlockEffect(effectToUnlock);

            AudioFeedbackManager audio = FindAnyObjectByType<AudioFeedbackManager>();
            audio?.PlayShardPickup();

            SkillVisualEffect.SpawnShardPickupBurst(transform.position, pickupBurstColor);

            HudController hud = FindAnyObjectByType<HudController>();
            if (hud != null)
            {
                if (newlyUnlocked)
                {
                    hud.ShowCustomBanner(
                        "MỞ KHÓA HIỆU ỨNG MỚI",
                        $"ĐÃ MỞ KHÓA: {effectToUnlock}!",
                        "Nhấn [K] mở Bảng Sáng Tạo Chiêu Thức để ghép hiệu ứng này ngay.",
                        3.4f);
                }
                else
                {
                    PlayerStats stats = detector.GetComponent<PlayerStats>();
                    if (stats != null)
                    {
                        stats.Heal(10f);
                    }

                    hud.ShowCustomBanner(
                        "NHẶT MẢNH KỸ NĂNG",
                        shardDisplayName.ToUpperInvariant(),
                        $"Đã mở khóa {effectToUnlock} trước đó  •  Hồi +10 HP",
                        2.2f);
                }
            }

            Destroy(gameObject);
            return true;
        }

        private void TryCollect(Collider other)
        {
            if (_collected || Time.time - _spawnTime < pickupDelay)
            {
                return;
            }

            MovementComboDetector detector = other.GetComponentInParent<MovementComboDetector>();
            if (detector == null)
            {
                return;
            }

            Collect(detector);
        }
    }
}
