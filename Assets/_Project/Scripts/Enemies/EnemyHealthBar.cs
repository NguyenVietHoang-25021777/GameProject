using UnityEngine;

namespace GameProject.Enemies
{
    /// <summary>
    /// Simple 3D world-space overhead health bar that smoothly billboards toward Camera.main.
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private Transform fillPivot;
        [SerializeField] private MeshRenderer fillRenderer;

        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }

        public void SetNormalizedHealth(float normalized)
        {
            float clamped = Mathf.Clamp01(normalized);

            if (fillPivot != null)
            {
                Vector3 scale = fillPivot.localScale;
                scale.x = clamped;
                fillPivot.localScale = scale;
            }

            if (fillRenderer != null)
            {
                if (_propBlock == null)
                {
                    _propBlock = new MaterialPropertyBlock();
                }

                Color barColor = Color.Lerp(new Color(1.0f, 0.15f, 0.15f, 1f), new Color(0.95f, 0.35f, 0.18f, 1f), clamped);
                fillRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(BaseColorId, barColor);
                _propBlock.SetColor(ColorId, barColor);
                fillRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}
