using UnityEngine;

namespace GameProject.Combat
{
    /// <summary>
    /// Shared damage interface implemented by Player, enemies, and destructible objects.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, Vector3 knockbackForce);
    }
}
