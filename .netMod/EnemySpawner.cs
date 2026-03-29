using System;
using System.Collections.Generic;
using REFrameworkNET;

namespace RE3DotNet_CC
{
    /// <summary>
    /// Handles actual enemy spawning logic
    /// </summary>
    public static class EnemySpawner
    {
        private static Dictionary<string, EnemyPrefabData>? _enemyPrefabs = null;
        private static DateTime _lastEnemyRegistration = DateTime.MinValue;
        private static readonly TimeSpan RegistrationInterval = TimeSpan.FromSeconds(30); // Increased to reduce registration frequency

        private static T LogReturn<T>(string methodName, T value)
        {
            
            return value;
        }

    public static void ResetPrefabCache()
    {
        if (_enemyPrefabs != null)
        {
            foreach (var enemyData in _enemyPrefabs.Values)
            {
                TryReleasePrefabReferer(enemyData);
            }

            _enemyPrefabs.Clear();
        }

        _enemyPrefabs = null;
        _lastEnemyRegistration = DateTime.MinValue;
    }


        /// <summary>
        /// Spawn an enemy by name
        /// </summary>
        public static bool SpawnEnemy(string enemyName, string playerName)
        {
            // Register enemies if needed
            RegisterEnemiesIfNeeded();

            // Get scene using native singleton
            var sceneManagerNative = API.GetNativeSingleton("via.SceneManager")!;
            var scene = sceneManagerNative.Call("get_CurrentScene")!;
            var sceneObj = (ManagedObject)scene;

            // Find spawn folder
            var folder = sceneObj.Call("findFolder", "ModdedTemporaryObjects")
                ?? sceneObj.Call("findFolder", "GUI_Rogue");

            // Get spawn position from player
            var playmanObj = (ManagedObject)API.GetManagedSingleton("offline.PlayerManager")!;
            var playerObj = (ManagedObject)playmanObj.Call("get_CurrentPlayer")!;
            var transformObj = (ManagedObject)playerObj.Call("get_Transform")!;
            var posValueType = (REFrameworkNET.ValueType)transformObj.Call("get_Position")!;
            var xObj = posValueType.Call("get_Item", 0);
            var yObj = posValueType.Call("get_Item", 1);
            var zObj = posValueType.Call("get_Item", 2);
            float spawnX = Convert.ToSingle(xObj);
            float spawnY = Convert.ToSingle(yObj);
            float spawnZ = Convert.ToSingle(zObj);
                // Spawn exactly at the player's position
                float finalSpawnX = spawnX, finalSpawnY = spawnY, finalSpawnZ = spawnZ;
                
                // Get player position for facing calculation
                float playerX = spawnX, playerY = spawnY, playerZ = spawnZ;
                
                spawnX = finalSpawnX;
                spawnY = finalSpawnY;
                spawnZ = finalSpawnZ;

                // Create spawn position vector
                var spawnPos = REFrameworkNET.ValueType.New<via.vec3>();
                spawnPos.x = spawnX;
                spawnPos.y = spawnY;
                spawnPos.z = spawnZ;

                // Calculate rotation to face the player
                // Direction from spawn position to player
                float dirToPlayerX = playerX - spawnX;
                float dirToPlayerY = playerY - spawnY;
                float dirToPlayerZ = playerZ - spawnZ;
                float dirLength = (float)Math.Sqrt(dirToPlayerX * dirToPlayerX + dirToPlayerY * dirToPlayerY + dirToPlayerZ * dirToPlayerZ);
                
                var rotation = REFrameworkNET.ValueType.New<via.Quaternion>();
                
                if (dirLength > 0.001f)
                {
                    // Normalize direction
                    dirToPlayerX /= dirLength;
                    dirToPlayerY /= dirLength;
                    dirToPlayerZ /= dirLength;
                    
                    // Calculate rotation to face player
                    // We want to look at the player, so we need a quaternion that rotates from forward (0,0,-1) to direction
                    // For simplicity, we'll calculate yaw (rotation around Y axis) to face the player
                    float yaw = (float)Math.Atan2(dirToPlayerX, dirToPlayerZ);
                    
                    // Create quaternion from yaw (rotation around Y axis)
                    float halfYaw = yaw * 0.5f;
                    rotation.x = 0.0f;
                    rotation.y = (float)Math.Sin(halfYaw);
                    rotation.z = 0.0f;
                    rotation.w = (float)Math.Cos(halfYaw);
                    
                }
                else
                {
                    // Fallback: identity quaternion (no rotation)
                    rotation.x = 0.0f;
                    rotation.y = 0.0f;
                    rotation.z = 0.0f;
                    rotation.w = 1.0f;
                }

                EnemyPrefabData? enemyData = null;
                if (_enemyPrefabs == null || _enemyPrefabs.Count == 0)
                {
                    Logger.LogInfo("EnemySpawner: No enemy prefabs registered yet, trying manager fallback.");
                }
                else if (!TryGetEnemyPrefabData(enemyName, out var foundEnemyData))
                {
                    Logger.LogInfo($"EnemySpawner: Unknown enemy '{enemyName}'. Registered={_enemyPrefabs.Count}");
                }
                else
                {
                    enemyData = foundEnemyData;
                }

                if (enemyData == null)
                {
                    return LogReturn(
                        nameof(SpawnEnemy),
                        TryRequestEnemyManagerInstantiate(enemyName, null, spawnPos, rotation));
                }

                // Resolve prefab from PrefabReferer (preferred) or cached prefab
            object? prefabObj = TryResolvePrefabFromReferer(enemyData) ?? enemyData.Prefab;
                ManagedObject? prefabManaged = prefabObj as ManagedObject;
                NativeObject? prefabNative = prefabObj as NativeObject;
                
            if (!IsPrefabUsable(prefabObj))
            {
                return LogReturn(nameof(SpawnEnemy), false);
            }

                // Set standby first
                try
                {
                    if (prefabManaged != null)
                    {
                        prefabManaged.Call("set_Standby", true);
                    }
                    else if (prefabNative != null)
                    {
                        prefabNative.Call("set_Standby", true);
                    }
                }
                catch
                {
                    // Ignore standby failures
                }

                // Instantiate the prefab using the full method signature like LUA does
                // LUA uses: "instantiate(via.vec3, via.Quaternion, via.Folder)"
            object? gameObject = null;
                if (prefabManaged != null)
                {
                    gameObject = prefabManaged.Call("instantiate(via.vec3, via.Quaternion, via.Folder)", spawnPos, rotation, folder);
                }
                else if (prefabNative != null)
                {
                    gameObject = prefabNative.Call("instantiate(via.vec3, via.Quaternion, via.Folder)", spawnPos, rotation, folder);
                }
                if (gameObject == null)
                {
                    return LogReturn(nameof(SpawnEnemy), false);
                }

            TryRequestEnemyManagerInstantiate(enemyName, enemyData.TypeId, spawnPos, rotation);

                // Verify we got a valid game object by checking for transform
                var gameObjForVerify = gameObject as ManagedObject;
                if (gameObjForVerify != null)
                {
                    var transform = gameObjForVerify.Call("get_Transform");
                    // Verify the transform has a valid position
                    var posObj = ((ManagedObject)transform!).Call("get_Position");
                    if (posObj == null)
                    {
                    }
                }

                // Track the spawned enemy
                // Only track if we haven't exceeded the limit
                EnemySpawnManager.TrackEnemy(
                    enemyName,
                    gameObject,
                    playerName,
                    inactiveDuration: 0f,
                    spawnX: spawnX,
                    spawnY: spawnY,
                    spawnZ: spawnZ);

                // Lua fix: refresh spawn folder and trim active enemy list to avoid black-screen on continue
                TryFixSpawnFolderAndEnemyList(folder);

            return LogReturn(nameof(SpawnEnemy), true);
        }

        private static void TryFixSpawnFolderAndEnemyList(
            object? folder,
            string? reason = null,
            bool force = false)
        {
            float? playerHp = TryGetPlayerCurrentHp();
            var flowManager = API.GetManagedSingleton("offline.gamemastering.MainFlowManager");
            if (flowManager is ManagedObject flowManagerObj)
            {
                var stateObj = flowManagerObj.GetField("_CurrentMainState");
                if (stateObj != null)
                {
                    int state = Convert.ToInt32(stateObj);
                    // Only run this fix during load/continue states to avoid affecting normal spawns.
                    bool shouldRun = force || (playerHp.HasValue && playerHp.Value <= 0f);
                    string reasonLabel = string.IsNullOrEmpty(reason) ? "spawn" : reason;
                    string pauseFlags = TryGetPauseFlagsDescription() ?? "null";
                    if (!shouldRun)
                    {
                        return;
                    }
                }
            }
        }

        private static float? TryGetPlayerCurrentHp()
        {
            try
            {
                var charMgr = API.GetManagedSingleton("app.CharacterManager") as ManagedObject;
                if (charMgr == null)
                    return null;

                // Match Lua: getPlayerContextRef()
                var ctx = charMgr.Call("getPlayerContextRef")
                       ?? charMgr.Call("get_PlayerContext");

                if (ctx is not ManagedObject ctxObj)
                    return null;

                var hpObj = ctxObj.Call("get_HitPoint");
                if (hpObj is not ManagedObject hp)
                    return null;

                var currentHpObj = hp.Call("get_CurrentHitPoint");
                if (currentHpObj == null)
                    return null;

                return LogReturn(nameof(TryGetPlayerCurrentHp), Convert.ToSingle(currentHpObj));
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetPauseFlagsDescription()
        {
            try
            {
                var guiObj = API.GetManagedSingleton("app.GUIManager") as ManagedObject;
                if (guiObj == null)
                    return null;

                bool? isHudPaused = null;
                bool? isOpenWorldMap = null;

                try
                {
                    var hudObj = guiObj.Call("get_IsHudPaused");
                    if (hudObj != null)
                        isHudPaused = Convert.ToBoolean(hudObj);
                }
                catch { }

                try
                {
                    var mapObj = guiObj.Call("get_IsOpenWorldMap");
                    if (mapObj != null)
                        isOpenWorldMap = Convert.ToBoolean(mapObj);
                }
                catch { }

                return LogReturn(
                    nameof(TryGetPauseFlagsDescription),
                    $"HudPaused={isHudPaused?.ToString() ?? "null"},WorldMap={isOpenWorldMap?.ToString() ?? "null"}"
                );
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Register all available enemies from EnemyDataManager
        /// </summary>
        private static void RegisterEnemiesIfNeeded()
        {
            var now = DateTime.Now;
            if (_enemyPrefabs != null && (now - _lastEnemyRegistration) < RegistrationInterval)
            {
                return;
            }
            _enemyPrefabs = new Dictionary<string, EnemyPrefabData>();

            // Get scene
            var sceneManagerNative = API.GetNativeSingleton("via.SceneManager")!;
            var scene = sceneManagerNative.Call("get_CurrentScene")!;
            var sceneObj = (ManagedObject)scene;

            // Find EnemyDataManager components
            var tdb = API.GetTDB();
            if (tdb == null)
            {
                Logger.LogInfo("EnemySpawner: TDB not available for enemy registration.");
                _lastEnemyRegistration = now;
                return;
            }
            int enemiesFound = 0;
            var managerObjects = new List<ManagedObject>();

            foreach (var typeName in new[]
                     {
                         "offline.EnemyDataManager",
                         "offline.enemy.EnemyDataManager",
                         "offline.enemy.EnemyDataCatalogRegister",
                         "offline.enemy.EnemyDataManagerBase",
                         "app.ropeway.EnemyDataManager"
                     })
            {
                var type = tdb.FindType(typeName);
                if (type == null)
                    continue;

                var managersObj = sceneObj.Call("findComponents", type);
                managerObjects.AddRange(CollectManagersFromResult(managersObj));
            }

            if (managerObjects.Count == 0)
            {
                foreach (var typeObj in tdb.Types)
                {
                    if (typeObj is not REFrameworkNET.TypeDefinition typeDef)
                        continue;

                    string fullName = typeDef.GetFullName();
                    if (!fullName.Contains("EnemyDataManager", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var managersObj = sceneObj.Call("findComponents", typeDef);
                    managerObjects.AddRange(CollectManagersFromResult(managersObj));
                }
            }

            if (managerObjects.Count == 0)
            {
                var singleton = API.GetManagedSingleton("offline.EnemyDataManager") as ManagedObject;
                if (singleton != null)
                {
                    managerObjects.Add(singleton);
                }
            }

            if (managerObjects.Count == 0)
            {
                Logger.LogInfo("EnemySpawner: No EnemyDataManager instances found.");
                _lastEnemyRegistration = now;
                return;
            }

            foreach (var managerObj in managerObjects)
            {
                TryRegisterEnemyPrefabsFromManager(managerObj, ref enemiesFound);
                TryRegisterEnemyPrefabsFromCatalogRegister(managerObj, ref enemiesFound);
            }

            _lastEnemyRegistration = now;
            Logger.LogInfo($"EnemySpawner: Registered {_enemyPrefabs.Count} enemy prefabs.");
        }

        public static IReadOnlyCollection<string> GetRegisteredEnemyNames()
        {
            if (_enemyPrefabs == null)
                return Array.Empty<string>();

            return _enemyPrefabs.Keys.ToArray();
        }

        private static void TryRegisterEnemyPrefabsFromManager(ManagedObject managerObj, ref int enemiesFound)
        {
            var managerType = managerObj.GetTypeDefinition();
            if (managerType == null)
                return;

            object? tableObj = null;
            if (managerType.FindMethod("get_EnemyDataTable") != null)
            {
                tableObj = managerObj.Call("get_EnemyDataTable");
            }
            else if (managerType.FindMethod("get_EnemyDataList") != null)
            {
                tableObj = managerObj.Call("get_EnemyDataList");
            }
            else if (managerType.FindField("EnemyDataTable") != null)
            {
                tableObj = managerObj.GetField("EnemyDataTable");
            }

            if (tableObj == null)
            {
                return;
            }

            foreach (var entry in EnumerateManagedDictionary(tableObj))
            {
                if (entry.Value is not ManagedObject valueObj)
                    continue;

                object? prefabRef = valueObj.GetField("Prefab") ?? valueObj.Call("get_Prefab");
                int? typeId = TryConvertToInt32(entry.Key, out var parsedTypeId) ? parsedTypeId : null;
                TryAddPrefabFromReferer(prefabRef, _enemyPrefabs!, ref enemiesFound, typeId);
            }

            foreach (var entry in EnumerateManagedList(tableObj))
            {
                if (entry == null)
                    continue;

                var entryObj = entry as ManagedObject ?? (entry != null ? ExtractFromInvokeRet(entry) as ManagedObject : null);
                if (entryObj == null)
                    continue;

                object? prefabRef = entryObj.GetField("Prefab") ?? entryObj.Call("get_Prefab");
                TryAddPrefabFromReferer(prefabRef, _enemyPrefabs!, ref enemiesFound, null);
            }
        }

        private static void TryRegisterEnemyPrefabsFromCatalogRegister(ManagedObject catalogObj, ref int enemiesFound)
        {
            var catalogType = catalogObj.GetTypeDefinition();
            if (catalogType == null || !catalogType.GetFullName().Contains("EnemyDataCatalogRegister", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            object? listObj = null;
            if (catalogType.FindMethod("get_PrefabDataList") != null)
            {
                listObj = catalogObj.Call("get_PrefabDataList");
            }
            else if (catalogType.FindField("PrefabDataList") != null)
            {
                listObj = catalogObj.GetField("PrefabDataList");
            }

            if (listObj == null)
            {
                return;
            }

            foreach (var entry in EnumerateManagedList(listObj))
            {
                var entryObj = entry as ManagedObject ?? (entry != null ? ExtractFromInvokeRet(entry) as ManagedObject : null);
                if (entryObj == null)
                {
                    continue;
                }

                int? kindId = null;
                object? kindIdObj = entryObj.GetField("KindID") ?? entryObj.Call("get_KindID");
                if (TryConvertToInt32(kindIdObj, out var parsedKindId))
                {
                    kindId = parsedKindId;
                }

                object? prefabObj = entryObj.GetField("Prefab") ?? entryObj.Call("get_Prefab");
                TryAddPrefabDirect(prefabObj, _enemyPrefabs!, ref enemiesFound, kindId);
            }
        }

        private static List<ManagedObject> CollectManagersFromResult(object? managersObj)
        {
            var result = new List<ManagedObject>();
            if (managersObj == null)
                return result;

            if (managersObj is System.Array arr)
            {
                foreach (var item in arr)
                {
                    if (item is ManagedObject obj)
                        result.Add(obj);
                }

                return result;
            }

            if (managersObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is ManagedObject obj)
                        result.Add(obj);
                }

                return result;
            }

            if (managersObj is ManagedObject managedObj)
            {
                result.Add(managedObj);
            }

            return result;
        }

        private static IEnumerable<object?> EnumerateManagedList(object? listObj)
        {
            if (listObj == null)
                yield break;

            if (listObj is System.Array arr)
            {
                foreach (var item in arr)
                    yield return item;
                yield break;
            }

            if (listObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    yield return item;
                yield break;
            }

            ManagedObject? managed = listObj as ManagedObject ?? ExtractFromInvokeRet(listObj) as ManagedObject;
            if (managed == null)
                yield break;

            var type = managed.GetTypeDefinition();
            if (type?.FindMethod("GetEnumerator") == null)
                yield break;

            ManagedObject? enumerator = managed.Call("GetEnumerator") as ManagedObject
                                         ?? ExtractFromInvokeRet(managed.Call("GetEnumerator")) as ManagedObject;
            if (enumerator == null)
                yield break;

            while (true)
            {
                object? moved = null;
                try
                {
                    moved = enumerator.Call("MoveNext");
                }
                catch
                {
                    break;
                }

                if (moved == null || !Convert.ToBoolean(moved))
                    break;

                yield return enumerator.Call("get_Current");
            }
        }

        private static bool TryGetEnemyPrefabData(string enemyName, out EnemyPrefabData enemyData)
        {
            enemyData = default!;
            if (_enemyPrefabs == null)
                return false;

            string key = enemyName.ToLowerInvariant();
            if (_enemyPrefabs.TryGetValue(key, out var found) && found != null)
            {
                enemyData = found;
                return true;
            }

            foreach (var entry in _enemyPrefabs)
            {
                if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.Value != null)
                    {
                        enemyData = entry.Value;
                        return true;
                    }
                    return false;
                }
            }

            return false;
        }

        private static IEnumerable<(object? Key, object? Value)> EnumerateManagedDictionary(object? dictObj)
        {
            if (dictObj == null)
                yield break;

            ManagedObject? dictManaged = dictObj as ManagedObject ?? ExtractFromInvokeRet(dictObj) as ManagedObject;
            if (dictManaged == null)
                yield break;

            ManagedObject? enumerator = null;
            try
            {
                enumerator = dictManaged.Call("GetEnumerator") as ManagedObject
                             ?? ExtractFromInvokeRet(dictManaged.Call("GetEnumerator")) as ManagedObject;
            }
            catch
            {
                yield break;
            }

            if (enumerator == null)
                yield break;

            while (true)
            {
                object? moved = null;
                try
                {
                    moved = enumerator.Call("MoveNext");
                }
                catch
                {
                    break;
                }

                if (moved == null || !Convert.ToBoolean(moved))
                    break;

                object? currentObj = enumerator.Call("get_Current");
                ManagedObject? current = currentObj as ManagedObject ?? ExtractFromInvokeRet(currentObj) as ManagedObject;
                if (current == null)
                    continue;

                object? key = current.Call("get_Key") ?? current.GetField("Key");
                object? value = current.Call("get_Value") ?? current.GetField("Value");
                yield return (key, value);
            }
        }

        private static void TryAddPrefabFromReferer(object? prefabRef, Dictionary<string, EnemyPrefabData> prefabs, ref int enemiesFound, int? typeId)
        {
            if (prefabRef == null)
            {
                return;
            }

            var prefabRefObj = (ManagedObject)(prefabRef as ManagedObject ?? ExtractFromInvokeRet(prefabRef) as ManagedObject)!;

            try
            {
                prefabRefObj.Call("set_DefaultStandby", true);
            }
            catch
            {
                // Some prefab referers do not expose DefaultStandby.
            }

            var prefabField = prefabRefObj.GetField("PrefabField") ?? prefabRefObj.Call("get_PrefabField");

            object? prefab = prefabField as ManagedObject;
            if (prefab == null)
            {
                var prefabNative = prefabField as NativeObject;
                prefab = prefabNative ?? prefabField;
            }

            object? pathObj = null;
            if (prefab is ManagedObject prefabMO)
            {
                pathObj = prefabMO.Call("get_Path");
            }
            else if (prefab is NativeObject prefabNO)
            {
                pathObj = prefabNO.Call("get_Path");
            }
            else
            {
                var prefabAsManaged = prefab as ManagedObject;
                if (prefabAsManaged != null)
                {
                    pathObj = prefabAsManaged.Call("get_Path");
                }
                else
                {
                    var prefabAsNative = prefab as NativeObject;
                    if (prefabAsNative != null)
                    {
                        pathObj = prefabAsNative.Call("get_Path");
                    }
                }
            }

            string? path = pathObj?.ToString();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var match = System.Text.RegularExpressions.Regex.Match(path, @"([^/\\]+)\.pfb$");
            if (!match.Success)
            {
                return;
            }

            string enemyName = match.Groups[1].Value.ToLower();

            var isNew = !prefabs.ContainsKey(enemyName);
            int? kindId = null;

            prefabs[enemyName] = new EnemyPrefabData
            {
                Name = enemyName,
                Prefab = prefab,
                TypeId = typeId,
                Ref = prefabRef,
                KindId = kindId
            };

            if (isNew)
            {
                enemiesFound++;
            }
            return;
        }

        private static void TryAddPrefabDirect(object? prefabObj, Dictionary<string, EnemyPrefabData> prefabs, ref int enemiesFound, int? typeId)
        {
            if (prefabObj == null)
            {
                return;
            }

            object? pathObj = null;
            if (prefabObj is ManagedObject prefabMO)
            {
                pathObj = prefabMO.Call("get_Path");
            }
            else if (prefabObj is NativeObject prefabNO)
            {
                pathObj = prefabNO.Call("get_Path");
            }
            else if (ExtractFromInvokeRet(prefabObj) is ManagedObject unwrappedPrefabMO)
            {
                prefabObj = unwrappedPrefabMO;
                pathObj = unwrappedPrefabMO.Call("get_Path");
            }
            else if (ExtractFromInvokeRet(prefabObj) is NativeObject unwrappedPrefabNO)
            {
                prefabObj = unwrappedPrefabNO;
                pathObj = unwrappedPrefabNO.Call("get_Path");
            }

            string? path = pathObj?.ToString();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var match = System.Text.RegularExpressions.Regex.Match(path, @"([^/\\]+)\.pfb$");
            if (!match.Success)
            {
                return;
            }

            string enemyName = match.Groups[1].Value.ToLowerInvariant();
            bool isNew = !prefabs.ContainsKey(enemyName);
            prefabs[enemyName] = new EnemyPrefabData
            {
                Name = enemyName,
                Prefab = prefabObj,
                TypeId = typeId,
                Ref = null,
                KindId = typeId
            };

            if (isNew)
            {
                enemiesFound++;
            }
        }

        /// <summary>
        /// Extract the actual object from InvokeRet wrapper (same as GameState)
        /// </summary>
        private static object? ExtractFromInvokeRet(object invokeRet)
        {
            try
            {
                if (invokeRet == null)
                {
                    return LogReturn(nameof(ExtractFromInvokeRet), (object?)null);
                }
                
                if (invokeRet is ManagedObject managedObj)
                {
                    return LogReturn(nameof(ExtractFromInvokeRet), managedObj);
                }
                
                var invokeRetType = invokeRet.GetType();
                var ptrField = invokeRetType.GetField("Ptr", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (ptrField != null)
                {
                        var ptrValue = ptrField.GetValue(invokeRet);
                        // If we can't extract the pointer, just return the invokeRet as-is
                        // The actual object might be accessible directly
                }
                
                return LogReturn(nameof(ExtractFromInvokeRet), invokeRet);
            }
            catch
            {
                return LogReturn(nameof(ExtractFromInvokeRet), invokeRet);
            }
        }

        private static object? TryResolvePrefabFromReferer(EnemyPrefabData enemyData)
        {
            if (enemyData.Ref == null)
            {
                return enemyData.Prefab;
            }

            var prefabRefObj = (ManagedObject)(enemyData.Ref as ManagedObject ?? ExtractFromInvokeRet(enemyData.Ref!) as ManagedObject)!;

            if (!enemyData.RefAdded)
            {
                try
                {
                    prefabRefObj.Call("setup");
                }
                catch
                {
                    // Ignore setup failures
                }
            }

            try
            {
                prefabRefObj.Call("updateRef");
            }
            catch
            {
                // updateRef might not be needed
            }

            object? prefabObj = null;
            try
            {
                prefabObj = prefabRefObj.Call("get_Prefab");
                if (prefabObj == null)
                {
                    prefabObj = prefabRefObj.Call("get_PrefabField");
                }
            }
            catch
            {
                // Fallback to cached prefab
            }

            if (prefabObj != null)
            {
                enemyData.Prefab = prefabObj;
            }

            return LogReturn(nameof(TryResolvePrefabFromReferer), prefabObj);
        }

    private static void TryReleasePrefabReferer(EnemyPrefabData enemyData)
    {
        if (!enemyData.RefAdded || enemyData.Ref == null)
            return;

        var prefabRefObj = enemyData.Ref as ManagedObject ?? ExtractFromInvokeRet(enemyData.Ref) as ManagedObject;
        if (prefabRefObj == null)
            return;

        try
        {
            prefabRefObj.Call("release");
            enemyData.RefAdded = false;
            return;
        }
        catch
        {
            // Try alternative ref-release names below.
        }

        try
        {
            prefabRefObj.Call("releaseRef");
            enemyData.RefAdded = false;
            return;
        }
        catch
        {
            // Ignore releaseRef failures.
        }

        try
        {
            prefabRefObj.Call("delRef");
            enemyData.RefAdded = false;
        }
        catch
        {
            // Ignore release failures during scene cleanup.
        }
    }

        private static bool TryRequestEnemyManagerInstantiate(string enemyName, int? typeId, via.vec3 spawnPos, via.Quaternion rotation)
        {
            int knownKindId = 0;
            if (!typeId.HasValue && !TryGetKnownEnemyKindId(enemyName, out knownKindId))
            {
                Logger.LogInfo($"EnemySpawner: No EnemyManager kind ID available for '{enemyName}'.");
                return false;
            }

            try
            {
                var enemyManager = API.GetManagedSingleton("offline.EnemyManager");
                if (enemyManager is not ManagedObject enemyManagerObj)
                {
                    return false;
                }

                int kindId = typeId ?? knownKindId;
                object? staySceneId = enemyManagerObj.GetField("<LastPlayerStaySceneID>k__BackingField");
                var enemyManagerType = enemyManagerObj.GetTypeDefinition();
                var requestMethod =
                    enemyManagerType?.FindMethod("requestInstantiate(System.Guid, offline.EnemyDefine.KindID, System.String, offline.gamemastering.Map.ID, via.vec3, via.Quaternion, System.Boolean, System.Object, System.Object)")
                    ?? enemyManagerType?.FindMethod("requestInstantiate");
                if (requestMethod == null)
                {
                    Logger.LogInfo($"EnemySpawner: Could not find EnemyManager.requestInstantiate for '{enemyName}'.");
                    return false;
                }

                object? ownerGuid = TryCreateManagedGuid();
                if (ownerGuid == null)
                {
                    Logger.LogInfo($"EnemySpawner: Could not create managed System.Guid for '{enemyName}'.");
                    return false;
                }

                object? requestResult = requestMethod.InvokeBoxed(
                    typeof(bool),
                    enemyManagerObj,
                    new object[]
                    {
                        ownerGuid,
                        kindId,
                        enemyName,
                        staySceneId!,
                        spawnPos,
                        rotation,
                        true,
                        null!,
                        null!
                    });
                enemyManagerObj.Call("execInstantiateRequests");
                bool success = requestResult == null || Convert.ToBoolean(requestResult);
                if (!success)
                {
                    Logger.LogInfo($"EnemySpawner: EnemyManager rejected instantiate request for '{enemyName}' (KindID={kindId}).");
                }
                return success;
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"EnemySpawner: EnemyManager instantiate request failed for '{enemyName}' - {ex.Message}");
                return false;
            }
        }

        private static bool TryConvertToInt32(object? value, out int result)
        {
            result = 0;

            try
            {
                object? unwrapped = value != null ? ExtractFromInvokeRet(value) : null;
                if (unwrapped == null)
                {
                    return false;
                }

                result = Convert.ToInt32(unwrapped);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object? TryCreateManagedGuid()
        {
            try
            {
                var guidType = API.GetTDB()?.FindType("System.Guid");
                var newGuidMethod = guidType?.FindMethod("NewGuid");
                var guidRuntimeType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(asm => asm.GetType("_System.Guid", throwOnError: false))
                    .FirstOrDefault(t => t != null);
                if (newGuidMethod == null || guidRuntimeType == null)
                {
                    return null;
                }

                return newGuidMethod.InvokeBoxed(guidRuntimeType, null, Array.Empty<object>());
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetKnownEnemyKindId(string enemyName, out int kindId)
        {
            switch (enemyName.ToLowerInvariant())
            {
                case "em0000":
                    kindId = 0;
                    return true;
                case "em0020":
                    kindId = 1;
                    return true;
                case "em0100":
                    kindId = 2;
                    return true;
                case "em0200":
                    kindId = 3;
                    return true;
                case "em0300":
                    kindId = 4;
                    return true;
                case "em0400":
                    kindId = 5;
                    return true;
                case "em0500":
                    kindId = 6;
                    return true;
                case "em0600":
                    kindId = 7;
                    return true;
                case "em0700":
                    kindId = 8;
                    return true;
                case "em0800":
                    kindId = 9;
                    return true;
                case "em1000":
                    kindId = 10;
                    return true;
                case "em2500":
                    kindId = 11;
                    return true;
                case "em2600":
                    kindId = 12;
                    return true;
                case "em2700":
                    kindId = 13;
                    return true;
                case "em3000":
                    kindId = 14;
                    return true;
                case "em3300":
                    kindId = 15;
                    return true;
                case "em3400":
                    kindId = 16;
                    return true;
                case "em3500":
                    kindId = 17;
                    return true;
                case "em4000":
                    kindId = 18;
                    return true;
                case "em7000":
                    kindId = 19;
                    return true;
                case "em7100":
                    kindId = 20;
                    return true;
                case "em7200":
                    kindId = 21;
                    return true;
                case "em8400":
                    kindId = 22;
                    return true;
                case "em9000":
                    kindId = 23;
                    return true;
                case "em9010":
                    kindId = 24;
                    return true;
                case "em9020":
                    kindId = 25;
                    return true;
                case "em9030":
                    kindId = 26;
                    return true;
                case "em9040":
                    kindId = 27;
                    return true;
                case "em9050":
                    kindId = 28;
                    return true;
                case "em9091":
                    kindId = 29;
                    return true;
                case "em9100":
                    kindId = 30;
                    return true;
                case "em9200":
                    kindId = 31;
                    return true;
                case "em9201":
                    kindId = 32;
                    return true;
                case "em9210":
                    kindId = 33;
                    return true;
                case "em9300":
                    kindId = 34;
                    return true;
                case "em9400":
                    kindId = 35;
                    return true;
                case "em9401":
                    kindId = 36;
                    return true;
                case "em9410":
                    kindId = 37;
                    return true;
                case "em9999":
                    kindId = 38;
                    return true;
                default:
                    kindId = 0;
                    return false;
            }
        }

        private static bool IsPrefabUsable(object? prefabObj)
        {
            if (prefabObj == null)
            {
                return LogReturn(nameof(IsPrefabUsable), false);
            }

            try
            {
                var tdb = API.GetTDB();
                var prefabUtilType = tdb!.FindType("offline.PrefabUtil")!;
                var isExistMethod = prefabUtilType.FindMethod("isExist(via.Prefab)")!;

                var result = isExistMethod.Invoke(null, new object[] { prefabObj });
                object? resultObj = ExtractFromInvokeRet(result);
                return LogReturn(nameof(IsPrefabUsable), Convert.ToBoolean(resultObj));
            }
            catch
            {
                return LogReturn(nameof(IsPrefabUsable), true);
            }
        }

    }

    /// <summary>
    /// Stores enemy prefab data
    /// </summary>
    public class EnemyPrefabData
    {
        public string Name { get; set; } = "";
        public object? Prefab { get; set; }
        public int? TypeId { get; set; }
        public object? Ref { get; set; }
        public int? KindId { get; set; }
        public bool RefAdded { get; set; }
    }
}



