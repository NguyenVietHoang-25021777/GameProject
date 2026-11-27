using UnityEngine;
using GameProject.Stats;

namespace GameProject.Enemies
{
    /// <summary>
    /// Simple glowing pickup orb dropped by defeated enemies (restores HP on touch).
    /// </summary>
    public class EnemyLootDrop : MonoBehaviour
    {
        [SerializeField] private float healAmount = 15f;
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobFrequency = 3f;
        [SerializeField] private float lifetime = 20f;

        private Vector3 _basePosition;
        private float _elapsed;
        private Material _runtimeMaterial;

        public static void Spawn(Vector3 position)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            orb.name = "Loot_HealthOrb";
            orb.transform.position = new Vector3(position.x, position.y + 0.55f, position.z);
            orb.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            BoxCollider col = orb.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = true;
                col.size = new Vector3(2.2f, 2.2f, 2.2f);
            }

            EnemyLootDrop drop = orb.AddComponent<EnemyLootDrop>();
            drop.SetupVisual();
        }

        private void Awake()
        {
            _basePosition = transform.position;
        }

        private void SetupVisual()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                _runtimeMaterial = new Material(shader);
                Color gold = new Color(1.0f, 0.82f, 0.18f, 1f);
                if (_runtimeMaterial.HasProperty("_BaseColor")) _runtimeMaterial.SetColor("_BaseColor", gold);
                if (_runtimeMaterial.HasProperty("_Color")) _runtimeMaterial.SetColor("_Color", gold);
                if (_runtimeMaterial.HasProperty("_Smoothness")) _runtimeMaterial.SetFloat("_Smoothness", 0.8f);
                mr.sharedMaterial = _runtimeMaterial;
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            transform.position = _basePosition + Vector3.up * (Mathf.Sin(_elapsed * bobFrequency) * bobHeight);

            if (_elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerStats stats = other.GetComponentInParent<PlayerStats>();
            if (stats == null)
            {
                return;
            }

            stats.Heal(healAmount);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }
    }
}
