using UnityEngine;

namespace GameProject.Feedback
{
    /// <summary>
    /// Spawns URP Particle System VFX prefabs (VFX_HitSpark, VFX_Shockwave, VFX_LungeDash, VFX_Heal).
    /// All prefabs configure ParticleSystemStopAction.Destroy so instances clean themselves up automatically.
    /// </summary>
    public class VfxFeedbackManager : MonoBehaviour
    {
        [Header("URP Particle VFX Prefabs")]
        [SerializeField] private GameObject hitSparkPrefab;
        [SerializeField] private GameObject shockwavePrefab;
        [SerializeField] private GameObject lungeDashPrefab;
        [SerializeField] private GameObject healPrefab;

        public GameObject SpawnHitSpark(Vector3 worldPosition)
        {
            return SpawnEffect(hitSparkPrefab, worldPosition, Quaternion.identity, null);
        }

        public GameObject SpawnShockwave(Vector3 origin)
        {
            return SpawnEffect(shockwavePrefab, origin + new Vector3(0f, 0.2f, 0f), Quaternion.identity, null);
        }

        public GameObject SpawnLungeDash(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                return null;
            }

            Vector3 pos = playerTransform.position + Vector3.up * 0.7f;
            Quaternion rot = Quaternion.LookRotation(-playerTransform.forward, Vector3.up);
            return SpawnEffect(lungeDashPrefab, pos, rot, playerTransform);
        }

        public GameObject SpawnHeal(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                return null;
            }

            return SpawnEffect(healPrefab, playerTransform.position + new Vector3(0f, 0.15f, 0f), Quaternion.identity, playerTransform);
        }

        private static GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation, parent);
            ParticleSystem ps = instance.GetComponent<ParticleSystem>();
            if (ps != null && !ps.isPlaying)
            {
                ps.Play(withChildren: true);
            }

            return instance;
        }
    }
}
