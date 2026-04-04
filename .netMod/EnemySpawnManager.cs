using System;
using System.Collections.Generic;
using REFrameworkNET;

namespace RE9DotNet_CC
{
    /// <summary>
    /// Manages enemy spawning and tracking for nameplate display
    /// </summary>
    public class EnemySpawnManager
    {
        private static readonly Dictionary<string, SpawnedEnemy> _spawnedEnemies = new();
        private static int _maxSpawnedEnemies = 20;
        private static readonly bool UseEnemyManagerRegistration = false;

        public static bool IsEnemyManagerRegistrationEnabled()
        {
            return UseEnemyManagerRegistration;
        }

        public static int GetMaxSpawnedEnemies()
        {
            return _maxSpawnedEnemies;
        }

        public static void SetMaxSpawnedEnemies(int maxEnemies)
        {
            _maxSpawnedEnemies = Math.Clamp(maxEnemies, 1, 25);
        }
        private static DateTime _lastCleanupTime = DateTime.MinValue;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(5);
        private static readonly Dictionary<string, DateTime> _lastStatusLogTime = new();
        private static readonly TimeSpan StatusLogInterval = TimeSpan.FromSeconds(3);
        // Delayed activation removed

        /// <summary>
        /// Track a newly spawned enemy
        /// </summary>
        public static void TrackEnemy(
            string enemyName,
            object gameObject,
            string playerName,
            float inactiveDuration = 1.5f,
            float? spawnX = null,
            float? spawnY = null,
            float? spawnZ = null)
        {
            try
            {
                if (_spawnedEnemies.Count >= _maxSpawnedEnemies)
                {
                    if (!TryDespawnFarthestEnemy())
                    {
                        Logger.LogInfo($"EnemySpawnManager: Max enemies reached ({_maxSpawnedEnemies}), cannot track more");
                    return;
                    }
                }

                // Generate unique ID
                string enemyId = $"{gameObject.GetHashCode()}_{DateTime.Now.Ticks}_{new Random().Next(1000, 9999)}";

                // Try to get EnemyController
                object? enemyController = null;
                try
                {
                    var gameObj = gameObject as ManagedObject;
                    if (gameObj != null)
                    {
                        // Use TDB to get component type
                        var tdb = API.GetTDB();
                        if (tdb != null)
                        {
                            var enemyControllerType = tdb.FindType("app.CharacterManager");
                            if (enemyControllerType != null)
                            {
                                enemyController = gameObj.Call("getComponent", enemyControllerType);
                            }
                        }
                    }
                }
                catch
                {
                    // EnemyController not available, that's okay
                }

                // Register the enemy with EnemyManager lists so game systems can track it
                TryRegisterWithEnemyManager(enemyController);

                // Only set lightweight room/awareness flags when available.
                TryMarkEnemyInRoom(enemyController);

                _spawnedEnemies[enemyId] = new SpawnedEnemy
                {
                    Id = enemyId,
                    Name = enemyName,
                    GameObject = gameObject,
                    EnemyController = enemyController,
                    PlayerName = playerName,
                    SpawnTime = DateTime.Now,
                    LastSeen = DateTime.Now,
                    ActivationTime = DateTime.Now, // No delay - activate immediately
                    IsInactive = false, // Start active
                    LastX = spawnX ?? 0f,
                    LastY = spawnY ?? 0f,
                    LastZ = spawnZ ?? 0f,
                    HasLastPosition = spawnX.HasValue && spawnY.HasValue && spawnZ.HasValue
                };

                Logger.LogInfo($"EnemySpawnManager: Tracked enemy '{enemyName}' (ID: {enemyId}) by {playerName}. Total: {_spawnedEnemies.Count}/{_maxSpawnedEnemies}");
            }
            catch (Exception ex)
                                {
                Logger.LogError($"EnemySpawnManager: Error tracking enemy - {ex.Message}");
                                    }
                                }

        private static void TryRegisterWithEnemyManager(object? enemyController)
        {
            if (!UseEnemyManagerRegistration)
                return;

            if (enemyController == null)
                return;

            try
            {
                var enemyManager = API.GetManagedSingleton("app.CharacterManager");
                var enemyManagerObj = enemyManager as ManagedObject;
                if (enemyManagerObj == null)
                    return;

                var managerType = enemyManagerObj.GetTypeDefinition();
                if (managerType?.FindMethod("registerCharacter") != null)
                                                                {
                    enemyManagerObj.Call("registerCharacter", enemyController);
                }
                else
                {
                    // Fallback: only touch ActiveEnemyList (EnemyList is RegisterEnemyInfo, not EnemyController)
                    TryAddEnemyToActiveList(enemyManagerObj, enemyController);
                }

                if (managerType?.FindMethod("set_CharacterPoolUpdated") != null)
                                                                                    {
                    enemyManagerObj.Call("set_CharacterPoolUpdated", true);
                                                                            }
                                                                        }
            catch
            {
                // If registration fails, ignore and continue.
            }
        }

        private static void TryMarkEnemyInRoom(object? enemyController)
        {
            if (enemyController is not ManagedObject controllerObj)
                return;

                                                                        try
                                                                        {
                var typeDef = controllerObj.GetTypeDefinition();
                if (typeDef?.FindMethod("set_IsSameArea") != null)
                {
                    controllerObj.Call("set_IsSameArea", true);
                                                                        }

                if (typeDef?.FindMethod("set_IsInSight") != null)
                {
                    controllerObj.Call("set_IsInSight", true);
                }
                                                                        }
            catch
            {
                // Ignore if these flags are not supported on this enemy.
            }
        }

        private static void TryAddEnemyToActiveList(
            ManagedObject enemyManagerObj,
            object enemyController)
        {
            try
            {
                ManagedObject? listObj = null;
                var listField = enemyManagerObj.Call("getSpawnedEnemyContextRefList") as ManagedObject;
                if (listField is ManagedObject listFieldObj)
                {
                    listObj = listFieldObj;
                }

                if (listObj == null)
                    return;

                var listType = listObj.GetTypeDefinition();
                if (listType?.FindMethod("Contains") != null)
                {
                    var contains = listObj.Call("Contains", enemyController);
                    if (contains != null && Convert.ToBoolean(contains))
                        return;
                }

                if (listType?.FindMethod("Add") != null)
                {
                    listObj.Call("Add", enemyController);
                }
            }
            catch
            {
                // Ignore list registration failures.
            }
        }

        /// <summary>
        /// Process pending enemy activations (removed)
        /// </summary>
        public static void ProcessPendingActivations()
        {
            // No-op: delayed activation removed
        }

        /// <summary>
        /// Activate enemy target finding via EnemyHateController
        /// </summary>
        private static void ActivateEnemyTargetFinding(SpawnedEnemy enemy)
        {
            // Delayed activation removed to avoid get_GameObject lookups.
                    return;
        }

        /// <summary>
        /// Log detailed enemy status for debugging
        /// </summary>
        private static void LogEnemyStatus(string enemyName, string enemyId, object? enemyController, string context)
        {
            if (enemyController == null)
            {
                Logger.LogInfo($"EnemyStatus[{context}]: '{enemyName}' (ID: {enemyId}) - No EnemyController");
                return;
            }

            try
            {
                var controllerObj = enemyController as ManagedObject;
                if (controllerObj == null)
                {
                    Logger.LogInfo($"EnemyStatus[{context}]: '{enemyName}' (ID: {enemyId}) - EnemyController is not ManagedObject");
                    return;
                }

                var status = new System.Text.StringBuilder();
                status.Append($"EnemyStatus[{context}]: '{enemyName}' (ID: {enemyId}) - ");

                // Try to get all relevant properties
                try
                {
                    var thinkEnable = controllerObj.Call("get_ThinkEnable");
                    status.Append($"ThinkEnable={thinkEnable}, ");
                }
                catch { status.Append("ThinkEnable=?, "); }

                try
                {
                    var attackEnable = controllerObj.Call("get_AttackEnable");
                    status.Append($"AttackEnable={attackEnable}, ");
                }
                catch { status.Append("AttackEnable=?, "); }

                try
                {
                    var hasAttackAuthority = controllerObj.Call("get_HasAttackAuthority");
                    status.Append($"HasAttackAuthority={hasAttackAuthority}, ");
                }
                catch { status.Append("HasAttackAuthority=?, "); }

                try
                {
                    var addReactionEnable = controllerObj.Call("get_AddReactionEnable");
                    status.Append($"AddReactionEnable={addReactionEnable}, ");
                }
                catch { status.Append("AddReactionEnable=?, "); }

                try
                {
                    var isSoundActive = controllerObj.Call("get_IsSoundActive");
                    status.Append($"IsSoundActive={isSoundActive}, ");
                }
                catch { status.Append("IsSoundActive=?, "); }

                try
                {
                    var isInSight = controllerObj.Call("get_IsInSight");
                    status.Append($"IsInSight={isInSight}, ");
                }
                catch { status.Append("IsInSight=?, "); }

                try
                {
                    var isSameArea = controllerObj.Call("get_IsSameArea");
                    status.Append($"IsSameArea={isSameArea}, ");
                }
                catch { status.Append("IsSameArea=?, "); }

                try
                {
                    var isTargetFind = controllerObj.Call("get_IsTargetFind");
                    status.Append($"IsTargetFind={isTargetFind}, ");
                }
                catch { status.Append("IsTargetFind=?, "); }

                try
                {
                    var isTargetAttention = controllerObj.Call("get_IsTargetAttention");
                    status.Append($"IsTargetAttention={isTargetAttention}, ");
                }
                catch { status.Append("IsTargetAttention=?, "); }

                try
                {
                    var isTargetLost = controllerObj.Call("get_IsTargetLost");
                    status.Append($"IsTargetLost={isTargetLost}, ");
                }
                catch { status.Append("IsTargetLost=?, "); }

                try
                {
                    var targetFindState = controllerObj.Call("get_TargetFindState");
                    status.Append($"TargetFindState={targetFindState}, ");
                }
                catch { status.Append("TargetFindState=?, "); }

                try
                {
                    var hitPoint = controllerObj.Call("get_HitPoint");
                    if (hitPoint != null)
                    {
                        var hitPointObj = hitPoint as ManagedObject;
                        if (hitPointObj != null)
                        {
                            try
                            {
                                var currentHp = hitPointObj.Call("get_CurrentHitPoint");
                                status.Append($"HP={currentHp}, ");
                            }
                            catch { status.Append("HP=?, "); }

                            try
                            {
                                var isDead = hitPointObj.Call("get_IsDead");
                                status.Append($"IsDead={isDead}");
                            }
                            catch { status.Append("IsDead=?"); }
                        }
                    }
                }
                catch { status.Append("HitPoint=?"); }

                Logger.LogInfo(status.ToString());
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"EnemyStatus[{context}]: '{enemyName}' (ID: {enemyId}) - Error logging status: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up dead/destroyed enemies
        /// </summary>
        public static void CleanupDeadEnemies()
        {
            try
            {
                var now = DateTime.Now;
                if (now - _lastCleanupTime < CleanupInterval)
                    return;

                _lastCleanupTime = now;

                var toRemove = new List<string>();
                ManagedObject? activeList = TryGetActiveEnemyList();

                foreach (var kvp in _spawnedEnemies)
                {
                    var enemy = kvp.Value;
                    bool isDead = false;

                    try
                    {
                        if (now - enemy.SpawnTime < TimeSpan.FromSeconds(5))
                        {
                            enemy.LastSeen = now;
                            continue;
                        }

                        if (enemy.GameObject == null)
                        {
                            if (now - enemy.SpawnTime > TimeSpan.FromSeconds(5))
                            {
                                isDead = true;
                            }
                        }
                        else
                        {
                            if (!IsGameObjectValid(enemy.GameObject))
                            {
                                isDead = true;
                            }
                            else
                            {
                                enemy.LastSeen = now;
                        }
                        }

                        if (!isDead && enemy.EnemyController is ManagedObject controllerObj)
                        {
                            if (!IsManagedObjectValid(controllerObj))
                            {
                                isDead = true;
                            }
                            else if (activeList != null && !ActiveListContains(activeList, controllerObj))
                            {
                                isDead = true;
                            }
                            else if (IsEnemyDead(controllerObj))
                            {
                                isDead = true;
                            }
                        }

                        if (enemy.EnemyController == null && now - enemy.SpawnTime > TimeSpan.FromSeconds(30))
                            {
                                isDead = true;
                            }
                    }
                    catch
                    {
                        if (now - enemy.SpawnTime > TimeSpan.FromSeconds(30))
                            {
                                isDead = true;
                            }
                    }

                    if (isDead)
                    {
                        if (!enemy.DeadMarkedTime.HasValue)
                        {
                            enemy.DeadMarkedTime = now;
                        }

                        if (now - enemy.DeadMarkedTime.Value >= TimeSpan.FromSeconds(3))
                        {
                            // Try to despawn dead bodies to avoid buildup.
                            TryDespawnEnemy(enemy);
                            toRemove.Add(kvp.Key);
                        }
                    }
                }

                    foreach (var id in toRemove)
                    {
                        _spawnedEnemies.Remove(id);
                        _lastStatusLogTime.Remove(id); // Clean up status log tracking
                    }

                if (toRemove.Count > 0)
                {
                    Logger.LogInfo($"EnemySpawnManager: Cleaned up {toRemove.Count} dead enemies. Remaining: {_spawnedEnemies.Count}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"EnemySpawnManager: Error cleaning up enemies - {ex.Message}");
            }
        }

        private static bool TryDespawnFarthestEnemy()
        {
            float refX;
            float refY;
            float refZ;
            bool hasRefPos = TryGetPlayerPosition(out refX, out refY, out refZ);
            if (!hasRefPos)
            {
                hasRefPos = TryGetCameraPosition(out refX, out refY, out refZ);
            }

            string? farthestId = null;
            float farthestDistSq = -1f;
            DateTime oldestTime = DateTime.MaxValue;
            string? oldestId = null;

            foreach (var kvp in _spawnedEnemies)
            {
                var enemy = kvp.Value;
                if (hasRefPos && enemy.HasLastPosition)
                {
                    float dx = enemy.LastX - refX;
                    float dy = enemy.LastY - refY;
                    float dz = enemy.LastZ - refZ;
                    float distSq = (dx * dx) + (dy * dy) + (dz * dz);
                    if (distSq > farthestDistSq)
                    {
                        farthestDistSq = distSq;
                        farthestId = kvp.Key;
                    }
                }

                if (enemy.SpawnTime < oldestTime)
                {
                    oldestTime = enemy.SpawnTime;
                    oldestId = kvp.Key;
                }
            }

            if (farthestId == null)
                farthestId = oldestId;

            if (farthestId == null || !_spawnedEnemies.TryGetValue(farthestId, out var farthestEnemy))
                return false;

            TryDespawnEnemy(farthestEnemy);

            _spawnedEnemies.Remove(farthestId);
            _lastStatusLogTime.Remove(farthestId);
            Logger.LogInfo($"EnemySpawnManager: Despawned farthest/oldest enemy '{farthestEnemy.Name}' (ID: {farthestId}) to make room");
            return true;
        }

        private static bool TryGetPlayerPosition(out float x, out float y, out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;
                                    
                                    try
                                    {
                var playman = API.GetManagedSingleton("app.CharacterManager");
                if (playman is not ManagedObject playmanObj)
                    return false;

                var player = playmanObj.Call("getPlayerContextRefFast");
                if (player is not ManagedObject playerObj)
                    return false;

                var playerTransform = playerObj.Call("get_Transform");
                if (playerTransform is not ManagedObject transformObj)
                    return false;

                var playerPosObj = transformObj.Call("get_Position");
                if (playerPosObj is not REFrameworkNET.ValueType posValueType)
                    return false;

                var xObj = posValueType.Call("get_Item", 0);
                var yObj = posValueType.Call("get_Item", 1);
                var zObj = posValueType.Call("get_Item", 2);
                if (xObj == null || yObj == null || zObj == null)
                    return false;

                x = Convert.ToSingle(xObj);
                y = Convert.ToSingle(yObj);
                z = Convert.ToSingle(zObj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetCameraPosition(out float x, out float y, out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;

            try
            {
                var cameraManager = API.GetManagedSingleton("offline.CameraManager");
                if (cameraManager is not ManagedObject cameraManagerObj)
                    return false;

                var camera = cameraManagerObj.Call("get_CurrentCamera") ?? cameraManagerObj.Call("get_MainCamera");
                if (camera is not ManagedObject cameraObj)
                    return false;

                var cameraTransform = cameraObj.Call("get_Transform");
                if (cameraTransform is not ManagedObject transformObj)
                    return false;

                var cameraPosObj = transformObj.Call("get_Position");
                if (cameraPosObj is not REFrameworkNET.ValueType posValueType)
                    return false;

                var xObj = posValueType.Call("get_Item", 0);
                var yObj = posValueType.Call("get_Item", 1);
                var zObj = posValueType.Call("get_Item", 2);
                if (xObj == null || yObj == null || zObj == null)
                    return false;

                x = Convert.ToSingle(xObj);
                y = Convert.ToSingle(yObj);
                z = Convert.ToSingle(zObj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDespawnEnemy(SpawnedEnemy enemy)
        {
            try
            {
                var enemyManager = API.GetManagedSingleton("app.CharacterManager");
                var enemyManagerObj = enemyManager as ManagedObject;
                var managerType = enemyManagerObj?.GetTypeDefinition();

                if (enemyManagerObj != null
                    && enemy.EnemyController != null
                    && managerType?.FindMethod("unregisterCharacter") != null)
                {
                    enemyManagerObj.Call("unregisterCharacter", enemy.EnemyController);
                }
            }
            catch
            {
                // Ignore unregister errors.
            }

            try
            {
                if (enemy.GameObject != null && IsGameObjectValid(enemy.GameObject))
                                                                            {
                    var tdb = API.GetTDB();
                    var gameObjectType = tdb?.FindType("via.GameObject");
                    var destroyMethod = gameObjectType?.FindMethod("destroy(via.GameObject)");
                    if (destroyMethod != null)
                                                                                {
                        destroyMethod.Invoke(null, new object[] { enemy.GameObject });
                    }
                    else if (enemy.GameObject is ManagedObject gameObj)
                    {
                        var gameObjType = gameObj.GetTypeDefinition();
                        if (gameObjType?.FindMethod("destroy") != null)
                        {
                            gameObj.Call("destroy");
                        }
                    }
                }
            }
            catch
            {
                // Ignore destroy errors.
            }
        }

        public static void TryRequestDestroyAllTracked()
        {
            try
            {
                foreach (var enemy in _spawnedEnemies.Values)
                {
                    TryDespawnEnemy(enemy);
                }
            }
            catch
            {
                // Ignore failure.
            }
        }
                        
        private static bool TryParseEnemyKindId(string enemyName, out int kindId)
            {
            kindId = 0;
            return false;
        }

        private static bool IsGameObjectValid(object? gameObject)
        {
            if (gameObject is not ManagedObject managedObj)
                return true;

            try
            {
                var typeDef = managedObj.GetTypeDefinition();
                if (typeDef?.FindMethod("get_Valid") != null)
                {
                    var valid = managedObj.Call("get_Valid");
                    if (valid != null)
                        return Convert.ToBoolean(valid);
                }

                if (typeDef?.FindMethod("get_IsValid") != null)
                {
                    var valid = managedObj.Call("get_IsValid");
                    if (valid != null)
                        return Convert.ToBoolean(valid);
                                }
                            }
                            catch
                            {
                // Assume valid if we can't query it safely.
            }

            return true;
        }

        private static bool IsEnemyDead(object enemyController)
        {
            if (enemyController is not ManagedObject controllerObj)
                return false;

            try
                                {
                var hitPoint = controllerObj.Call("get_HitPoint");
                if (hitPoint is not ManagedObject hitPointObj)
                    return false;

                var hitPointType = hitPointObj.GetTypeDefinition();
                if (hitPointType?.FindMethod("get_IsDead") != null)
                                        {
                    var isDead = hitPointObj.Call("get_IsDead");
                    if (isDead != null && Convert.ToBoolean(isDead))
                        return true;
                }

                if (hitPointType?.FindMethod("get_CurrentHitPoint") != null)
                {
                    var hp = hitPointObj.Call("get_CurrentHitPoint");
                    if (hp != null && Convert.ToSingle(hp) <= 0f)
                        return true;
                }
            }
            catch
            {
                // Ignore and keep tracking.
            }

            return false;
        }

        private static ManagedObject? TryGetActiveEnemyList()
        {
            if (!UseEnemyManagerRegistration)
                return null;

            try
            {
                var enemyManager = API.GetManagedSingleton("app.CharacterManager");
                if (enemyManager is not ManagedObject enemyManagerObj)
                    return null;

                var listField = enemyManagerObj.Call("getSpawnedEnemyContextRefList");
                if (listField is ManagedObject listFieldObj)
                    return listFieldObj;

                var managerType = enemyManagerObj.GetTypeDefinition();
                if (managerType?.FindMethod("get_ActiveEnemyList") != null)
                {
                    return enemyManagerObj.Call("get_ActiveEnemyList") as ManagedObject;
                                }
                            }
                            catch
                            {
                // Ignore list retrieval failures.
            }

            return null;
        }

        private static bool ActiveListContains(ManagedObject activeList, ManagedObject enemyController)
                    {
            try
            {
                var listType = activeList.GetTypeDefinition();
                if (listType?.FindMethod("Contains") != null)
                {
                    var contains = activeList.Call("Contains", enemyController);
                    if (contains != null)
                        return Convert.ToBoolean(contains);
                }
            }
            catch
            {
                // Ignore contains failures.
            }

            return true;
        }

        private static bool IsManagedObjectValid(ManagedObject managedObj)
        {
            try
            {
                var typeDef = managedObj.GetTypeDefinition();
                if (typeDef?.FindMethod("get_Valid") != null)
                {
                    var valid = managedObj.Call("get_Valid");
                    if (valid != null)
                        return Convert.ToBoolean(valid);
                    }

                if (typeDef?.FindMethod("get_IsValid") != null)
                {
                    var valid = managedObj.Call("get_IsValid");
                    if (valid != null)
                        return Convert.ToBoolean(valid);
                }
            }
            catch
            {
                // Assume valid if we can't query it safely.
            }

            return true;
        }

        /// <summary>
        /// Get all currently tracked enemies
        /// </summary>
        public static IEnumerable<SpawnedEnemy> GetTrackedEnemies()
        {
            return _spawnedEnemies.Values;
        }

        /// <summary>
        /// Get count of spawned enemies
        /// </summary>
        public static int GetSpawnedEnemyCount()
        {
            return _spawnedEnemies.Count;
        }

        /// <summary>
        /// Expose max count for UI/logic checks
        /// </summary>
        /// <summary>
        /// True when we are at or above the configured max
        /// </summary>
        public static bool IsAtOrAboveLimit()
        {
            return _spawnedEnemies.Count >= _maxSpawnedEnemies;
        }

        /// <summary>
        /// Check if we can spawn more enemies
        /// </summary>
        public static bool CanSpawnMore()
        {
            return _spawnedEnemies.Count < _maxSpawnedEnemies;
        }

        /// <summary>
        /// Ensure there is room to spawn by removing a far/old enemy if needed.
        /// </summary>
        public static bool TryMakeRoomForSpawn()
        {
            CleanupDeadEnemies();
            if (_spawnedEnemies.Count < _maxSpawnedEnemies)
                return true;

            return TryDespawnFarthestEnemy();
        }

        public static void TickCleanup()
        {
            CleanupDeadEnemies();
        }

        /// <summary>
        /// Clear all tracked enemies
        /// </summary>
        public static void ClearAll()
        {
            int count = _spawnedEnemies.Count;
            _spawnedEnemies.Clear();
            _lastStatusLogTime.Clear();
            Logger.LogInfo($"EnemySpawnManager: Cleared all {count} tracked enemies");
        }
    }

    /// <summary>
    /// Represents a spawned enemy being tracked
    /// </summary>
    public class SpawnedEnemy
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public object? GameObject { get; set; }
        public object? EnemyController { get; set; }
        public string PlayerName { get; set; } = "";
        public DateTime SpawnTime { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime ActivationTime { get; set; } // When enemy becomes active/attacks
        public bool IsInactive { get; set; } = true; // Starts inactive
        public float LastX { get; set; }
        public float LastY { get; set; }
        public float LastZ { get; set; }
        public bool HasLastPosition { get; set; }
        public DateTime? DeadMarkedTime { get; set; }
    }
}



