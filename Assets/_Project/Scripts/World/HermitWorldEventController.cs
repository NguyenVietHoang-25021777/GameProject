using UnityEngine;
using GameProject.Enemies;
using GameProject.Skills;

namespace GameProject.World
{
    /// <summary>
    /// Reacts to Hermit WorldStateManager outcomes in Sandbox_Test:
    /// - hermit_trust >= 1: reveals the hidden SecretSkillShrine.
    /// - hermit_trust <= -1: spawns 2 Enemy_Dummy ambushers nearby.
    /// </summary>
    public class HermitWorldEventController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldStateManager worldState;
        [SerializeField] private GameObject secretSkillShrine;
        [SerializeField] private GameObject enemyDummyPrefab;
        [SerializeField] private Transform[] ambushSpawnPoints;

        private void Awake()
        {
            if (worldState == null)
            {
                worldState = FindAnyObjectByType<WorldStateManager>();
            }
        }

        private void OnEnable()
        {
            if (worldState == null)
            {
                worldState = FindAnyObjectByType<WorldStateManager>();
            }

            if (worldState != null)
            {
                worldState.OnWorldStateChanged += HandleWorldStateChanged;
            }
        }

        private void Start()
        {
            SyncVisualStateFromWorld();
        }

        private void OnDisable()
        {
            if (worldState != null)
            {
                worldState.OnWorldStateChanged -= HandleWorldStateChanged;
            }
        }

        private void HandleWorldStateChanged(string key, int value)
        {
            if (key == "_world_state_loaded")
            {
                SyncVisualStateFromWorld();
                return;
            }

            if (key == "hermit_trust")
            {
                if (value >= 1)
                {
                    RevealSecretShrine();
                }
                else if (value <= -1)
                {
                    TriggerAmbush();
                }
            }
            else if (key == "ambush_enemies_defeated" && value >= 2)
            {
                worldState?.SetFlag("ambush_cleared", true);
            }
        }

        private void SyncVisualStateFromWorld()
        {
            if (secretSkillShrine == null)
            {
                return;
            }

            int trust = worldState != null ? worldState.GetInt("hermit_trust", 0) : 0;
            secretSkillShrine.SetActive(trust >= 1);
        }

        private void RevealSecretShrine()
        {
            if (worldState != null && worldState.HasFlag("shrine_revealed"))
            {
                return;
            }

            worldState?.SetFlag("shrine_revealed", true);

            if (secretSkillShrine != null)
            {
                secretSkillShrine.SetActive(true);
                SkillVisualEffect.SpawnHighLeapRing(secretSkillShrine.transform.position);
            }
        }

        private void TriggerAmbush()
        {
            if (worldState != null && worldState.HasFlag("ambush_triggered"))
            {
                return;
            }

            worldState?.SetFlag("ambush_triggered", true);

            if (enemyDummyPrefab == null)
            {
                return;
            }

            int spawnCount = ambushSpawnPoints != null && ambushSpawnPoints.Length > 0 ? ambushSpawnPoints.Length : 2;
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 pos = (ambushSpawnPoints != null && i < ambushSpawnPoints.Length && ambushSpawnPoints[i] != null)
                    ? ambushSpawnPoints[i].position
                    : transform.position + new Vector3(i == 0 ? -3f : 3f, 0f, -2f);

                GameObject spawned = Instantiate(enemyDummyPrefab, pos, Quaternion.identity);
                spawned.name = $"Enemy_Ambush_{i + 1}";

                EnemyController enemy = spawned.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    enemy.IsAmbushEnemy = true;
                    enemy.ForceAggro();
                }

                SkillVisualEffect.SpawnHitSpark(pos + Vector3.up * 1.0f);
            }
        }
    }
}
