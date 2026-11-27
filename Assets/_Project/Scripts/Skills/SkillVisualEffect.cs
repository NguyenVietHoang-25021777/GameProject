using UnityEngine;

namespace GameProject.Skills
{
    /// <summary>
    /// Simple procedural primitive visual effect for testing skill activations
    /// without external VFX assets (auto-expands, fades, and destroys itself cleanly).
    /// </summary>
    public class SkillVisualEffect : MonoBehaviour
    {
        private Vector3 _startScale;
        private Vector3 _endScale;
        private Vector3 _velocity;
        private Color _baseColor;
        private float _duration;
        private float _elapsed;
        private Material _runtimeMaterial;
        private Transform _followTarget;
        private Vector3 _followOffset;

        public static void SpawnShockwave(Vector3 origin, float maxRadius = 4.5f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "VFX_AreaShockwave";
            RemoveColliders(go);
            go.transform.position = origin + new Vector3(0f, 0.25f, 0f);

            SkillVisualEffect fx = go.AddComponent<SkillVisualEffect>();
            fx.Initialize(
                new Vector3(0.8f, 0.08f, 0.8f),
                new Vector3(maxRadius * 2f, 0.06f, maxRadius * 2f),
                Vector3.zero,
                new Color(0.25f, 0.85f, 1.0f, 0.75f),
                0.45f,
                null,
                Vector3.zero);
        }

        public static void SpawnLungeBurst(Transform playerTransform)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "VFX_ForwardLunge";
            RemoveColliders(go);
            go.transform.position = playerTransform.position + Vector3.up * 1.0f;
            go.transform.rotation = Quaternion.LookRotation(playerTransform.forward) * Quaternion.Euler(90f, 0f, 0f);

            SkillVisualEffect fx = go.AddComponent<SkillVisualEffect>();
            fx.Initialize(
                new Vector3(0.75f, 0.9f, 0.75f),
                new Vector3(0.25f, 2.2f, 0.25f),
                playerTransform.forward * 4f,
                new Color(0.2f, 0.65f, 1.0f, 0.7f),
                0.35f,
                null,
                Vector3.zero);
        }

        public static void SpawnHighLeapRing(Vector3 origin)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "VFX_HighLeap";
            RemoveColliders(go);
            go.transform.position = origin + new Vector3(0f, 0.12f, 0f);

            SkillVisualEffect fx = go.AddComponent<SkillVisualEffect>();
            fx.Initialize(
                new Vector3(0.9f, 0.08f, 0.9f),
                new Vector3(3.6f, 0.04f, 3.6f),
                Vector3.up * 1.2f,
                new Color(1.0f, 0.82f, 0.25f, 0.75f),
                0.42f,
                null,
                Vector3.zero);
        }

        public static void SpawnHealBurst(Transform playerTransform)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VFX_HealBurst";
            RemoveColliders(go);
            go.transform.position = playerTransform.position + Vector3.up * 1.0f;

            SkillVisualEffect fx = go.AddComponent<SkillVisualEffect>();
            fx.Initialize(
                new Vector3(0.9f, 0.9f, 0.9f),
                new Vector3(2.8f, 2.8f, 2.8f),
                Vector3.zero,
                new Color(0.25f, 0.95f, 0.45f, 0.65f),
                0.55f,
                playerTransform,
                Vector3.up * 1.0f);
        }

        public static void SpawnMeleeSlash(Transform playerTransform)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "VFX_MeleeSlash";
            RemoveColliders(go);
            go.transform.position = playerTransform.position + playerTransform.forward * 1.25f + Vector3.up * 1.05f;
            go.transform.rotation = playerTransform.rotation * Quaternion.Euler(0f, 0f, -12f);

            SkillVisualEffect fx = go.AddComponent<SkillVisualEffect>();
            fx.Initialize(
                new Vector3(0.6f, 0.12f, 0.25f),
                new Vector3(2.1f, 0.08f, 0.85f),
                playerTransform.forward * 2.5f,
                new Color(1.0f, 0.92f, 0.55f, 0.85f),
                0.18f,
                null,
                Vector3.zero);
        }

        public static void SpawnHitSpark(Vector3 worldPos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VFX_HitSpark";
            RemoveColliders(go);
            go.transform.position = worldPos;

            SkillVisualEffect fx = go.AddComponent<SkillVisualEffect>();
            fx.Initialize(
                new Vector3(0.35f, 0.35f, 0.35f),
                new Vector3(1.15f, 1.15f, 1.15f),
                Vector3.zero,
                new Color(1.0f, 0.65f, 0.2f, 0.9f),
                0.2f,
                null,
                Vector3.zero);
        }

        public static void SpawnEnemyAttackFlash(Transform enemyTransform)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "VFX_EnemyAttack";
            RemoveColliders(go);
            go.transform.position = enemyTransform.position + enemyTransform.forward * 1.15f + Vector3.up * 1.05f;
            go.transform.rotation = enemyTransform.rotation;

            SkillVisualEffect fx = go.AddComponent<SkillVisualEffect>();
            fx.Initialize(
                new Vector3(0.5f, 0.16f, 0.25f),
                new Vector3(1.6f, 0.12f, 0.7f),
                enemyTransform.forward * 2.0f,
                new Color(1.0f, 0.22f, 0.18f, 0.85f),
                0.22f,
                null,
                Vector3.zero);
        }

        private void Initialize(
            Vector3 startScale,
            Vector3 endScale,
            Vector3 velocity,
            Color color,
            float duration,
            Transform followTarget,
            Vector3 followOffset)
        {
            _startScale = startScale;
            _endScale = endScale;
            _velocity = velocity;
            _baseColor = color;
            _duration = Mathf.Max(0.05f, duration);
            _followTarget = followTarget;
            _followOffset = followOffset;

            transform.localScale = _startScale;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader);
                    ConfigureTransparentMaterial(_runtimeMaterial, _baseColor);
                    renderer.sharedMaterial = _runtimeMaterial;
                }
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float eased = 1f - (1f - t) * (1f - t);

            if (_followTarget != null)
            {
                transform.position = _followTarget.position + _followOffset;
            }
            else if (_velocity.sqrMagnitude > 0.001f)
            {
                transform.position += _velocity * Time.deltaTime;
            }

            transform.localScale = Vector3.Lerp(_startScale, _endScale, eased);

            if (_runtimeMaterial != null)
            {
                Color faded = _baseColor;
                faded.a = Mathf.Lerp(_baseColor.a, 0f, t);
                if (_runtimeMaterial.HasProperty("_BaseColor"))
                {
                    _runtimeMaterial.SetColor("_BaseColor", faded);
                }
                if (_runtimeMaterial.HasProperty("_Color"))
                {
                    _runtimeMaterial.SetColor("_Color", faded);
                }
            }

            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        private static void RemoveColliders(GameObject go)
        {
            Collider[] colliders = go.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }
        }

        private static void ConfigureTransparentMaterial(Material mat, Color color)
        {
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // Transparent in URP
            }
            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f); // Alpha blend
            }
            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }
            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", 0f);
            }

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
    }
}
