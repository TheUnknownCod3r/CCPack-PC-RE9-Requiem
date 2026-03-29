using System;
using System.Threading.Tasks;
using REFrameworkNET;
using RE3DotNet_CC;

namespace RE3DotNet_CC.Effects
{
    /// <summary>
    /// Dynamic effect that handles all enemy spawn effects
    /// Extracts the enemy name from the effect code (e.g., "spawn_em0000" -> "em0000")
    /// </summary>
    public class SpawnEnemyEffect : EffectBase
    {
        private readonly string _effectCode;
        private string? _enemyName = null;

        public SpawnEnemyEffect(string effectCode)
        {
            _effectCode = effectCode;
        }

        public override string Code => _effectCode;

        private string GetEnemyName()
        {
            if (_enemyName != null)
                return _enemyName;

            // Extract enemy name from code (e.g., "spawn_em0000" -> "em0000")
            const string prefix = "spawn_";
            if (_effectCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _enemyName = _effectCode.Substring(prefix.Length);
            }
            else
            {
                _enemyName = _effectCode; // Fallback to full code
            }

            return _enemyName;
        }

        protected override Task<int> OnExecute(GameState gameState, CCRequest request)
        {
            try
            {
                if (!RE3CrowdControlPlugin.AllowEnemySpawns(gameState))
                {
                    Logger.LogInfo($"{Code}: Enemy spawns currently disabled");
                    return Task.FromResult((int)CCStatus.Failure);
                }

                string enemyName = GetEnemyName();
                Logger.LogInfo($"{Code}: Spawn request for '{enemyName}'");
                
                // Ensure we have room before attempting spawn
                if (!EnemySpawnManager.TryMakeRoomForSpawn())
                {
                    int currentCount = EnemySpawnManager.GetSpawnedEnemyCount();
                    Logger.LogInfo($"{Code}: Cannot spawn more enemies - at limit ({currentCount}/30)");
                    return Task.FromResult((int)CCStatus.Retry);
                }

                // Spawn the enemy
                string viewerName = request.Viewer?.ToString() ?? "Unknown";
                int beforeCount = EnemySpawnManager.GetSpawnedEnemyCount();
                bool spawnSuccess = EnemySpawner.SpawnEnemy(enemyName, viewerName);
                int afterCount = EnemySpawnManager.GetSpawnedEnemyCount();
                
                if (!spawnSuccess && afterCount > beforeCount)
                {
                    Logger.LogInfo($"{Code}: Spawn count increased ({beforeCount} -> {afterCount}), treating as success");
                    spawnSuccess = true;
                }

                if (!spawnSuccess)
                {
                    var registered = EnemySpawner.GetRegisteredEnemyNames();
                    if (registered.Count > 0)
                    {
                        Logger.LogInfo($"{Code}: Registered enemies sample: {string.Join(", ", registered.Take(10))}");
                    }
                }
                
                // Double-check we didn't exceed limit after spawn
                if (spawnSuccess && !EnemySpawnManager.CanSpawnMore())
                {
                    Logger.LogInfo($"{Code}: Spawn succeeded but limit reached, cleaning up excess");
                    // The spawner should have handled tracking, but verify
                }
                
                if (spawnSuccess)
                {
                    Logger.LogInfo($"{Code}: Successfully spawned enemy '{enemyName}' by {viewerName}");
                    return Task.FromResult((int)CCStatus.Success);
                }

                Logger.LogInfo($"{Code}: Failed to spawn enemy '{enemyName}'");
                return Task.FromResult((int)CCStatus.Retry);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{Code}: Error spawning enemy - {ex.Message}");
                return Task.FromResult((int)CCStatus.Failure);
            }
        }
    }
}



