using app;
using REFrameworkNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using via;

namespace RE9DotNet_CC
{
    /// <summary>
    /// Manages and checks game state for effect execution
    /// Provides reusable state checking methods for all effects
    /// </summary>
    public class GameState
    {
        private static string _lastPlayerNameLog = "";
        private object? _playerManager;
        private object? _mainFlowManager;
        private object? _currentPlayerCondition;
        private object? _hitPointController;
        private bool _hadPlayerModel = false;
        private bool _playerModelMissingLogged = false;
        private ManagedObject? _currentPostEffectParam;
        private string? _currentPostEffectName;
        private string? _lastPostEffectListSignature;
        private string? _lastAccessoryListSignature;
        private HashSet<string>? _accessorySceneSnapshot;
        private string? _lastSceneRootLookupSignature;
        private readonly HashSet<string> _loggedFolderTypeSignatures = new(StringComparer.Ordinal);
        private readonly HashSet<string> _loggedListTypeSignatures = new(StringComparer.Ordinal);

        // Cached values
        private float _currentHealth = 0;
        private float _maxHealth = 0;
        private bool _isPlayerAlive = false;
        private bool _isGameReady = false;
        private bool _isInvulnerable = false;
        private bool _isOneHitKO = false;

        // OHKO tracking
        private float? _ohkoOriginalHealth = null;
        private float _ohkoTimer = -1.0f; // Timer in seconds
        private int _ohkoRequestId = 0;
        private string? _ohkoRequestID = null;

        // Invincibility tracking
        private float? _invulOriginalHealth = null;
        private float _invulTimer = -1.0f; // Timer in seconds
        private int _invulRequestId = 0;
        private string? _invulRequestID = null;
        private bool _invulWasPaused = false;

        private bool IsLoggingEnabled => RE9CrowdControlPlugin.IsLoggingEnabled;

        private void LogInfo(string message)
        {
            if (IsLoggingEnabled)
                Logger.LogInfo(message);
        }

        private void LogWarning(string message)
        {
            if (IsLoggingEnabled)
                Logger.LogWarning(message);
        }

        private void LogError(string message)
        {
            if (IsLoggingEnabled)
                Logger.LogError(message);
        }

        // OHKO pause tracking
        private bool _ohkoWasPaused = false;

        // Camera/FOV tracking
        private float? _originalFOV = null;
        private float _fovTimer = -1.0f; // Timer in seconds
        private int _fovRequestId = 0;
        private string? _fovRequestID = null;
        private bool _fovWasPaused = false;
        private float _targetFOV = 81.0f; // Default FOV
        private bool _fovActive = false;

        // Player scale tracking
        private float? _originalScale = null;
        private float _scaleTimer = -1.0f; // Timer in seconds
        private int _scaleRequestId = 0;
        private string? _scaleRequestID = null;
        private bool _scaleWasPaused = false;
        private float _scaleResumeDelay = 0.0f;
        private bool _scalePausedForLadder = false;
        private bool _scalePausedForMotion = false;
        private float _targetScale = 1.0f; // Default scale
        private bool _scaleActive = false;
        private bool _isGiant = false; // Track if giant or tiny
        
        // Camera position tracking for tiny player effect
        private float? _originalCameraY = null;
        private object? _cameraTransform = null;
        private bool _cameraTransformErrorLogged = false;
        private bool _cameraHeightAdjustmentLogged = false;
        private const bool UseSceneManagerCamera = true;
        private object? _lastPrimaryCamera = null;
        private DateTime _lastPrimaryCameraTime = DateTime.MinValue;
        private DateTime _lastCameraFailLog = DateTime.MinValue;
        private float _aimZoomOffset = 0.0f;
        private float _aimForwardX = 0.0f;
        private float _aimForwardZ = 0.0f;
        private float _aimBlend = 0.0f;
        
        // Aiming detection cache (to avoid calling every frame and reduce crash risk)
        private DateTime _lastAimingCheck = DateTime.MinValue;
        private bool _cachedAimingResult = false;
        private bool _hasCachedAimingResult = false;
        private bool _lastLoggedAimingState = false;

        // Player speed tracking
        private float? _originalSpeed = null;
        private float _speedTimer = -1.0f; // Timer in seconds
        private int _speedRequestId = 0;
        private string? _speedRequestID = null;
        private bool _speedWasPaused = false;
        private float _targetSpeed = 1.0f; // Default speed multiplier
        private bool _speedActive = false;
        private bool _isFast = false; // Track if fast/hyper (true) or slow (false)
        private bool _wasReloading = false; // Track reload state to temporarily restore speed
        private bool _speedPausedForMotion = false;
        private string _lastMotionNodeSignature = "";
        private string _lastMotionSource = "";
        private bool _motionPauseActive = false;
        private static readonly HashSet<string> MotionPauseNodes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "STEP.STEP_UP",
                "STEP.STEP_DOWN",
                "RailWalk.Start",
                "RailWalk.End",
                "RailWalk.ButtonBackWalkIdle"
            };

        // Enemy size tracking
        private float _enemySizeTimer = -1.0f; // Timer in seconds
        private int _enemySizeRequestId = 0;
        private string? _enemySizeRequestID = null;
        private bool _enemySizeWasPaused = false;
        private float _enemySizeResumeDelay = 0.0f;
        private float _targetEnemySize = 1.0f; // Default scale
        private bool _enemySizeActive = false;
        private bool _isEnemyGiant = false; // Track if giant (true) or tiny (false)
        private bool _enemySizeRestorePending = false;
        private DateTime? _enemySizeRestoreReadyAt = null;

        // Enemy speed tracking
        private float _enemySpeedTimer = -1.0f; // Timer in seconds
        private int _enemySpeedRequestId = 0;
        private string? _enemySpeedRequestID = null;
        private bool _enemySpeedWasPaused = false;
        private float _targetEnemySpeed = 1.0f; // Default speed multiplier
        private bool _enemySpeedActive = false;
        private bool _isEnemyFast = false; // Track if fast (true) or slow (false)

        // Update interval
        private DateTime _lastUpdate = DateTime.MinValue;
        private const double UPDATE_INTERVAL_MS = 100; // Update every 100ms
        
        // Logging interval
        private DateTime _lastLogTime = DateTime.MinValue;
        private const double LOG_INTERVAL_MS = 5000; // Log every 5 seconds

        private const float SCALE_RESUME_DELAY_SECONDS = 2.0f;

        private const float ENEMY_SIZE_RESUME_DELAY_SECONDS = 2.0f;

        private static readonly TimeSpan EnemySizeRestoreDelay = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Current player health
        /// </summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>
        /// Maximum player health
        /// </summary>
        public float MaxHealth => _maxHealth;

        /// <summary>
        /// Is the player alive?
        /// </summary>
        public bool IsPlayerAlive => _isPlayerAlive;

        /// <summary>
        /// Is the game in a ready state (not in menu, cutscene, etc)?
        /// </summary>
        public bool IsGameReady => _isGameReady;

        /// <summary>
        /// Is the player currently invulnerable?
        /// </summary>
        public bool IsInvulnerable => _isInvulnerable;

        /// <summary>
        /// Is one-hit KO mode active?
        /// </summary>
        public bool IsOneHitKO => _isOneHitKO;

        /// <summary>
        /// Is FOV effect active?
        /// </summary>
        public bool IsFOVActive => _fovActive;

        /// <summary>
        /// Is player scale effect active?
        /// </summary>
        public bool IsScaleActive => _scaleActive;
        public bool IsGiant => _isGiant;

        public bool IsSpeedActive => _speedActive;

        /// <summary>
        /// Is enemy size effect active?
        /// </summary>
        public bool IsEnemySizeActive => _enemySizeActive;

        /// <summary>
        /// Is enemy speed effect active?
        /// </summary>
        public bool IsEnemySpeedActive => _enemySpeedActive;

        /// <summary>
        /// Update game state (call this periodically)
        /// </summary>
        public void Update()
        {
            var now = DateTime.Now;
            if ((now - _lastUpdate).TotalMilliseconds < UPDATE_INTERVAL_MS)
                return;

            _lastUpdate = now;

            try
            {
                UpdatePlayerManager();
                UpdateMainFlowManager();
                UpdatePlayerCondition();
                //UpdateMotionState();
                UpdateHealth();
                UpdateGameReady();
                //UpdatePendingEnemySizeRestore();
                //UpdatePlayerModelState();
                
                 //Log state every 5 seconds
                 if ((now - _lastLogTime).TotalMilliseconds >= LOG_INTERVAL_MS)
                 {
                     _lastLogTime = now;
                     LogGameState();
                 }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error updating - {ex.Message}");
            }
        }

        private void UpdatePlayerModelState()
        {
            bool hasModel = false;
            try
            {
                hasModel = GetPlayerHeadGameObject() != null || GetPlayerRootGameObject() != null;
            }
            catch
            {
                hasModel = false;
            }

            if (hasModel)
            {
                _hadPlayerModel = true;
                _playerModelMissingLogged = false;
                return;
            }

            if (!_hadPlayerModel || _playerModelMissingLogged)
                return;

            _playerModelMissingLogged = true;
            _hadPlayerModel = false;
            LogWarning("GameState: Player model not detected; stopping timed effects");
            StopTimedEffectsDueToMissingPlayerModel();
        }

        private void StopTimedEffectsDueToMissingPlayerModel()
        {
            try
            {
                StopOHKO();
                StopInvincibility();
                StopFOV();
                StopScale();
                StopSpeed();
                StopEnemySize();
                StopEnemySpeed();
                EnemySpawnManager.TryRequestDestroyAllTracked();
                EnemySpawnManager.ClearAll();
                EnemySpawner.ResetPrefabCache();
            }
            catch
            {
                // Best-effort cleanup when player model is missing.
            }
        }

        private void UpdatePendingEnemySizeRestore()
        {
            if (!_enemySizeRestorePending)
                return;

            if (!IsGameReady)
                return;

            if (_enemySizeRestoreReadyAt.HasValue && DateTime.UtcNow >= _enemySizeRestoreReadyAt.Value)
            {
                _enemySizeRestorePending = false;
                _enemySizeRestoreReadyAt = null;
                RestoreAllEnemyScales();
            }
        }
        
        /// <summary>
        /// Log current game state for debugging
        /// </summary>
        private void LogGameState()
        {
            try
            {
                // Player status
                string playerStatus = $"PlayerStatus: Alive={_isPlayerAlive}, Health={_currentHealth:F1}/{_maxHealth:F1}, PlayerManager={_playerManager != null}, Condition={_currentPlayerCondition != null}, HitPointController={_hitPointController != null}";
                LogInfo($"GameState: {playerStatus}");
                int? mainState = null;
                try
                {
                    var controller = API.GetManagedSingleton("app.MainGameFlowController");
                    if (controller != null)
                    {
                        var controllerObj = controller as ManagedObject;
                        if (controllerObj != null)
                        {
                            var phaseObj = controllerObj.GetField("_CurrentPhase")
                                        ?? controllerObj.GetField("phase");

                            if (phaseObj != null)
                            {
                                mainState = Convert.ToInt32(phaseObj);
                            }
                        }
                    }
                }
                catch (Exception) { }
                // Aim status
                bool? isHold = TryGetConditionHold();
                string aimStatus = $"AimStatus: IsAiming={isHold?.ToString() ?? "null"}";
                LogInfo($"GameState: {mainState}");
                
                // Menu status
                bool? isOpenInventory = null;
                bool? isOpenMap = null;
                bool? isOpenPause = null;
                bool? isOpenPauseForEvent = null;
                bool guiMasterExists = false;
                
                try
                {
                    var guiMaster = API.GetManagedSingleton("app.GuiManager");
                    if (guiMaster != null)
                    {
                        guiMasterExists = true;
                        var guiMasterObj = guiMaster as ManagedObject;
                        if (guiMasterObj != null)
                        {
                            var map = guiMasterObj.Call("get_IsWorldMapEnabled");
                            if (map != null) isOpenMap = Convert.ToBoolean(map);
                            
                            var pause = guiMasterObj.Call("get_IsHudPaused");
                            if (pause != null) isOpenPause = Convert.ToBoolean(pause);
                            
                            
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"GameState: Error checking GUI state - {ex.Message}");
                }
                
                string menuStatus = $"MenuStatus: GUIMaster={guiMasterExists}, Inventory={isOpenInventory?.ToString() ?? "null"}, Map={isOpenMap?.ToString() ?? "null"}, Pause={isOpenPause?.ToString() ?? "null"}, PauseForEvent={isOpenPauseForEvent?.ToString() ?? "null"}";
                LogInfo($"GameState: {menuStatus}");
                
                // Pause status (overall)
                string pauseStatus = $"PauseStatus: IsGameReady={_isGameReady}, OHKO={_isOneHitKO}, Invulnerable={_isInvulnerable}";
                LogInfo($"GameState: {pauseStatus}");
                
                // Effect timers
                if (_isOneHitKO || _isInvulnerable)
                {
                    string timerStatus = $"TimerStatus: OHKO={(_isOneHitKO ? $"{_ohkoTimer:F1}s" : "inactive")}, Invulnerable={(_isInvulnerable ? $"{_invulTimer:F1}s" : "inactive")}";
                    LogInfo($"GameState: {timerStatus}");
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error logging state - {ex.Message}");
            }
        }

        private void UpdatePlayerManager()//working for re9
        {
            try
            {
                // Use GetManagedSingleton with string to get ManagedObject directly (like LUA version)
                _playerManager = API.GetManagedSingleton("app.CharacterManager");
            }
            catch (Exception)
            {
                // Silently fail - this is expected if game is not ready
                _playerManager = null;
            }
        }

        private void UpdateMainFlowManager()//working for RE9
        {
            try
            {
                _mainFlowManager = API.GetManagedSingleton("app.MainGameFlowController");
            }
            catch (Exception)
            {
                // Silently fail - this is expected if game is not ready
                _mainFlowManager = null;
            }
        }

        ///*
        private bool _loggedCostumeSupport = false;
        private bool _loggedCostumeDictionary = false;

        private IEnumerable<(object? Key, object? Value)> EnumerateDictionaryLike(object? dictObj)
        {
            if (dictObj == null)
                yield break;

            var dictManaged = dictObj as ManagedObject ?? ExtractFromInvokeRet(dictObj) as ManagedObject;
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

                var currentObj = enumerator.Call("get_Current");
                var current = currentObj as ManagedObject ?? ExtractFromInvokeRet(currentObj) as ManagedObject;
                if (current == null)
                    continue;

                object? key = current.Call("get_Key") ?? current.GetField("Key");
                object? value = current.Call("get_Value") ?? current.GetField("Value");
                yield return (key, value);
            }
        }

        
        private IEnumerable<object?> EnumerateListLike(object? listObj)
        {
            if (listObj == null)
                yield break;

            if (listObj is System.Array array)
            {
                foreach (var item in array)
                    yield return item;
                yield break;
            }

            if (listObj is System.Collections.IEnumerable enumerable)
                {
                foreach (var item in enumerable)
                    yield return item;
                yield break;
            }

            var managed = listObj as ManagedObject ?? ExtractFromInvokeRet(listObj) as ManagedObject;
            if (managed == null)
                yield break;

            var typeDef = managed.GetTypeDefinition();
            if (typeDef?.FindMethod("GetEnumerator") == null)
                yield break;

            ManagedObject? enumerator = null;
            try
            {
                enumerator = managed.Call("GetEnumerator") as ManagedObject
                             ?? ExtractFromInvokeRet(managed.Call("GetEnumerator")) as ManagedObject;
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

                yield return enumerator.Call("get_Current");
            }
        }


        private static List<string> TryGetTypeDefinitionMethodNames(REFrameworkNET.TypeDefinition typeDef)
        {
            var names = new List<string>();
            try
            {
                var type = typeDef.GetType();
                object? methodsObj = type.GetProperty("Methods")?.GetValue(typeDef)
                                     ?? type.GetMethod("GetMethods", Type.EmptyTypes)?.Invoke(typeDef, null);

                if (methodsObj is System.Collections.IEnumerable enumerable)
                {
                    foreach (var method in enumerable)
                    {
                        if (method == null)
                            continue;

                        var nameProp = method.GetType().GetProperty("Name");
                        if (nameProp?.GetValue(method) is string name && !string.IsNullOrWhiteSpace(name))
                        {
                            names.Add(name);
                        }
                    }
                }
            }
            catch
            {
                // Ignore reflection failures.
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private ManagedObject? GetPlayerRootGameObject()
        {
            try
            {
                var playerManager = API.GetManagedSingleton("app.CharacterManager") as ManagedObject;
                var playerObj = playerManager?.Call("get_PlayerContextFast") as ManagedObject;
                if (playerObj == null)
                    return null;

                var playerType = playerObj.GetTypeDefinition();
                if (playerType?.FindMethod("get_GameObject") != null)
                    return playerObj.Call("get_GameObject") as ManagedObject;
            }
            catch
            {
                return null;
            }

            return null;
        }
        

        private UnifiedObject? GetGameObjectTransform(UnifiedObject gameObject)
        {
            var gameObjectType = gameObject.GetTypeDefinition();
            if (gameObjectType?.FindMethod("get_Transform") != null)
                return gameObject.Call("get_Transform") as UnifiedObject;

            return gameObject.GetField("transform") as UnifiedObject;
        }

        private IEnumerable<UnifiedObject> EnumerateChildGameObjects(UnifiedObject transform)
        {
            var result = new List<UnifiedObject>();
            var transformType = transform.GetTypeDefinition();
            if (transformType == null)
                return result;

            if (transformType.FindMethod("get_ChildCount") != null &&
                (transformType.FindMethod("get_Child") != null || transformType.FindMethod("GetChild") != null))
            {
                var countObj = transform.Call("get_ChildCount");
                if (countObj != null && int.TryParse(countObj.ToString(), out var count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        UnifiedObject? childTransform = null;
                        if (transformType.FindMethod("get_Child") != null)
                            childTransform = transform.Call("get_Child", i) as UnifiedObject;
                        if (childTransform == null && transformType.FindMethod("GetChild") != null)
                            childTransform = transform.Call("GetChild", i) as UnifiedObject;

                        var childGameObject = childTransform != null
                            ? GetTransformGameObject(childTransform)
                            : null;
                        if (childGameObject != null)
                            result.Add(childGameObject);
                    }
                }

                return result;
            }

            if (transformType.FindMethod("get_Children") != null)
            {
                var childrenObj = transform.Call("get_Children");
                foreach (var childTransform in EnumerateUnifiedList(childrenObj))
                {
                    var childGameObject = GetTransformGameObject(childTransform);
                    if (childGameObject != null)
                        result.Add(childGameObject);
                }
            }

            return result;
        }

        private IEnumerable<UnifiedObject> EnumerateChildGameObjectsRecursive(UnifiedObject transform, int maxDepth)
        {
            var result = new List<UnifiedObject>();
            if (maxDepth < 0)
                return result;

            foreach (var child in EnumerateChildGameObjects(transform))
            {
                result.Add(child);
                var childTransform = GetGameObjectTransform(child);
                if (childTransform != null && maxDepth > 0)
                    result.AddRange(EnumerateChildGameObjectsRecursive(childTransform, maxDepth - 1));
            }

            return result;
        }

        private IEnumerable<ManagedObject> EnumerateManagedList(object? listObj)
        {
            var result = new List<ManagedObject>();
            if (listObj == null)
                return result;

            if (listObj is UnifiedObject managedList)
            {
                var listType = managedList.GetTypeDefinition();
                LogListTypeMethodsIfNeeded(listType);
                var count = TryGetManagedListCount(managedList, listType);
                if (count.HasValue)
                {
                    for (int i = 0; i < count.Value; i++)
                    {
                        object? item = null;
                        if (listType?.FindMethod("get_Item") != null)
                            item = managedList.Call("get_Item", i);
                        if (item == null && listType?.FindMethod("Get") != null)
                            item = managedList.Call("Get", i);
                        if (item is ManagedObject managedItem)
                            result.Add(managedItem);
                    }
                }

                if (result.Count > 0)
                    return result;

                foreach (var item in EnumerateManagedEnumerator(managedList, listType))
                {
                    if (item is ManagedObject managedItem)
                        result.Add(managedItem);
                }

                if (result.Count > 0)
                    return result;

                foreach (var item in EnumerateManagedIterator(managedList, listType))
                {
                    if (item is ManagedObject managedItem)
                        result.Add(managedItem);
                }

                return result;
            }

            if (listObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is ManagedObject managedItem)
                        result.Add(managedItem);
                }
            }

            return result;
        }

        private IEnumerable<UnifiedObject> EnumerateUnifiedList(object? listObj)
        {
            var result = new List<UnifiedObject>();
            if (listObj == null)
                return result;

            if (listObj is UnifiedObject unifiedList)
            {
                var listType = unifiedList.GetTypeDefinition();
                LogListTypeMethodsIfNeeded(listType);
                var count = TryGetManagedListCount(unifiedList, listType);
                if (count.HasValue)
                {
                    for (int i = 0; i < count.Value; i++)
                    {
                        object? item = null;
                        if (listType?.FindMethod("get_Item") != null)
                            item = unifiedList.Call("get_Item", i);
                        if (item == null && listType?.FindMethod("Get") != null)
                            item = unifiedList.Call("Get", i);
                        if (item is UnifiedObject unifiedItem)
                            result.Add(unifiedItem);
                    }
                }

                if (result.Count > 0)
                    return result;

                foreach (var item in EnumerateManagedEnumerator(unifiedList, listType))
                    result.Add(item);

                if (result.Count > 0)
                    return result;

                foreach (var item in EnumerateManagedIterator(unifiedList, listType))
                    result.Add(item);

                return result;
            }

            if (listObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is UnifiedObject unifiedItem)
                        result.Add(unifiedItem);
                }
            }

            return result;
        }

        private IEnumerable<UnifiedObject> EnumerateManagedEnumerator(UnifiedObject managedList, REFrameworkNET.TypeDefinition? listType)
        {
            if (listType == null)
                yield break;

            var getEnumeratorMethod = FindMethodWithSuffixes(listType, "GetEnumerator");
            var getEnumeratorAltMethod = FindMethodWithSuffixes(listType, "get_Enumerator");
            if (getEnumeratorMethod == null && getEnumeratorAltMethod == null)
                yield break;

            UnifiedObject? enumerator = null;
            if (getEnumeratorMethod != null)
                enumerator = managedList.Call(getEnumeratorMethod.Name) as UnifiedObject;
            if (enumerator == null && getEnumeratorAltMethod != null)
                enumerator = managedList.Call(getEnumeratorAltMethod.Name) as UnifiedObject;
            if (enumerator == null)
                yield break;

            var enumType = enumerator.GetTypeDefinition();
            if (enumType == null)
                yield break;

            var moveNextMethod = FindMethodWithSuffixes(enumType, "MoveNext");
            var getCurrentMethod = FindMethodWithSuffixes(enumType, "get_Current", "get_Item");
            if (moveNextMethod == null || getCurrentMethod == null)
                yield break;

            int safety = 0;
            while (safety++ < 512)
            {
                var moved = enumerator.Call(moveNextMethod.Name);
                if (moved == null || !bool.TryParse(moved.ToString(), out var hasNext) || !hasNext)
                    yield break;

                var current = enumerator.Call(getCurrentMethod.Name);
                if (current is UnifiedObject managedCurrent)
                    yield return managedCurrent;
            }
        }

        private IEnumerable<UnifiedObject> EnumerateManagedIterator(UnifiedObject iterator, REFrameworkNET.TypeDefinition? iteratorType)
        {
            if (iteratorType == null)
                yield break;

            var moveNextMethod = FindMethodWithSuffixes(iteratorType, "MoveNext");
            var getCurrentMethod = FindMethodWithSuffixes(iteratorType, "get_Current", "get_Item");
            if (moveNextMethod == null || getCurrentMethod == null)
                yield break;

            int safety = 0;
            while (safety++ < 512)
            {
                var moved = iterator.Call(moveNextMethod.Name);
                if (moved == null || !bool.TryParse(moved.ToString(), out var hasNext) || !hasNext)
                    yield break;

                var current = iterator.Call(getCurrentMethod.Name);
                if (current is UnifiedObject managedCurrent)
                    yield return managedCurrent;
            }
        }

        private static REFrameworkNET.Method? FindMethodWithSuffixes(
            REFrameworkNET.TypeDefinition? type,
            params string[] names)
        {
            if (type == null)
                return null;

            foreach (var name in names)
            {
                var method = type.FindMethod(name);
                if (method != null)
                    return method;
            }

            foreach (var method in type.Methods)
            {
                var methodName = method.Name;
                foreach (var name in names)
                {
                    if (string.Equals(methodName, name, StringComparison.Ordinal))
                        return method;
                    if (methodName.EndsWith("." + name, StringComparison.Ordinal))
                        return method;
                }
            }

            return null;
        }

        private void LogListTypeMethodsIfNeeded(REFrameworkNET.TypeDefinition? listType)
        {
            if (listType == null)
                return;

            var typeName = listType.Name ?? listType.FullName ?? "List";
            if (_loggedListTypeSignatures.Contains(typeName))
                return;

            _loggedListTypeSignatures.Add(typeName);
            var methodNames = new List<string>();
            foreach (var method in listType.Methods)
            {
                var name = method.Name;
                if (name.IndexOf("Count", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Length", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Size", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Num", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Get", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Enumerator", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    methodNames.Add(name);
                    if (methodNames.Count >= 40)
                        break;
                }
            }

            LogInfo($"GameState: List type '{typeName}' methods: {string.Join(", ", methodNames)}");
        }

        private static int? TryGetManagedListCount(UnifiedObject managedList, REFrameworkNET.TypeDefinition? listType)
        {
            if (listType == null)
                return null;

            object? countObj = null;
            if (listType.FindMethod("get_Count") != null)
                countObj = managedList.Call("get_Count");
            if (countObj == null && listType.FindMethod("get_Length") != null)
                countObj = managedList.Call("get_Length");
            if (countObj == null && listType.FindMethod("get_size") != null)
                countObj = managedList.Call("get_size");
            if (countObj == null && listType.FindMethod("get_Size") != null)
                countObj = managedList.Call("get_Size");
            if (countObj == null && listType.FindMethod("get_Num") != null)
                countObj = managedList.Call("get_Num");

            return countObj != null && int.TryParse(countObj.ToString(), out var count)
                ? count
                : null;
        }

        private void TryUnregisterAccessoryEntry(ManagedObject costumeManager, ManagedObject entry)
        {
            try
            {
                var entryType = entry.GetTypeDefinition();
                if (entryType?.FindMethod("get_AccessoryType") != null)
                {
                    var accessoryValue = entry.Call("get_AccessoryType");
                    if (accessoryValue != null)
                        costumeManager.Call("unloadAccessory", accessoryValue);
                }
            }
            catch
            {
                // Ignore unregister failures.
            }
        }

        private void LogAccessoryListSnapshot(List<ManagedObject> entries)
        {
            try
            {
                var sampleNames = new List<string>();
                foreach (var entry in entries)
                {
                    var name = TryGetAccessoryEntryName(entry);
                    if (!string.IsNullOrEmpty(name))
                        sampleNames.Add(name);
                    if (sampleNames.Count >= 5)
                        break;
                }

                var signature = $"{entries.Count}|{string.Join(",", sampleNames)}";
                if (signature == _lastAccessoryListSignature)
                    return;

                _lastAccessoryListSignature = signature;
                var sampleText = sampleNames.Count > 0 ? string.Join(", ", sampleNames) : "n/a";
                LogInfo($"GameState: AccessoryList entries={entries.Count}, sample={sampleText}");
            }
            catch
            {
                // Ignore logging errors.
            }
        }

        private string? TryGetAccessoryEntryName(ManagedObject entry)
        {
            var entryType = entry.GetTypeDefinition();
            if (entryType?.FindMethod("get_AccessoryType") != null)
            {
                try
                {
                    var accessoryValue = entry.Call("get_AccessoryType");
                    if (accessoryValue != null)
                        return accessoryValue.ToString();
                }
                catch
                {
                    // Ignore accessory type failures.
                }
            }

            if (entryType?.FindMethod("get_GameObject") != null)
            {
                var gameObject = entry.Call("get_GameObject") as UnifiedObject;
                return gameObject != null ? GetGameObjectName(gameObject) : null;
            }

            var typeName = entryType?.Name ?? entryType?.FullName;
            return typeName;
        }

        private List<UnifiedObject> GetSceneRootGameObjects()
        {
            try
            {
                ManagedObject? sceneObj = null;
                var sceneManagerManaged = API.GetManagedSingleton("via.SceneManager") as ManagedObject;
                if (sceneManagerManaged != null)
                {
                    sceneObj = sceneManagerManaged.Call("get_CurrentScene") as ManagedObject;
                }
                else
                {
                    var sceneManagerNative = API.GetNativeSingleton("via.SceneManager");
                    if (sceneManagerNative != null)
                        sceneObj = sceneManagerNative.Call("get_CurrentScene") as ManagedObject;
                }

                if (sceneObj == null)
                    return new List<UnifiedObject>();

                var roots = TryResolveSceneRootGameObjects(sceneObj);
                if (roots.Count == 0)
                    LogSceneRootLookupFailure(sceneObj);

                return roots;
            }
            catch
            {
                return new List<UnifiedObject>();
            }
        }

        private List<UnifiedObject> TryResolveSceneRootGameObjects(ManagedObject sceneObj)
        {
            var sceneType = sceneObj.GetTypeDefinition();
            if (sceneType == null)
                return new List<UnifiedObject>();

            var roots = new List<UnifiedObject>();
            string[] methodNames =
            {
                "get_Root",
                "get_RootFolder",
                "get_RootObject",
                "get_RootGameObject",
                "get_GameObject"
            };

            foreach (var methodName in methodNames)
            {
                if (sceneType.FindMethod(methodName) == null)
                    continue;

                var value = sceneObj.Call(methodName) as UnifiedObject;
                var resolved = TryResolveGameObjectFromUnknown(value);
                if (resolved != null)
                    roots.Add(resolved);
            }

            string[] fieldNames = { "Root", "RootFolder", "RootObject", "RootGameObject", "GameObject" };
            foreach (var fieldName in fieldNames)
            {
                if (sceneType.FindField(fieldName) == null)
                    continue;

                var value = sceneObj.GetField(fieldName) as UnifiedObject;
                var resolved = TryResolveGameObjectFromUnknown(value);
                if (resolved != null)
                    roots.Add(resolved);
            }

            if (sceneType.FindMethod("findFolder") != null)
            {
                string[] folderNames = { "root", "Root", "GUI_Rogue", "ModdedTemporaryObjects" };
                foreach (var folderName in folderNames)
                {
                    var folderObj = sceneObj.Call("findFolder", folderName);
                    var folder = folderObj as UnifiedObject;
                    if (folder == null)
                        continue;

                    var resolved = TryResolveGameObjectFromUnknown(folder);
                    if (resolved != null)
                        roots.Add(resolved);

                    roots.AddRange(GatherGameObjectsFromFolder(folder));
                }
            }

            return roots
                .Distinct()
                .ToList();
        }

        private List<UnifiedObject> GatherGameObjectsFromFolder(UnifiedObject folder)
        {
            var results = new List<UnifiedObject>();
            foreach (var entry in EnumerateFolderEntries(folder, maxDepth: 6))
            {
                var resolved = TryResolveGameObjectFromUnknown(entry);
                if (resolved != null)
                    results.Add(resolved);
            }

            return results;
        }

        private IEnumerable<UnifiedObject> EnumerateFolderEntries(UnifiedObject folder, int maxDepth)
        {
            if (maxDepth < 0)
                yield break;

            foreach (var child in EnumerateFolderChildren(folder))
            {
                yield return child;
                foreach (var nested in EnumerateFolderEntries(child, maxDepth - 1))
                    yield return nested;
            }
        }

        private IEnumerable<UnifiedObject> EnumerateFolderChildren(UnifiedObject folder)
        {
            var result = new List<UnifiedObject>();
            var folderType = folder.GetTypeDefinition();
            if (folderType == null)
                return result;

            LogFolderTypeMethodsIfNeeded(folderType);

            var countMethodNames = new[]
            {
                "get_ChildCount",
                "get_FolderCount",
                "get_ChildFolderCount",
                "get_Count",
                "get_Length",
                "get_Size",
                "get_Num"
            };

            var childMethodNames = new[]
            {
                "get_Child",
                "GetChild",
                "get_Folder",
                "GetFolder",
                "get_ChildFolder",
                "GetChildFolder",
                "get_Item",
                "Get"
            };

            int? count = null;
            foreach (var countMethodName in countMethodNames)
            {
                if (folderType.FindMethod(countMethodName) == null)
                    continue;

                var countObj = folder.Call(countMethodName);
                if (countObj != null && int.TryParse(countObj.ToString(), out var parsedCount))
                {
                    count = parsedCount;
                    break;
                }
            }

            if (count.HasValue && count.Value > 0)
            {
                var foundChildMethod = childMethodNames
                    .FirstOrDefault(methodName => folderType.FindMethod(methodName) != null);

                if (!string.IsNullOrEmpty(foundChildMethod))
                {
                    for (int i = 0; i < count.Value; i++)
                    {
                        var child = folder.Call(foundChildMethod, i) as UnifiedObject;
                        if (child != null)
                            result.Add(child);
                    }

                    return result;
                }
            }

            if (folderType.FindMethod("get_Children") != null)
            {
                var childrenObj = folder.Call("get_Children");
                foreach (var child in EnumerateUnifiedList(childrenObj))
                    result.Add(child);
            }

            if (folderType.FindMethod("get_Folders") != null)
            {
                var childrenObj = folder.Call("get_Folders");
                foreach (var child in EnumerateUnifiedList(childrenObj))
                    result.Add(child);
            }

            if (folderType.FindMethod("getAllSubFolders") != null)
            {
                var childrenObj = folder.Call("getAllSubFolders");
                foreach (var child in EnumerateUnifiedList(childrenObj))
                    result.Add(child);
            }

            if (folderType.FindMethod("get_SceneFolder") != null)
            {
                var sceneFolderObj = folder.Call("get_SceneFolder");
                if (sceneFolderObj is UnifiedObject sceneFolder)
                    result.Add(sceneFolder);
            }

            return result;
        }

        private void LogFolderTypeMethodsIfNeeded(TypeDefinition? folderType)
        {
            if (folderType == null)
                return;

            var typeName = folderType.Name ?? folderType.FullName ?? "Folder";
            if (_loggedFolderTypeSignatures.Contains(typeName))
                return;

            _loggedFolderTypeSignatures.Add(typeName);
            var methodNames = new List<string>();
            foreach (var method in folderType.Methods)
            {
                var name = method.Name;
                if (name.IndexOf("Child", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("GameObject", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Transform", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Object", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    methodNames.Add(name);
                    if (methodNames.Count >= 40)
                        break;
                }
            }

            LogInfo($"GameState: Folder type '{typeName}' methods: {string.Join(", ", methodNames)}");
        }

        private void LogSceneRootLookupFailure(ManagedObject sceneObj)
        {
            try
            {
                var sceneType = sceneObj.GetTypeDefinition();
                var typeName = sceneType?.Name ?? sceneType?.FullName ?? "unknown";
                var hasFindFolder = sceneType?.FindMethod("findFolder") != null;
                var signature = $"{typeName}|findFolder={hasFindFolder}";
                if (_lastSceneRootLookupSignature == signature)
                    return;

                _lastSceneRootLookupSignature = signature;
                LogInfo($"GameState: Scene root lookup failed (type={typeName}, findFolder={hasFindFolder})");

                if (hasFindFolder)
                {
                    string[] folderNames = { "root", "Root", "GUI_Rogue", "ModdedTemporaryObjects" };
                    foreach (var folderName in folderNames)
                    {
                        var folderObj = sceneObj.Call("findFolder", folderName);
                        LogInfo($"GameState: Scene findFolder '{folderName}' => {DescribeSceneObject(folderObj)}");
                        if (folderObj is UnifiedObject managedFolder)
                        {
                            LogFolderTypeMethodsIfNeeded(managedFolder.GetTypeDefinition());
                            var resolved = TryResolveGameObjectFromUnknown(managedFolder);
                            LogInfo($"GameState: Scene findFolder '{folderName}' resolved GameObject={resolved != null}");
                        }
                    }
                }
            }
            catch
            {
                // Ignore logging errors.
            }
        }

        private static string DescribeSceneObject(object? obj)
        {
            if (obj == null)
                return "null";

            if (obj is ManagedObject managed)
            {
                var typeName = managed.GetTypeDefinition()?.Name ?? managed.GetTypeDefinition()?.FullName ?? "ManagedObject";
                return $"ManagedObject:{typeName}";
            }

            if (obj is NativeObject native)
            {
                var typeName = native.GetTypeDefinition()?.Name ?? native.GetTypeDefinition()?.FullName ?? "NativeObject";
                return $"NativeObject:{typeName}";
            }

            return obj.GetType().Name;
        }

        private UnifiedObject? TryResolveGameObjectFromUnknown(UnifiedObject? obj)
        {
            return TryResolveGameObjectFromUnknown(obj, depth: 0);
        }

        private UnifiedObject? TryResolveGameObjectFromUnknown(UnifiedObject? obj, int depth)
        {
            if (obj == null || depth > 4)
                return null;

            var objType = obj.GetTypeDefinition();
            if (objType == null)
                return null;

            if (objType.FindMethod("get_GameObject") != null)
                return obj.Call("get_GameObject") as UnifiedObject;

            if (objType.FindMethod("get_Transform") != null)
            {
                var transform = obj.Call("get_Transform") as UnifiedObject;
                return transform != null ? GetTransformGameObject(transform) : null;
            }

            string[] methodNames =
            {
                "get_Root",
                "get_RootFolder",
                "get_RootObject",
                "get_RootGameObject",
                "get_Object",
                "get_Target",
                "get_Parent",
                "get_ParentFolder",
                "get_Folder",
                "get_Owner",
                "get_OwnerGameObject"
            };

            foreach (var methodName in methodNames)
            {
                if (objType.FindMethod(methodName) == null)
                    continue;

                var value = obj.Call(methodName) as UnifiedObject;
                var resolved = TryResolveGameObjectFromUnknown(value, depth + 1);
                if (resolved != null)
                    return resolved;
            }

            string[] fieldNames =
            {
                "Root",
                "RootFolder",
                "RootObject",
                "RootGameObject",
                "GameObject",
                "gameObject",
                "_gameObject",
                "m_GameObject",
                "Object",
                "Parent",
                "ParentFolder",
                "Owner",
                "OwnerGameObject",
                "ownerGameObject"
            };

            foreach (var fieldName in fieldNames)
            {
                if (objType.FindField(fieldName) == null)
                    continue;

                var value = obj.GetField(fieldName) as UnifiedObject;
                var resolved = TryResolveGameObjectFromUnknown(value, depth + 1);
                if (resolved != null)
                    return resolved;
            }

            var typeName = objType.Name ?? objType.FullName;
            if (!string.IsNullOrEmpty(typeName) &&
                typeName.IndexOf("GameObject", StringComparison.OrdinalIgnoreCase) >= 0)
                return obj;

            return null;
        }


        private UnifiedObject? GetTransformGameObject(UnifiedObject transform)
        {
            var transformType = transform.GetTypeDefinition();
            if (transformType?.FindMethod("get_GameObject") != null)
                return transform.Call("get_GameObject") as UnifiedObject;

            if (transformType?.FindField("ownerGameObject") != null)
            {
                return transform.GetField("ownerGameObject") as UnifiedObject;
            }

            return null;
        }

        private static string? GetGameObjectName(UnifiedObject gameObject)
        {
            var gameObjectType = gameObject.GetTypeDefinition();
            if (gameObjectType?.FindMethod("get_Name") != null)
            {
                var nameObj = gameObject.Call("get_Name");
                return nameObj?.ToString();
            }

            var validName = TryCallGameObjectExtensionString("getValidName", gameObject);
            if (!string.IsNullOrEmpty(validName))
                return validName;

            return null;
        }

        private static string? TryGetGameObjectFullPath(UnifiedObject gameObject)
        {
            return TryCallGameObjectExtensionString("getFullPath", gameObject);
        }

        private static string? TryCallGameObjectExtensionString(string methodName, UnifiedObject gameObject)
        {
            try
            {
                var tdb = TDB.Get();
                var extensionType = tdb?.FindType("offline.GameObjectExtension");
                if (extensionType == null)
                    return null;

                var method = extensionType.FindMethod(methodName);
                if (method == null)
                    return null;

                var result = method.Invoke(null, new object[] { gameObject });
                return result.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static void DestroyGameObject(UnifiedObject gameObject)
        {
            try
            {
                var tdb = API.GetTDB();
                var gameObjectType = tdb?.FindType("via.GameObject");
                var destroyMethod = gameObjectType?.FindMethod("destroy(via.GameObject)");
                if (destroyMethod != null)
                {
                    destroyMethod.Invoke(null, new object[] { gameObject });
                    return;
                }

                var managedType = gameObject.GetTypeDefinition();
                if (managedType?.FindMethod("destroy") != null)
                    gameObject.Call("destroy");
            }
            catch
            {
                // Ignore destroy errors.
            }
        }

        public bool TryApplyPostEffectFilter(string filterKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filterKey))
                    return false;

                var customLayer = GetPostEffectCustomLayer();
                if (customLayer == null)
                {
                    LogWarning("GameState: PostEffect custom layer not available");
                    return false;
                }

                var filterParams = GetPostEffectFilterParams(customLayer);
                var availableNames = new List<string>();
                ManagedObject? match = null;
                string? matchName = null;

                foreach (var param in filterParams)
                {
                    var name = GetPostEffectParamName(param);
                    if (!string.IsNullOrEmpty(name))
                        availableNames.Add(name);

                    if (match == null && name != null &&
                        name.IndexOf(filterKey, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        match = param;
                        matchName = name;
                    }
                }

                var signature = string.Join("|", availableNames);
                if (!string.Equals(_lastPostEffectListSignature, signature, StringComparison.Ordinal))
                {
                    _lastPostEffectListSignature = signature;
                    LogInfo($"GameState: Available filters ({availableNames.Count}): {string.Join(", ", availableNames)}");
                }

                if (match == null)
                {
                    LogWarning($"GameState: Filter '{filterKey}' not found");
                    return false;
                }

                if (_currentPostEffectParam != null && ReferenceEquals(_currentPostEffectParam, match))
                    return true;

                var customLayerType = customLayer.GetTypeDefinition();
                if (customLayerType?.FindMethod("applyPostEffectParam") == null)
                {
                    LogWarning("GameState: CustomLayer applyPostEffectParam not available");
                    return false;
                }

                customLayer.Call("applyPostEffectParam", match);
                if (customLayerType.FindMethod("set_CurrentParam") != null)
                    customLayer.Call("set_CurrentParam", match);

                _currentPostEffectParam = match;
                _currentPostEffectName = matchName ?? filterKey;
                LogInfo($"GameState: Applied filter {_currentPostEffectName}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error applying filter - {ex.Message}");
                return false;
            }
        }

        private static string? NormalizeAccessoryId(string accessoryId)
        {
            var trimmed = accessoryId.Trim();
            if (trimmed.Length == 0)
                return null;

            if (trimmed.StartsWith("Ac", StringComparison.OrdinalIgnoreCase))
                return "Ac" + trimmed.Substring(2);

            if (trimmed.StartsWith("ac", StringComparison.OrdinalIgnoreCase))
                return "Ac" + trimmed.Substring(2);

            if (trimmed.All(char.IsDigit))
                return "Ac" + trimmed.PadLeft(4, '0');

            return trimmed;
        }

        private ManagedObject? GetPlayerHeadGameObject()
        {
            try
            {
                if (_currentPlayerCondition is not ManagedObject conditionObj)
                    return null;

                var headJoint = conditionObj.GetField("<HeadJoint>k__BackingField") as ManagedObject;
                var headTransform = headJoint?.Call("get_Owner") as ManagedObject;
                ManagedObject? gameObject = null;

                if (headTransform != null)
                {
                    var transformType = headTransform.GetTypeDefinition();
                    if (transformType?.FindMethod("get_GameObject") != null)
                    {
                        gameObject = headTransform.Call("get_GameObject") as ManagedObject;
                    }
                    else if (transformType?.FindField("ownerGameObject") != null)
                    {
                        gameObject = headTransform.GetField("ownerGameObject") as ManagedObject;
                    }
                }

                if (gameObject != null)
                    return gameObject;

                var playerManager = API.GetManagedSingleton("offline.PlayerManager") as ManagedObject;
                var playerObj = playerManager?.Call("get_CurrentPlayer") as ManagedObject;
                if (playerObj == null)
                    return null;

                var playerType = playerObj.GetTypeDefinition();
                if (playerType?.FindMethod("get_GameObject") != null)
                {
                    return playerObj.Call("get_GameObject") as ManagedObject;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string? MapPlayerIdToCharacterName(string playerId)
        {
            if (!playerId.StartsWith("pl", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!int.TryParse(playerId.Substring(2), out var numericId))
                return null;

            if (numericId < 1000)
                return "Jill";
            if (numericId < 2000)
                return "Carlos";
            if (numericId < 3000)
                return "Jill";
            if (numericId < 4000)
                return "Sherry";
            if (numericId < 4100)
                return "Hunk";
            if (numericId < 4200)
                return "Tofu";
            if (numericId < 5100)
                return "Kendo";
            if (numericId < 5200)
                return "Irons";
            if (numericId < 5300)
                return "Ben";
            if (numericId < 5400)
                return "Annette";
            if (numericId < 5500)
                return "Chris";
            if (numericId < 5600)
                return "Ethan";
            if (numericId < 5700)
                return "USS";
            if (numericId < 5800)
                return "Marvin";
            if (numericId < 6100)
                return "William";
            if (numericId < 6500)
                return "Katherine";

            return null;
        }

        /// <summary>
        /// Check if a weapon is available for the current character
        /// </summary>
        public bool IsWeaponAvailableForCharacter(string weaponKey)
        {
            // RE3 LUA does not restrict weapons by character.
                return true;
        }

        /// <summary>
        /// Check if ammo is available for the current character
        /// </summary>
        public bool IsAmmoAvailableForCharacter(string ammoKey)
        {
            // RE3 LUA does not restrict ammo by character.
                return true;
        }

        private void UpdatePlayerCondition()//updated for RE9
        {
            _currentPlayerCondition = null;

            if (_playerManager == null)
            {
                UpdatePlayerManager();
                if (_playerManager == null)
                    return;
            }

            try
            {
                var playerManagerObj = _playerManager as ManagedObject;
                if (playerManagerObj == null)
                    return;

                var playerContext = playerManagerObj.Call("getPlayerContextRef") as ManagedObject;

                if (playerContext == null)
                    return;

                var hitPoint = playerContext.Call("get_HitPoint") as ManagedObject;
                if (hitPoint == null)
                    return;

                _currentPlayerCondition = hitPoint;
                if(hitPoint != null)
                {
                    try
                    {
                        var hitCont = playerContext.GetField("<HitPoint>k__BackingField");
                        if (hitCont != null) _hitPointController = hitCont;
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception)
            {
                // expected during load
            }
        }

        private void UpdateHealth() // updated for RE9
        {
            _currentHealth = 0;
            _maxHealth = 0;
            _isPlayerAlive = false;

            if (_currentPlayerCondition == null)
            {
                UpdatePlayerCondition();
                if (_currentPlayerCondition == null)
                    return;
            }

            try
            {
                var hpObj = _currentPlayerCondition as ManagedObject;
                if (hpObj == null)
                    return;

                object currentHPObj = hpObj.Call("get_CurrentHitPoint");
                object maxHPObj = hpObj.Call("get_CurrentMaximumHitPoint");

                if (currentHPObj == null || maxHPObj == null)
                    return;

                float currentHP = Convert.ToSingle(currentHPObj);
                float maxHP = Convert.ToSingle(maxHPObj);

                _currentHealth = currentHP;
                _maxHealth = maxHP;

                object isDeadObj = hpObj.Call("get_IsDead");

                if (isDeadObj != null)
                {
                    _isPlayerAlive = !Convert.ToBoolean(isDeadObj);
                }
                else
                {
                    // fallback
                    _isPlayerAlive = currentHP > 0;
                }
            }
            catch (Exception)
            {
                // Expected during loading / transitions
            }
        }

        private void UpdateGameReady()
        {
            _isGameReady = false;

            // Ensure MainFlowManager exists
            if (_mainFlowManager == null)
            {
                UpdateMainFlowManager();
                if (_mainFlowManager == null)
                {
                    _isGameReady = _isPlayerAlive;
                    return;
                }
            }

            try
            {
                var controller = API.GetManagedSingleton("app.MainGameFlowController") as ManagedObject;
                if (controller != null)
                {
                    var phaseObj = controller.GetField("_CurrentPhase")
                                ?? controller.GetField("phase");

                    if (phaseObj == null || Convert.ToInt32(phaseObj) != 6) // BattleReady
                    {
                        _isGameReady = false;
                        return;
                    }
                }
            }
            catch (Exception)
            {
                _isGameReady = _isPlayerAlive;
                return;
            }

            if (!_isPlayerAlive)
            {
                _isGameReady = false;
                return;
            }

            try
            {
                var guiMaster = API.GetManagedSingleton("app.GuiManager");
                if (guiMaster != null)
                {
                    var guiObj = guiMaster as ManagedObject;
                    if (guiObj != null)
                    {
                        var isOpenMap = guiObj.Call("get_IsOpenWorldMap");
                        if (isOpenMap != null && Convert.ToBoolean(isOpenMap))
                        {
                            _isGameReady = false;
                            return;
                        }

                        var isHudPaused = guiObj.Call("get_IsHudPaused");
                        if (isHudPaused != null && Convert.ToBoolean(isHudPaused))
                        {
                            _isGameReady = false;
                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
                
            }

            _isGameReady = true;
        }

        /// <summary>
        /// Check if a specific effect can be executed based on game state
        /// </summary>
        public bool CanExecuteEffect(string effectCode)
        {
            Update(); // Ensure state is fresh

            return effectCode switch
            {
                "heal" => CanHeal(),
                "hurt" or "damage" => CanHurt(),
                "kill" => CanKill(),
                _ => IsGameReady && IsPlayerAlive
            };
        }

        /// <summary>
        /// Check if heal effect can be executed
        /// </summary>
        public bool CanHeal()
        {
            // Update state first
            Update();
            
            // Be lenient - if we have a HitPointController, we can probably heal
            if (_hitPointController == null)
            {
                LogInfo($"CanHeal: HitPointController is null");
                return false;
            }
            
            // If health detection isn't working, but we have the controller, allow it
            if (_currentHealth == 0 && _maxHealth == 0)
            {
                LogInfo($"CanHeal: Health detection not working, but allowing heal attempt");
                return true;
            }
            
            if (IsOneHitKO || IsInvulnerable)
            {
                LogInfo($"CanHeal: OneHitKO={IsOneHitKO}, Invulnerable={IsInvulnerable}");
                return false;
            }

            // Can heal if health is below max
            var canHeal = _currentHealth < _maxHealth;
            LogInfo($"CanHeal: Current={_currentHealth}, Max={_maxHealth}, CanHeal={canHeal}");
            return canHeal;
        }

        /// <summary>
        /// Check if hurt/damage effect can be executed
        /// </summary>
        public bool CanHurt()
        {
            // Update state first
            Update();
            
            // Be lenient - if we have a HitPointController, we can probably damage
            if (_hitPointController == null)
            {
                LogInfo($"CanHurt: HitPointController is null");
                return false;
            }
            
            // If health detection isn't working, but we have the controller, allow it
            if (_currentHealth == 0 && _maxHealth == 0)
            {
                LogInfo($"CanHurt: Health detection not working, but allowing damage attempt");
                return true;
            }
            
            if (IsOneHitKO || IsInvulnerable)
            {
                LogInfo($"CanHurt: OneHitKO={IsOneHitKO}, Invulnerable={IsInvulnerable}");
                return false;
            }

            // Can hurt if health is above minimum threshold
            var damageAmount = _maxHealth / 4.0f;
            var canHurt = _currentHealth > (damageAmount / 2.0f);
            LogInfo($"CanHurt: Current={_currentHealth}, Max={_maxHealth}, DamageAmount={damageAmount}, CanHurt={canHurt}");
            return canHurt;
        }

        /// <summary>
        /// Check if kill effect can be executed
        /// </summary>
        public bool CanKill()
        {
            if (!IsGameReady)
            {
                return false;
            }
            
            if (!IsPlayerAlive)
            {
                return false;
            }

            // Block kill during invincibility
            if (IsInvulnerable)
            {
                LogInfo("CanKill: Invincibility is active, cannot kill");
                return false;
            }

            // Allow kill during OHKO (kill is allowed during OHKO)
            // Can kill if player is alive
            return true;
        }

        /// <summary>
        /// Set player health directly
        /// </summary>
        /// 
        public bool SetHealth(float health, int type = 1)
        {
            Update();

            if (_currentPlayerCondition == null)
            {
                LogError("GameState: No PlayerCondition");
                return false;
            }
            
            try
            {
                var playerManagerObj = _playerManager as ManagedObject;
                var playerContext = playerManagerObj.Call("getPlayerContextRef") as ManagedObject;
                if (playerContext == null)
                    return false;
                var hpObj = playerContext.Call("get_HitPoint") as ManagedObject;
                var currentObj = hpObj.Call("get_CurrentHitPoint")
                    ?? hpObj.GetField("<HitPointData>k__BackingField")
                    ?? hpObj.GetField("CurrentHitPoint");
                if (currentObj == null)
                {
                    Logger.LogWarning("RE9DotNet-CC: TrySetHitPoint failed - current HP missing.");
                    return false;
                }
                var current = Convert.ToInt32(currentObj);
                var target = Math.Max(0, Convert.ToInt32(health));
                var delta = target - current;
                if (delta == 0)
                    return false;

                var typeDef = hpObj.GetTypeDefinition();
                if (type == 2)
                {
                    if (typeDef?.FindMethod("set_CurrentHitPoint") == null)
                    {
                        Logger.LogWarning("RE9DotNet-CC: HitPoint.addDamage not found.");
                        return false;
                    }

                    ((ManagedObject)hpObj).Call("set_CurrentHitPoint", current - target);
                }
                else
                {
                    if (typeDef?.FindMethod("set_CurrentHitPoint") == null)
                    {
                        Logger.LogWarning("RE9DotNet-CC: HitPoint.recovery not found.");
                        return false;
                    }
                    ((ManagedObject)hpObj).Call("set_CurrentHitPoint", target);
                }
                LogInfo($"Final HP: {_currentHealth}, Alive: {_isPlayerAlive}");

                return true;
            }
            catch (Exception ex)
            {
                LogError($"SetHealth failed: {ex.Message}");
                return false;
            }
        }


        public bool ForceKillPlayer()
        {
            Update();

            if (_hitPointController == null)
            {
                for (int i = 0; i < 3 && _hitPointController == null; i++)
                {
                    UpdatePlayerManager();
                    UpdatePlayerCondition();
                    if (_hitPointController != null)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(10);
                }

                if (_hitPointController == null)
                {
                    LogError("GameState: ForceKillPlayer failed - HitPointController not available");
                    return false;
                }
            }

            var hitPointObj = (ManagedObject)_hitPointController;
            var conditionObj = _currentPlayerCondition as ManagedObject;

            LogInfo("GameState: ForceKillPlayer starting");

            try
            {
                try
                {
                    var invincibleObj = hitPointObj.Call("get_Invincible");
                    var noDamageObj = hitPointObj.Call("get_NoDamage");

                    if (invincibleObj != null && Convert.ToBoolean(invincibleObj))
                    {
                        hitPointObj.Call("set_Invincible", false);
                    }

                    if (noDamageObj != null && Convert.ToBoolean(noDamageObj))
                    {
                        hitPointObj.Call("set_NoDamage", false);
                    }
                }
                catch
                {
                    // ignore flag failures
                }

                try
                {
                    if (conditionObj != null)
                    {
                        var conditionType = conditionObj.GetTypeDefinition();
                        if (conditionType?.FindMethod("set_IsPoison") != null)
                        {
                            conditionObj.Call("set_IsPoison", false);
                        }
                    }
                }
                catch
                {
                    // ignore poison flag failures
                }

                var gameOverTriggered = false;
                var deadNow = false;

                TryKillViaConditionDeadAction(conditionObj);

                if (TryKillViaDamageParamAndHit(conditionObj))
                {
                    deadNow = IsPlayerDeadNow(hitPointObj, conditionObj);
                    if (deadNow)
                        LogInfo("GameState: Kill sequence completed via DamageParam/onHitDamage");
                }

                if (!deadNow && TryKillViaAddDamageHitPoint(conditionObj))
                {
                    deadNow = IsPlayerDeadNow(hitPointObj, conditionObj);
                    if (deadNow)
                        LogInfo("GameState: Kill sequence completed via addDamageHitPoint");
                }

                if (!deadNow && TryKillViaPlayerManagerDamage())
                {
                    deadNow = IsPlayerDeadNow(hitPointObj, conditionObj);
                    if (deadNow)
                        LogInfo("GameState: Kill sequence completed via PlayerManager.damageCurrentPlayer");
                }

                if (!deadNow)
                {
                    var hpObj = hitPointObj.Call("get_CurrentHitPoint");
                    if (hpObj != null && Convert.ToInt32(hpObj) > 0)
                    {
                        var damageAmount = 999999;
                        LogInfo($"GameState: Using addDamage({damageAmount}) to kill player");
                        hitPointObj.Call("addDamage", damageAmount);
                    }

                    hitPointObj.Call("set_CurrentHitPoint", 0);
                    if (!TryForceDead(hitPointObj))
                    {
                        if (hitPointObj.GetTypeDefinition()?.FindMethod("dead") != null)
                            hitPointObj.Call("dead");
                    }

                    deadNow = IsPlayerDeadNow(hitPointObj, conditionObj);
                }

                if (!deadNow)
                {
                    // As a last resort, trigger the GameOver flow (scene transition).
                    gameOverTriggered = TryTriggerGameOverFlow();
                    deadNow = gameOverTriggered;
                }

                if (deadNow)
                {
                    try
                    {
                        EnemySpawnManager.ClearAll();
                        EnemySpawner.ResetPrefabCache();
                    }
                    catch
                    {
                        // ignore cleanup failures during kill
                    }

                    _currentHealth = 0;
                    _isPlayerAlive = false;
                }

                LogInfo($"GameState: ForceKillPlayer completed - dead={deadNow}, gameOver={gameOverTriggered}");
                return deadNow;
            }
            catch (Exception ex)
            {
                LogError($"GameState: ForceKillPlayer failed - {ex.Message}");
                return false;
            }
        }

        private bool TryKillViaConditionDeadAction(ManagedObject? conditionObj)
        {
            if (conditionObj == null)
            {
                return false;
            }

            try
            {
                var conditionType = conditionObj.GetTypeDefinition();
                if (conditionType == null)
                {
                    return false;
                }

                var didAnything = false;
                var methodNames = new[]
                {
                    "dead",
                    "requestDead",
                    "forceDead",
                    "onDead",
                    "onDeath"
                };

                foreach (var methodName in methodNames)
                {
                    if (conditionType.FindMethod(methodName) != null)
                    {
                        conditionObj.Call(methodName);
                        didAnything = true;
                    }
                }

                var setMethodNames = new[]
                {
                    "set_IsDead",
                    "set_Dead",
                    "set_isDead"
                };

                foreach (var methodName in setMethodNames)
                {
                    if (conditionType.FindMethod(methodName) != null)
                    {
                        conditionObj.Call(methodName, true);
                        didAnything = true;
                    }
                }

                if (didAnything)
                {
                    LogInfo("GameState: Called SurvivorCondition death actions");
                }

                return didAnything;
            }
            catch (Exception ex)
            {
                LogInfo($"GameState: SurvivorCondition death actions failed - {ex.Message}");
                return false;
            }
        }

        private bool TryKillViaAddDamageHitPoint(ManagedObject? conditionObj)
        {
            if (conditionObj == null)
            {
                return false;
            }

            try
            {
                var conditionType = conditionObj.GetTypeDefinition();
                if (conditionType?.FindMethod("addDamageHitPoint") == null)
                {
                    return false;
                }

                conditionObj.Call("addDamageHitPoint", 999999);
                return true;
            }
            catch (Exception ex)
            {
                LogInfo($"GameState: addDamageHitPoint kill failed - {ex.Message}");
                return false;
            }
        }

        private bool TryKillViaPlayerManagerDamage()
        {
            try
            {
                var playerManager = API.GetManagedSingleton("offline.PlayerManager");
                if (playerManager is not ManagedObject playerManagerObj)
                {
                    return false;
                }

                var managerType = playerManagerObj.GetTypeDefinition();
                if (managerType?.FindMethod("damageCurrentPlayer") == null)
                {
                    return false;
                }

                playerManagerObj.Call("damageCurrentPlayer", 999999);
                return true;
            }
            catch (Exception ex)
            {
                LogInfo($"GameState: damageCurrentPlayer kill failed - {ex.Message}");
                return false;
            }
        }

        private bool TryTriggerGameOverFlow()
        {
            try
            {
                if (_mainFlowManager == null)
                {
                    UpdateMainFlowManager();
                }

                var mainFlowObj = _mainFlowManager as ManagedObject
                    ?? API.GetManagedSingleton("offline.gamemastering.MainFlowManager") as ManagedObject;
                if (mainFlowObj == null)
                {
                    return false;
                }

                var mainFlowType = mainFlowObj.GetTypeDefinition();
                if (mainFlowType == null)
                {
                    return false;
                }

                // Ensure game-over/continue flags are enabled so continue/load works.
                if (mainFlowType.FindMethod("set_UseGameOver") != null)
                {
                    mainFlowObj.Call("set_UseGameOver", true);
                }

                if (mainFlowType.FindMethod("set_UseContinue") != null)
                {
                    mainFlowObj.Call("set_UseContinue", true);
                }

                var triggered = false;

                // Some mods trigger a scene transition directly when forcing game over.
                if (mainFlowType.FindMethod("requestSceneInSaveDataActor") != null)
                {
                    mainFlowObj.Call("requestSceneInSaveDataActor");
                    return true;
                }

                // Try direct GameOverFlow setup first.
                ManagedObject? gameOverFlowObj = null;
                if (mainFlowType.FindMethod("get_GameOver") != null)
                {
                    var gameOverRet = mainFlowObj.Call("get_GameOver");
                    gameOverFlowObj = gameOverRet as ManagedObject
                        ?? ExtractFromInvokeRet(gameOverRet) as ManagedObject;
                }
                else
                {
                    var gameOverField = mainFlowObj.GetField("GameOver");
                    gameOverFlowObj = gameOverField as ManagedObject
                        ?? ExtractFromInvokeRet(gameOverField) as ManagedObject;
                }

                if (gameOverFlowObj != null)
                {
                    var gameOverType = gameOverFlowObj.GetTypeDefinition();
                    if (gameOverType?.FindMethod("set_CacheIsContinue") != null)
                    {
                        gameOverFlowObj.Call("set_CacheIsContinue", true);
                    }

                    if (gameOverType?.FindMethod("set_WaitToExitFlow") != null)
                    {
                        gameOverFlowObj.Call("set_WaitToExitFlow", false);
                    }

                    if (gameOverType?.FindMethod("set_IsNeedEnd") != null)
                    {
                        gameOverFlowObj.Call("set_IsNeedEnd", false);
                    }

                    if (gameOverType?.FindMethod("setupSimple") != null)
                    {
                        gameOverFlowObj.Call(
                            "setupSimple",
                            0,
                            null
                        );
                        triggered = true;
                    }
                    else if (gameOverType?.FindMethod("setupWhite") != null)
                    {
                        gameOverFlowObj.Call(
                            "setupWhite",
                            0,
                            null
                        );
                        triggered = true;
                    }
                    else if (gameOverType?.FindMethod("setup") != null)
                    {
                        gameOverFlowObj.Call("setup", null);
                        triggered = true;
                    }

                    if (triggered && gameOverType?.FindMethod("setupExitFlow") != null)
                    {
                        gameOverFlowObj.Call("setupExitFlow", false);
                    }

                    if (triggered)
                    {
                        // Force the state machine to advance if setup didn't transition.
                        if (gameOverType?.FindMethod("set_StateValue") != null)
                        {
                            gameOverFlowObj.Call(
                                "set_StateValue",
                                0
                            );
                            LogInfo("GameState: GameOverFlow state forced to INITIALIZE_SIMPLE");
                        }
                        else if (gameOverType?.FindMethod("setStateValue") != null)
                        {
                            gameOverFlowObj.Call(
                                "setStateValue",
                                0
                            );
                            LogInfo("GameState: GameOverFlow state forced to INITIALIZE_SIMPLE");
                        }

                        if (gameOverType?.FindMethod("update") != null)
                        {
                            gameOverFlowObj.Call("update");
                        }
                    }
                }

                if (!triggered)
                {
                    if (mainFlowType.FindMethod("goGameOverSimple") != null)
                    {
                        mainFlowObj.Call("goGameOverSimple", null);
                        triggered = true;
                    }
                    else if (mainFlowType.FindMethod("goGameOverWhite") != null)
                    {
                        mainFlowObj.Call("goGameOverWhite", null);
                        triggered = true;
                    }
                    else if (mainFlowType.FindMethod("goGameOver") != null)
                    {
                        mainFlowObj.Call("goGameOver", null);
                        triggered = true;
                    }
                }

                if (triggered && mainFlowType.FindMethod("get_IsInGameOver") != null)
                {
                    var inGameOverObj = mainFlowObj.Call("get_IsInGameOver");
                    var inGameOver = inGameOverObj != null && Convert.ToBoolean(inGameOverObj);
                    LogInfo($"GameState: GameOver triggered - IsInGameOver={inGameOver}");
                }

                return triggered;
            }
            catch (Exception ex)
            {
                LogInfo($"GameState: GameOver trigger failed - {ex.Message}");
                return false;
            }
        }

        private static bool IsHitPointDead(ManagedObject hitPointObj)
        {
            var isDeadObj = hitPointObj.Call("get_IsDead");
            return isDeadObj != null && Convert.ToBoolean(isDeadObj);
        }

        private static bool TryForceDead(ManagedObject hitPointObj)
        {
            try
            {
                var type = hitPointObj.GetTypeDefinition();

                if (type?.FindMethod("set_CurrentHitPoint") != null)
                {
                    hitPointObj.Call("set_CurrentHitPoint", 0);
                }

                if (type?.FindMethod("dead") != null)
                {
                    hitPointObj.Call("dead");
                }

                if (type?.FindMethod("set_IsDead") != null)
                {
                    hitPointObj.Call("set_IsDead", true);
                }

                return IsHitPointDead(hitPointObj);
            }
            catch
            {
                return false;
            }
        }

        private bool TryKillViaDamageParamAndHit(ManagedObject? hitPointObj)
        {
            if (hitPointObj == null)
                return false;

            try
            {
                var type = hitPointObj.GetTypeDefinition();

                try
                {
                    hitPointObj.Call("set_Invincible", false);

                    var noDamage = hitPointObj.Call("get_NoDamage");
                    if (noDamage is ManagedObject nd)
                    {
                        try { nd.Call("clear"); } catch { }
                        try { nd.Call("reset"); } catch { }
                    }
                }
                catch { }

                if (type?.FindMethod("addDamage") != null)
                {
                    hitPointObj.Call("addDamage", 999999);
                }

                if (type?.FindMethod("set_CurrentHitPoint") != null)
                {
                    hitPointObj.Call("set_CurrentHitPoint", 0);
                }

                if (type?.FindMethod("dead") != null)
                {
                    hitPointObj.Call("dead");
                }

                return IsHitPointDead(hitPointObj);
            }
            catch (Exception ex)
            {
                LogInfo($"GameState: Kill fallback failed - {ex.Message}");
                return false;
            }
        }

        private bool IsPlayerDeadNow(ManagedObject hitPointObj, ManagedObject? conditionObj)
        {
            var hitPointDead = false;

            try
            {
                var isDeadObj = hitPointObj.Call("get_IsDead");
                hitPointDead = isDeadObj != null && Convert.ToBoolean(isDeadObj);
            }
            catch
            {
                hitPointDead = false;
            }

            var conditionDead = false;
            if (conditionObj != null)
            {
                try
                {
                    var obj = conditionObj.Call("get_IsDead");
                    conditionDead = obj != null && Convert.ToBoolean(obj);
                }
                catch { }
            }

            LogInfo($"GameState: Kill verify - HitPointDead={hitPointDead}, ConditionDead={conditionDead}");

            return hitPointDead || conditionDead;
        }

        private bool TryDamageCurrentPlayer(int damageAmount)
        {
            try
            {
                var charMgr = API.GetManagedSingleton("app.CharacterManager") as ManagedObject;
                if (charMgr == null)
                    return false;

                var ctx = charMgr.Call("getPlayerContextRef")
                       ?? charMgr.Call("get_PlayerContext");

                if (ctx is not ManagedObject ctxObj)
                    return false;

                var hpObj = ctxObj.Call("get_HitPoint") as ManagedObject;
                if (hpObj == null)
                    return false;

                LogInfo($"GameState: Applying damage via HitPoint ({damageAmount})");

                hpObj.Call("addDamage", damageAmount);
                return true;
            }
            catch (Exception ex)
            {
                LogInfo($"GameState: damageCurrentPlayer failed - {ex.Message}");
                return false;
            }
        }
        public bool TryApplyDopingStatus(float durationSeconds = 15f)
        {
            Update();
            var conditionObj = _currentPlayerCondition as ManagedObject;
            if (conditionObj == null)
            {
                return false;
            }

            var conditionType = conditionObj.GetTypeDefinition();
            if (conditionType?.FindMethod("set_DopingTimer") == null)
            {
                return false;
            }

            conditionObj.Call("set_DopingTimer", durationSeconds);

            if (conditionType.FindMethod("updateDopingTimer") != null)
            {
                conditionObj.Call("updateDopingTimer");
            }

            return true;
        }

        public bool TryClearDopingStatus()
        {
            Update();
            var conditionObj = _currentPlayerCondition as ManagedObject;
            if (conditionObj == null)
            {
                return false;
            }

            var conditionType = conditionObj.GetTypeDefinition();
            if (conditionType?.FindMethod("set_DopingTimer") == null)
            {
                return false;
            }

            conditionObj.Call("set_DopingTimer", 0f);

            if (conditionType.FindMethod("updateDopingTimer") != null)
            {
                conditionObj.Call("updateDopingTimer");
            }

            return true;
        }
        private ManagedObject? TryGetUserVariablesUpdater(ManagedObject conditionObj)
        {
            var conditionType = conditionObj.GetTypeDefinition();
            if (conditionType?.FindMethod("get_UserVariablesUpdater") == null)
            {
                return null;
            }

            var updaterRet = conditionObj.Call("get_UserVariablesUpdater");
            if (updaterRet == null)
            {
                return null;
            }

            if (updaterRet is ManagedObject managedUpdater)
            {
                return managedUpdater;
            }

            var extracted = ExtractFromInvokeRet(updaterRet);
            return extracted as ManagedObject;
        }

        /// <summary>
        /// Get the HitPointController for direct manipulation if needed
        /// </summary>
        public object? GetHitPointController()
        {
            Update();
            return _hitPointController;
        }

        /// <summary>
        /// Set invulnerable state (for tracking active effects)
        /// </summary>
        public void SetInvulnerable(bool invulnerable)
        {
            _isInvulnerable = invulnerable;
        }

        /// <summary>
        /// Set one-hit KO state (for tracking active effects)
        /// </summary>
        public void SetOneHitKO(bool oneHitKO)
        {
            _isOneHitKO = oneHitKO;
        }

        /// <summary>
        /// Start OHKO mode - stores original health and sets timer
        /// </summary>
        public bool StartOHKO(float originalHealth, int durationMs)
        {
            if (_isOneHitKO)
            {
                LogError("GameState: OHKO already active");
                return false;
            }

            _ohkoOriginalHealth = originalHealth;
            _ohkoTimer = durationMs / 1000.0f; // Convert milliseconds to seconds
            _isOneHitKO = true;
            
            // Set health to 1
            if (!SetHealth(1))
            {
                LogError("GameState: Failed to set health to 1 for OHKO");
                _ohkoOriginalHealth = null;
                _ohkoTimer = -1.0f;
                _isOneHitKO = false;
                return false;
            }

            // Force refresh health after setting to ensure it's actually 1
            // This prevents IsGameReady from being false due to stale health values
            UpdateHealth();
            
            // Verify health was set correctly
            if (_currentHealth <= 0)
            {
                LogError($"GameState: OHKO health verification failed - health is {_currentHealth}, retrying...");
                // Try setting again
                if (!SetHealth(1))
                {
                    LogError("GameState: Failed to set health to 1 for OHKO on retry");
                    _ohkoOriginalHealth = null;
                    _ohkoTimer = -1.0f;
                    _isOneHitKO = false;
                    return false;
                }
                UpdateHealth();
            }

            LogInfo($"GameState: OHKO started - Original health: {originalHealth}, Current health: {_currentHealth}, Duration: {_ohkoTimer}s");
            return true;
        }

        /// <summary>
        /// Update OHKO mode - maintains health at 1 and counts down timer
        /// Returns true if OHKO is still active, false if it expired
        /// </summary>
        public bool UpdateOHKO(float deltaTime, bool isGameReady, out bool wasPaused, out bool justResumed)
        {
            wasPaused = false;
            justResumed = false;

            // If not active, ensure state is fully cleared
            if (!_isOneHitKO)
            {
                if (_ohkoOriginalHealth != null || _ohkoTimer > 0 || _ohkoRequestId > 0)
                {
                    LogInfo("GameState: Clearing stale OHKO state");
                    _ohkoOriginalHealth = null;
                    _ohkoTimer = -1.0f;
                    _ohkoRequestId = 0;
                    _ohkoRequestID = null;
                    _ohkoWasPaused = false;
                }
                return false;
            }

            // If active but missing original health, clear state
            if (_ohkoOriginalHealth == null)
            {
                LogInfo("GameState: OHKO active but missing original health, clearing state");
                _ohkoTimer = -1.0f;
                _isOneHitKO = false;
                _ohkoRequestId = 0;
                _ohkoRequestID = null;
                _ohkoWasPaused = false;
                return false;
            }

            // Handle pause/resume
            if (!isGameReady)
            {
                wasPaused = true;
                if (!_ohkoWasPaused)
                {
                    _ohkoWasPaused = true;
                    LogInfo("GameState: OHKO paused (game not ready)");
                }
                // Don't count down timer when paused, but maintain health at 1
                if (_currentHealth > 1)
                {
                    SetHealth(1);
                }
                return true; // Still active, just paused
            }
            else
            {
                // Game is ready
                if (_ohkoWasPaused)
                {
                    justResumed = true;
                    _ohkoWasPaused = false;
                    LogInfo("GameState: OHKO resumed (game ready)");
                }
            }

            // If timer already expired, clear state immediately
            if (_ohkoTimer <= 0)
            {
                // Restore original health
                float restoreHealth = _ohkoOriginalHealth.Value;
                LogInfo($"GameState: OHKO expired, restoring health to {restoreHealth}");
                
                if (SetHealth(restoreHealth))
                {
                    LogInfo("GameState: Health restored successfully");
                }
                else
                {
                    LogError("GameState: Failed to restore health after OHKO");
                }

                // Clear OHKO state completely
                _ohkoOriginalHealth = null;
                _ohkoTimer = -1.0f;
                _isOneHitKO = false;
                _ohkoRequestId = 0;
                _ohkoRequestID = null;
                _ohkoWasPaused = false;
                return false; // OHKO expired
            }

            // Count down timer only when game is ready
            _ohkoTimer -= deltaTime;

            // Maintain health at 1 while active
            if (_currentHealth > 1)
            {
                SetHealth(1);
            }

            // Check if timer expired after counting down
            if (_ohkoTimer <= 0)
            {
                // Restore original health
                float restoreHealth = _ohkoOriginalHealth.Value;
                LogInfo($"GameState: OHKO expired, restoring health to {restoreHealth}");
                
                if (SetHealth(restoreHealth))
                {
                    LogInfo("GameState: Health restored successfully");
                }
                else
                {
                    LogError("GameState: Failed to restore health after OHKO");
                }

                // Clear OHKO state completely
                _ohkoOriginalHealth = null;
                _ohkoTimer = -1.0f;
                _isOneHitKO = false;
                _ohkoRequestId = 0;
                _ohkoRequestID = null;
                _ohkoWasPaused = false;
                return false; // OHKO expired
            }

            return true; // Still active
        }

        /// <summary>
        /// Stop OHKO mode and restore health
        /// </summary>
        public void StopOHKO()
        {
            if (!_isOneHitKO || _ohkoOriginalHealth == null)
            {
                return;
            }

            float restoreHealth = _ohkoOriginalHealth.Value;
            LogInfo($"GameState: Stopping OHKO, restoring health to {restoreHealth}");
            
            SetHealth(restoreHealth);
            
            _ohkoOriginalHealth = null;
            _ohkoTimer = -1.0f;
            _isOneHitKO = false;
        }

        /// <summary>
        /// Get OHKO request ID for sending Stopped response
        /// </summary>
        public int GetOHKORequestId() => _ohkoRequestId;
        
        /// <summary>
        /// Get OHKO request ID string for sending Stopped response
        /// </summary>
        public string? GetOHKORequestID() => _ohkoRequestID;

        /// <summary>
        /// Set OHKO request ID for tracking
        /// </summary>
        public void SetOHKORequestId(int requestId, string? requestID)
        {
            _ohkoRequestId = requestId;
            _ohkoRequestID = requestID;
        }

        /// <summary>
        /// Start Invincibility mode - stores original health and sets timer
        /// </summary>
        public bool StartInvincibility(float originalHealth, int durationMs)
        {
            if (_isInvulnerable)
            {
                LogError("GameState: Invincibility already active");
                return false;
            }

            _invulOriginalHealth = originalHealth;
            _invulTimer = durationMs / 1000.0f; // Convert milliseconds to seconds
            _isInvulnerable = true;
            
            // Set health to max to start invincibility
            if (!SetHealth(_maxHealth))
            {
                LogError("GameState: Failed to set health to max for invincibility");
                _invulOriginalHealth = null;
                _invulTimer = -1.0f;
                _isInvulnerable = false;
                return false;
            }

            // Force refresh health after setting to ensure it's actually max
            // This prevents IsGameReady from being false due to stale health values
            UpdateHealth();
            
            LogInfo($"GameState: Invincibility started - Original health: {originalHealth}, Current health: {_currentHealth}, Duration: {_invulTimer}s");
            return true;
        }

        /// <summary>
        /// Update Invincibility mode - maintains health at max and counts down timer
        /// Returns true if invincibility is still active, false if it expired
        /// </summary>
        public bool UpdateInvincibility(float deltaTime, bool isGameReady, out bool wasPaused, out bool justResumed)
        {
            wasPaused = false;
            justResumed = false;

            // If not active, ensure state is fully cleared
            if (!_isInvulnerable)
            {
                if (_invulOriginalHealth != null || _invulTimer > 0 || _invulRequestId > 0)
                {
                    LogInfo("GameState: Clearing stale invincibility state");
                    _invulOriginalHealth = null;
                    _invulTimer = -1.0f;
                    _invulRequestId = 0;
                    _invulRequestID = null;
                    _invulWasPaused = false;
                }
                return false;
            }

            // If active but missing original health, clear state
            if (_invulOriginalHealth == null)
            {
                LogInfo("GameState: Invincibility active but missing original health, clearing state");
                _invulTimer = -1.0f;
                _isInvulnerable = false;
                _invulRequestId = 0;
                _invulRequestID = null;
                _invulWasPaused = false;
                return false;
            }

            // Handle pause/resume
            if (!isGameReady)
            {
                wasPaused = true;
                if (!_invulWasPaused)
                {
                    _invulWasPaused = true;
                    LogInfo("GameState: Invincibility paused (game not ready)");
                }
                // Don't count down timer when paused, but maintain health at max
                if (_currentHealth < _maxHealth)
                {
                    SetHealth(_maxHealth);
                }
                return true; // Still active, just paused
            }
            else
            {
                // Game is ready
                if (_invulWasPaused)
                {
                    justResumed = true;
                    _invulWasPaused = false;
                    LogInfo("GameState: Invincibility resumed (game ready)");
                }
            }

            // If timer already expired, clear state immediately
            if (_invulTimer <= 0)
            {
                // Restore original health when invincibility expires
                float restoreHealth = _invulOriginalHealth.Value;
                LogInfo($"GameState: Invincibility expired, restoring health to {restoreHealth}");
                
                if (SetHealth(restoreHealth))
                {
                    LogInfo("GameState: Health restored successfully");
                }
                else
                {
                    LogError("GameState: Failed to restore health after invincibility");
                }

                // Clear invincibility state completely
                _invulOriginalHealth = null;
                _invulTimer = -1.0f;
                _isInvulnerable = false;
                _invulRequestId = 0;
                _invulRequestID = null;
                _invulWasPaused = false;
                return false; // Invincibility expired
            }

            // Count down timer only when game is ready
            _invulTimer -= deltaTime;

            // Maintain health at max while active
            if (_currentHealth < _maxHealth)
            {
                SetHealth(_maxHealth);
            }

            // Check if timer expired after counting down
            if (_invulTimer <= 0)
            {
                // Restore original health when invincibility expires
                float restoreHealth = _invulOriginalHealth.Value;
                LogInfo($"GameState: Invincibility expired, restoring health to {restoreHealth}");
                
                if (SetHealth(restoreHealth))
                {
                    LogInfo("GameState: Health restored successfully");
                }
                else
                {
                    LogError("GameState: Failed to restore health after invincibility");
                }

                // Clear invincibility state completely
                _invulOriginalHealth = null;
                _invulTimer = -1.0f;
                _isInvulnerable = false;
                _invulRequestId = 0;
                _invulRequestID = null;
                _invulWasPaused = false;
                return false; // Invincibility expired
            }

            return true; // Still active
        }

        /// <summary>
        /// Stop Invincibility mode and restore original health
        /// </summary>
        public void StopInvincibility()
        {
            if (!_isInvulnerable || _invulOriginalHealth == null)
            {
                return;
            }

            float restoreHealth = _invulOriginalHealth.Value;
            LogInfo($"GameState: Stopping invincibility, restoring health to {restoreHealth}");
            
            SetHealth(restoreHealth);
            
            _invulOriginalHealth = null;
            _invulTimer = -1.0f;
            _isInvulnerable = false;
        }

        /// <summary>
        /// Get Invincibility request ID for sending Stopped response
        /// </summary>
        public int GetInvincibilityRequestId() => _invulRequestId;
        
        /// <summary>
        /// Get Invincibility request ID string for sending Stopped response
        /// </summary>
        public string? GetInvincibilityRequestID() => _invulRequestID;

        /// <summary>
        /// Set Invincibility request ID for tracking
        /// </summary>
        public void SetInvincibilityRequestId(int requestId, string? requestID)
        {
            _invulRequestId = requestId;
            _invulRequestID = requestID;
        }

        /// <summary>
        /// Get primary camera from the game (mimics sdk.get_primary_camera from LUA)
        /// </summary>
        private object? CacheAndReturnCamera(object? camera)
        {
            if (camera == null)
            {
                return null;
            }
            
            _lastPrimaryCamera = camera;
            _lastPrimaryCameraTime = DateTime.Now;
            return camera;
        }
        
        private object? GetPrimaryCamera()
        {
            var now = DateTime.Now;
            if (_lastPrimaryCamera != null && (now - _lastPrimaryCameraTime).TotalSeconds < 5.0)
            {
                return _lastPrimaryCamera;
            }
            try
            {
                // Approach 1: Try CameraManager (game-specific)
                try
                {
                    var tdb = API.GetTDB();
                    var cameraManagerType = tdb?.FindType("offline.CameraManager");
                    var hasCurrentCamera = cameraManagerType?.FindMethod("get_CurrentCamera") != null;
                    var hasMainCamera = cameraManagerType?.FindMethod("get_MainCamera") != null;
                    var cameraManager = API.GetManagedSingleton("offline.CameraManager");
                    if (cameraManager != null)
                    {
                        var cameraManagerObj = cameraManager as ManagedObject;
                        if (cameraManagerObj != null)
                        {
                            // Try get_CurrentCamera first
                            if (hasCurrentCamera)
                            {
                                var cameraRet = cameraManagerObj.Call("get_CurrentCamera");
                                if (cameraRet != null)
                                {
                                    var camera = ExtractFromInvokeRet(cameraRet);
                                    if (camera != null)
                                    {
                                        return CacheAndReturnCamera(camera);
                                    }
                                }
                            }

                            // Try get_MainCamera
                            if (hasMainCamera)
                            {
                                var cameraRet = cameraManagerObj.Call("get_MainCamera");
                                if (cameraRet != null)
                                {
                                    var camera = ExtractFromInvokeRet(cameraRet);
                                    if (camera != null)
                                    {
                                        return CacheAndReturnCamera(camera);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"GameState: CameraManager approach failed - {ex.Message}");
                }
                
                // Approach 1b: Try CameraManager via native singleton
                try
                {
                    var tdb = API.GetTDB();
                    var cameraManagerType = tdb?.FindType("offline.CameraManager");
                    var hasCurrentCamera = cameraManagerType?.FindMethod("get_CurrentCamera") != null;
                    var hasMainCamera = cameraManagerType?.FindMethod("get_MainCamera") != null;
                    var cameraManagerNative = API.GetNativeSingleton("offline.CameraManager");
                    if (cameraManagerNative != null)
                    {
                        if (hasCurrentCamera)
                        {
                            var cameraRet = cameraManagerNative.Call("get_CurrentCamera");
                            if (cameraRet != null)
                            {
                                var camera = ExtractFromInvokeRet(cameraRet);
                                if (camera != null)
                                {
                                    return CacheAndReturnCamera(camera);
                                }
                            }
                        }
                        
                        if (hasMainCamera)
                        {
                            var cameraRet = cameraManagerNative.Call("get_MainCamera");
                            if (cameraRet != null)
                            {
                                var camera = ExtractFromInvokeRet(cameraRet);
                                if (camera != null)
                                {
                                    return CacheAndReturnCamera(camera);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"GameState: Native CameraManager approach failed - {ex.Message}");
                }
                
                // Approach 2: Try via.Camera static methods
                try
                {
                    var cameraTypeDef = API.GetTDB()?.FindType("via.Camera");
                    if (cameraTypeDef != null)
                    {
                        // Try get_PrimaryCamera static method
                        var primaryCameraMethod = cameraTypeDef.FindMethod("get_PrimaryCamera");
                        if (primaryCameraMethod != null)
                        {
                            try
                            {
                                var cameraRet = primaryCameraMethod.Invoke(null, null);
                                // Extract actual value from InvokeRet wrapper
                                var camera = ExtractFromInvokeRet(cameraRet);
                                if (camera != null)
                                {
                                    return CacheAndReturnCamera(camera);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogInfo($"GameState: Failed to invoke get_PrimaryCamera - {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"GameState: via.Camera static method approach failed - {ex.Message}");
                }
                
                // Approach 3: Try singletons
                try
                {
                    var viaCamera = API.GetNativeSingleton("via.Camera");
                    if (viaCamera != null)
                    {
                        return CacheAndReturnCamera(viaCamera);
                    }
                    
                    var managedCamera = API.GetManagedSingleton("via.Camera");
                    if (managedCamera != null)
                    {
                        return CacheAndReturnCamera(managedCamera);
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"GameState: Singleton approach failed - {ex.Message}");
                }
                
                // Approach 4: Use SDK approach (SceneManager -> MainView -> PrimaryCamera)
                if (UseSceneManagerCamera && _isGameReady && _playerManager != null)
                {
                    try
                    {
                        var tdb = API.GetTDB();
                        var sceneManagerType = tdb?.FindType("via.SceneManager");
                        var sceneViewType = tdb?.FindType("via.SceneView");
                        var mainViewMethod = sceneManagerType?.FindMethod("get_MainView");
                        var primaryCameraMethod = sceneViewType?.FindMethod("get_PrimaryCamera");
                        if (sceneManagerType != null && mainViewMethod != null && primaryCameraMethod != null)
                        {
                            object? sceneManager = API.GetManagedSingleton("via.SceneManager");
                            if (sceneManager == null)
                            {
                                sceneManager = API.GetNativeSingleton("via.SceneManager");
                            }
                            
                            if (sceneManager != null)
                            {
                                var sceneManagerObj = sceneManager as ManagedObject;
                                var sceneManagerNative = sceneManager as NativeObject;
                                if (sceneManagerObj == null && sceneManagerNative != null)
                                {
                                    // Fallback to native only when game is ready to reduce access violations
                                    try
                                    {
                                        var mainViewRet = sceneManagerNative.Call("get_MainView");
                                        if (mainViewRet != null)
                                        {
                                            var mainView = ExtractFromInvokeRet(mainViewRet);
                                            if (mainView != null)
                                            {
                                                var mainViewObj = mainView as ManagedObject;
                                                if (mainViewObj != null)
                                                {
                                                    var cameraRet = mainViewObj.Call("get_PrimaryCamera");
                                                    if (cameraRet != null)
                                                    {
                                                        var camera = ExtractFromInvokeRet(cameraRet);
                                                        if (camera != null)
                                                        {
                                                            return CacheAndReturnCamera(camera);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                                else if (sceneManagerObj != null)
                                {
                                    var mainViewRet = sceneManagerObj.Call("get_MainView");
                                    if (mainViewRet != null)
                                    {
                                        var mainView = ExtractFromInvokeRet(mainViewRet);
                                        if (mainView != null)
                                        {
                                            var mainViewObj = mainView as ManagedObject;
                                            if (mainViewObj != null)
                                            {
                                                var cameraRet = mainViewObj.Call("get_PrimaryCamera");
                                                if (cameraRet != null)
                                                {
                                                    var camera = ExtractFromInvokeRet(cameraRet);
                                                    if (camera != null)
                                                    {
                                                            return CacheAndReturnCamera(camera);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogInfo($"GameState: SDK approach failed - {ex.Message}");
                    }
                }

                // Approach 5: Use WwiseCameraStateManager CurrentCamera (GameObject) if method exists
                try
                {
                    var tdb = API.GetTDB();
                    var wwiseType = tdb?.FindType("offline.WwiseCameraStateManager");
                    if (wwiseType?.FindMethod("get_CurrentCamera") != null)
                    {
                        var wwiseCameraManager = API.GetManagedSingleton("offline.WwiseCameraStateManager");
                        if (wwiseCameraManager != null)
                        {
                            var wwiseObj = wwiseCameraManager as ManagedObject;
                            if (wwiseObj != null)
                            {
                                var cameraRet = wwiseObj.Call("get_CurrentCamera");
                                var camera = ExtractFromInvokeRet(cameraRet);
                                if (camera != null)
                                {
                                    return CacheAndReturnCamera(camera);
                                }
                            }
                        }
                    }
                }
                catch { }

                // Approach 6: Use SoundListenerController CurrentCamera (GameObject) if method exists
                try
                {
                    var tdb = API.GetTDB();
                    var soundType = tdb?.FindType("offline.SoundListenerController");
                    if (soundType?.FindMethod("get_CurrentCamera") != null)
                    {
                        var soundListener = API.GetManagedSingleton("offline.SoundListenerController");
                        if (soundListener != null)
                        {
                            var soundObj = soundListener as ManagedObject;
                            if (soundObj != null)
                            {
                                var cameraRet = soundObj.Call("get_CurrentCamera");
                                var camera = ExtractFromInvokeRet(cameraRet);
                                if (camera != null)
                                {
                                    return CacheAndReturnCamera(camera);
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error getting primary camera - {ex.Message}");
            }

            if (_lastPrimaryCamera != null && (now - _lastPrimaryCameraTime).TotalSeconds < 5.0)
            {
                return _lastPrimaryCamera;
            }
            
            if ((now - _lastCameraFailLog).TotalSeconds > 2.0)
            {
                LogError("GameState: All camera access methods failed");
                _lastCameraFailLog = now;
            }
            return null;
        }

        /// <summary>
        /// Start FOV effect - stores original FOV and sets target FOV
        /// </summary>
        public bool StartFOV(float targetFOV, int durationMs)
        {
            if (_fovActive)
            {
                LogError("GameState: FOV effect already active");
                return false;
            }

            // Get current FOV from camera
            var camera = GetPrimaryCamera();
            if (camera == null)
            {
                LogError("GameState: Cannot get primary camera for FOV effect");
                return false;
            }

            try
            {
                // Try to use ManagedObject.Call if it's a ManagedObject
                var cameraObj = camera as ManagedObject;
                if (cameraObj != null)
                {
                    // Managed camera
                    var fovObj = cameraObj.Call("get_FOV");
                    if (fovObj != null)
                    {
                        _originalFOV = Convert.ToSingle(fovObj);
                    }
                }
                else
                {
                    // For native camera, try to get FOV using reflection
                    // If we can't get it, use default
                    LogInfo("GameState: Native camera detected, using default FOV 81.0");
                    _originalFOV = 81.0f;
                }

                // If we couldn't get original FOV, use default
                if (_originalFOV == null)
                {
                    _originalFOV = 81.0f; // Default FOV
                    LogInfo("GameState: Could not get original FOV, using default 81.0");
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error getting original FOV - {ex.Message}");
                _originalFOV = 81.0f; // Default FOV
            }

            _targetFOV = targetFOV;
            _fovTimer = durationMs / 1000.0f; // Convert milliseconds to seconds
            _fovActive = true;
            
            LogInfo($"GameState: FOV effect started - Original: {_originalFOV}, Target: {targetFOV}, Duration: {_fovTimer}s");
            return true;
        }

        /// <summary>
        /// Update FOV effect - applies target FOV and counts down timer
        /// Returns true if FOV effect is still active, false if it expired
        /// </summary>
        public bool UpdateFOV(float deltaTime, bool isGameReady, out bool wasPaused, out bool justResumed)
        {
            wasPaused = false;
            justResumed = false;

            // If not active, ensure state is fully cleared
            if (!_fovActive)
            {
                if (_originalFOV != null || _fovTimer > 0 || _fovRequestId > 0)
                {
                    LogInfo("GameState: Clearing stale FOV state");
                    _originalFOV = null;
                    _fovTimer = -1.0f;
                    _fovRequestId = 0;
                    _fovRequestID = null;
                    _fovWasPaused = false;
                    _targetFOV = 81.0f;
                }
                return false;
            }

            // Handle pause/resume
            if (!isGameReady)
            {
                wasPaused = true;
                if (!_fovWasPaused)
                {
                    _fovWasPaused = true;
                    LogInfo("GameState: FOV effect paused (game not ready)");
                }
                // Don't count down timer when paused, but still apply FOV
                ApplyFOV();
                return true; // Still active, just paused
            }
            else
            {
                // Game is ready
                if (_fovWasPaused)
                {
                    justResumed = true;
                    _fovWasPaused = false;
                    LogInfo("GameState: FOV effect resumed (game ready)");
                }
            }

            // If timer already expired, clear state immediately
            if (_fovTimer <= 0)
            {
                // Restore original FOV
                if (_originalFOV != null)
                {
                    LogInfo($"GameState: FOV effect expired, restoring FOV to {_originalFOV}");
                    RestoreFOV();
                }

                // Clear FOV state completely
                _originalFOV = null;
                _fovTimer = -1.0f;
                _fovActive = false;
                _fovRequestId = 0;
                _fovRequestID = null;
                _fovWasPaused = false;
                _targetFOV = 81.0f;
                return false; // FOV effect expired
            }

            // Count down timer only when game is ready
            _fovTimer -= deltaTime;

            // Apply target FOV continuously
            ApplyFOV();

            // Check if timer expired after counting down
            if (_fovTimer <= 0)
            {
                // Restore original FOV
                if (_originalFOV != null)
                {
                    LogInfo($"GameState: FOV effect expired, restoring FOV to {_originalFOV}");
                    RestoreFOV();
                }

                // Clear FOV state completely
                _originalFOV = null;
                _fovTimer = -1.0f;
                _fovActive = false;
                _fovRequestId = 0;
                _fovRequestID = null;
                _fovWasPaused = false;
                _targetFOV = 81.0f;
                return false; // FOV effect expired
            }

            return true; // Still active
        }

        /// <summary>
        /// Apply target FOV to camera
        /// Made public so it can be called from BeginRendering callback
        /// </summary>
        public void ApplyFOV()
        {
            if (!_fovActive)
                return;

            var camera = GetPrimaryCamera();
            if (camera == null)
                return;

            try
            {
                // Try to use ManagedObject.Call if it's a ManagedObject
                var cameraObj = camera as ManagedObject;
                if (cameraObj != null)
                {
                    // Managed camera - use Call method
                    try
                    {
                        cameraObj.Call("set_FOV", _targetFOV);
                        // Don't log every frame - only log errors
                    }
                    catch (Exception ex)
                    {
                        LogError($"GameState: Error calling set_FOV on ManagedObject camera - {ex.Message}");
                    }
                }
                else
                {
                    // Try NativeObject
                    var nativeCamera = camera as NativeObject;
                    if (nativeCamera != null)
                    {
                        try
                        {
                            // Get the type definition from the NativeObject
                            var cameraTypeDef = nativeCamera.GetTypeDefinition();
                            if (cameraTypeDef != null)
                            {
                                var setFOVMethod = cameraTypeDef.FindMethod("set_FOV");
                                if (setFOVMethod != null)
                                {
                                    // Invoke set_FOV on the native camera object
                                    setFOVMethod.Invoke(nativeCamera, new object[] { _targetFOV });
                                    // Don't log every frame - only log errors
                                }
                                else
                                {
                                    LogError("GameState: Could not find set_FOV method on NativeObject camera type");
                                }
                            }
                            else
                            {
                                LogError("GameState: NativeObject camera has no type definition");
                            }
                        }
                        catch (Exception ex2)
                        {
                            LogError($"GameState: Error setting FOV on NativeObject camera - {ex2.Message}");
                        }
                    }
                    else
                    {
                        // Fallback: Try to find via.Camera type and invoke method
                        try
                        {
                            var cameraTypeDef = API.GetTDB()?.FindType("via.Camera");
                            if (cameraTypeDef != null)
                            {
                                var setFOVMethod = cameraTypeDef.FindMethod("set_FOV");
                                if (setFOVMethod != null)
                                {
                                    // Invoke set_FOV on the camera object
                                    setFOVMethod.Invoke(camera, new object[] { _targetFOV });
                                }
                                else
                                {
                                    LogError("GameState: Could not find set_FOV method on via.Camera");
                                }
                            }
                            else
                            {
                                LogError("GameState: Could not find via.Camera type definition");
                            }
                        }
                        catch (Exception ex2)
                        {
                            LogError($"GameState: Error setting FOV using fallback method - {ex2.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error applying FOV - {ex.Message}");
            }
        }

        /// <summary>
        /// Restore original FOV to camera
        /// </summary>
        private void RestoreFOV()
        {
            if (_originalFOV == null)
                return;

            var camera = GetPrimaryCamera();
            if (camera == null)
                return;

            try
            {
                // Try to use ManagedObject.Call if it's a ManagedObject
                var cameraObj = camera as ManagedObject;
                if (cameraObj != null)
                {
                    // Managed camera
                    cameraObj.Call("set_FOV", _originalFOV.Value);
                }
                else
                {
                    // Try NativeObject
                    var nativeCamera = camera as NativeObject;
                    if (nativeCamera != null)
                    {
                        var cameraTypeDef = nativeCamera.GetTypeDefinition();
                        if (cameraTypeDef != null)
                        {
                            var setFOVMethod = cameraTypeDef.FindMethod("set_FOV");
                            if (setFOVMethod != null)
                            {
                                setFOVMethod.Invoke(nativeCamera, new object[] { _originalFOV.Value });
                            }
                        }
                    }
                    else
                    {
                        // Fallback: Try to find via.Camera type and invoke method
                        var cameraTypeDef = API.GetTDB()?.FindType("via.Camera");
                        if (cameraTypeDef != null)
                        {
                            var setFOVMethod = cameraTypeDef.FindMethod("set_FOV");
                            if (setFOVMethod != null)
                            {
                                setFOVMethod.Invoke(camera, new object[] { _originalFOV.Value });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error restoring FOV - {ex.Message}");
            }
        }

        /// <summary>
        /// Stop FOV effect and restore original FOV
        /// </summary>
        public void StopFOV()
        {
            if (!_fovActive && _originalFOV == null)
            {
                return;
            }

            if (_originalFOV != null)
            {
                LogInfo($"GameState: Stopping FOV effect, restoring FOV to {_originalFOV}");
                RestoreFOV();
            }

            _originalFOV = null;
            _fovTimer = -1.0f;
            _fovRequestId = 0;
            _fovRequestID = null;
            _fovWasPaused = false;
            _targetFOV = 81.0f;
            _fovActive = false;
        }

        /// <summary>
        /// Get FOV request ID for sending Stopped response
        /// </summary>
        public int GetFOVRequestId() => _fovRequestId;
        
        /// <summary>
        /// Get FOV request ID string for sending Stopped response
        /// </summary>
        public string? GetFOVRequestID() => _fovRequestID;

        /// <summary>
        /// Set FOV request ID for tracking
        /// </summary>
        public void SetFOVRequestId(int requestId, string? requestID)
        {
            _fovRequestId = requestId;
            _fovRequestID = requestID;
        }

        /// <summary>
        /// Get player transform from HeadJoint
        /// </summary>
        public bool GetCharacterGrace()
        {
            if (_playerManager == null)
            {
                return false;
            }

            var playMan = _playerManager as ManagedObject;
            if (playMan == null) return false;

            var ctx = playMan.Call("getPlayerContextRefFast") as ManagedObject;
            if (ctx == null) return false;
            var isGrace = ctx.Call("get_IsCp_A0Character");
            return isGrace != null && Convert.ToBoolean(isGrace);
        }
        public bool GetCharacterLeon()
        {
            if (_playerManager == null)
            {
                return false;
            }

            var playMan = _playerManager as ManagedObject;
            if (playMan == null) return false;

            var ctx = playMan.Call("getPlayerContextRefFast") as ManagedObject;
            if (ctx == null) return false;
            var isLeon = ctx.Call("get_IsCp_A1Character");
            return isLeon != null && Convert.ToBoolean(isLeon);
        }
        private object? GetPlayerTransform()
        {
            try
            {
                var cm = API.GetManagedSingleton("app.CharacterManager") as ManagedObject;
                if (cm == null)
                    return null;

                var ctx = cm.Call("getPlayerContextRef") as ManagedObject;
                if (ctx == null)
                    return null;

                var updater = ctx.Call("get_Updater") as ManagedObject;
                if (updater == null)
                    return null;

                var go = updater.Call("get_GameObject") as ManagedObject;
                if (go == null)
                    return null;

                var transform = go.Call("get_Transform");
                return transform;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error getting player transform - {ex.Message}");
                return null;
            }
        }

        private ManagedObject? GetPostEffectCustomLayer()
        {
            var controller = GetPostEffectController();
            if (controller == null)
                return null;

            var controllerType = controller.GetTypeDefinition();
            if (controllerType?.FindMethod("get_RefCustomLayer") != null)
                return controller.Call("get_RefCustomLayer") as ManagedObject;

            var customLayerField = controller.GetField("<RefCustomLayer>k__BackingField") as ManagedObject;
            return customLayerField;
        }

        private ManagedObject? GetPostEffectController()
        {
            var controller = API.GetManagedSingleton("offline.posteffect.PostEffectController") as ManagedObject;
            if (controller != null)
                return controller;

            var unitRoot = API.GetManagedSingleton("offline.PostEffectControllerUnitRoot") as ManagedObject;
            if (unitRoot == null)
                return null;

            var unitRootType = unitRoot.GetTypeDefinition();
            if (unitRootType?.FindMethod("get_PostEffectController") != null)
                return unitRoot.Call("get_PostEffectController") as ManagedObject;

            if (unitRootType?.FindMethod("get_RefPostEffectController") != null)
                return unitRoot.Call("get_RefPostEffectController") as ManagedObject;

            return unitRoot.GetField("<PostEffectController>k__BackingField") as ManagedObject;
        }

        private IEnumerable<ManagedObject> GetPostEffectFilterParams(ManagedObject customLayer)
        {
            object? listObj = null;
            var customLayerType = customLayer.GetTypeDefinition();
            if (customLayerType?.FindMethod("get_FilterParamList") != null)
                listObj = customLayer.Call("get_FilterParamList");

            if (listObj == null && customLayerType?.FindField("FilterParamList") != null)
                listObj = customLayer.GetField("FilterParamList");
            if (listObj == null && customLayerType?.FindField("<FilterParamList>k__BackingField") != null)
                listObj = customLayer.GetField("<FilterParamList>k__BackingField");
            if (listObj == null)
                return Array.Empty<ManagedObject>();

            if (listObj is System.Collections.IEnumerable enumerable)
            {
                var list = new List<ManagedObject>();
                foreach (var item in enumerable)
                {
                    if (item is ManagedObject managedItem)
                        list.Add(managedItem);
                }
                return list;
            }

            if (listObj is ManagedObject managedList)
            {
                var list = new List<ManagedObject>();
                var listType = managedList.GetTypeDefinition();
                object? countObj = null;
                if (listType?.FindMethod("get_Count") != null)
                    countObj = managedList.Call("get_Count");
                if (countObj == null && listType?.FindMethod("get_Length") != null)
                    countObj = managedList.Call("get_Length");
                if (countObj != null && int.TryParse(countObj.ToString(), out var count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        object? item = null;
                        if (listType?.FindMethod("get_Item") != null)
                            item = managedList.Call("get_Item", i);
                        if (item == null && listType?.FindMethod("Get") != null)
                            item = managedList.Call("Get", i);
                        if (item is ManagedObject managedItem)
                            list.Add(managedItem);
                    }
                }
                return list;
            }

            return Array.Empty<ManagedObject>();
        }

        private static string? GetPostEffectParamName(ManagedObject param)
        {
            var nameObj = param.Call("get_Name") ?? param.Call("get_DisplayName") ?? param.Call("get_Title");
            if (nameObj != null)
                return nameObj.ToString();

            var gameObject = param.Call("get_GameObject") as ManagedObject;
            if (gameObject != null)
            {
                var goName = gameObject.Call("get_Name");
                if (goName != null)
                    return goName.ToString();
            }

            return param.ToString();
        }

        /// <summary>
        /// Start player scale effect - stores original scale and sets target scale
        /// </summary>
        public bool StartScale(float targetScale, int durationMs, bool isGiant)
        {
            if (_scaleActive)
            {
                LogError("GameState: Scale effect already active");
                return false;
            }

            // Get current scale from player transform
            var transform = GetPlayerTransform();
            if (transform == null)
            {
                LogError("GameState: Cannot get player transform for scale effect");
                return false;
            }

            try
            {
                var transformObj = transform as ManagedObject;
                if (transformObj == null)
                {
                    LogError("GameState: Player transform is not a ManagedObject");
                    return false;
                }

                // Get current scale
                var scaleObj = transformObj.Call("get_LocalScale");
                if (scaleObj == null)
                {
                    LogError("GameState: Cannot get LocalScale from transform");
                    return false;
                }

                LogInfo($"GameState: Scale object type: {scaleObj.GetType().Name}");

                // Try as ManagedObject first
                var scaleManagedObj = scaleObj as ManagedObject;
                if (scaleManagedObj != null)
                {
                    // Try get_X (uppercase) first, then get_x (lowercase)
                    var xObj = scaleManagedObj.Call("get_X");
                    if (xObj == null)
                    {
                        xObj = scaleManagedObj.Call("get_x");
                    }
                    if (xObj != null)
                    {
                        _originalScale = Convert.ToSingle(xObj);
                        LogInfo($"GameState: Got original scale from ManagedObject: {_originalScale}");
                    }
                    else
                    {
                        _originalScale = 1.0f; // Default scale
                        LogInfo("GameState: Could not get original scale, using default 1.0");
                    }
                }
                else
                {
                    // Try as ValueType
                    var valueType = scaleObj as REFrameworkNET.ValueType;
                    if (valueType != null)
                    {
                        try
                        {
                            // Try to get Value property
                            var valueProperty = valueType.GetType().GetProperty("Value");
                            if (valueProperty != null)
                            {
                                var value = valueProperty.GetValue(valueType);
                                if (value != null)
                                {
                                    var valueManaged = value as ManagedObject;
                                    if (valueManaged != null)
                                    {
                                        var xObj = valueManaged.Call("get_X") ?? valueManaged.Call("get_x");
                                        if (xObj != null)
                                        {
                                            _originalScale = Convert.ToSingle(xObj);
                                            LogInfo($"GameState: Got original scale from ValueType: {_originalScale}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"GameState: Error extracting scale from ValueType - {ex.Message}");
                        }
                    }
                    
                    if (_originalScale == null)
                    {
                        _originalScale = 1.0f; // Default scale
                        LogInfo("GameState: Could not get original scale, using default 1.0");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error getting original scale - {ex.Message}");
                _originalScale = 1.0f; // Default scale
            }

            _targetScale = targetScale;
            _scaleTimer = durationMs / 1000.0f; // Convert milliseconds to seconds
            _scaleActive = true;
            _isGiant = isGiant;
            
            // For tiny player, also adjust camera height
            if (!isGiant)
            {
                try
                {
                    var camera = GetPrimaryCamera();
                    if (camera != null)
                    {
                        var cameraObj = camera as ManagedObject;
                        if (cameraObj != null)
                        {
                            // Approach based on FreeCam.cpp: camera->ownerGameObject->transform
                            ManagedObject? transformObj = null;
                            
                            // Step 1: Get GameObject (like camera->ownerGameObject in C++)
                            object? gameObject = null;
                            try
                            {
                                // Try ownerGameObject field first (C++ style, like Camera.cpp line 111)
                                var cameraManaged = cameraObj as ManagedObject;
                                if (cameraManaged != null)
                                {
                                    gameObject = cameraManaged.GetField("ownerGameObject");
                                    if (gameObject == null)
                                    {
                                        // Try get_GameObject property
                                        gameObject = cameraManaged.Call("get_GameObject");
                                    }
                                }
                                
                                if (gameObject == null)
                                {
                                    LogInfo("GameState: Could not get GameObject from camera (both ownerGameObject field and get_GameObject failed)");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogInfo($"GameState: Error getting GameObject - {ex.Message}");
                            }
                            
                            if (gameObject != null)
                            {
                                var gameObj = gameObject as ManagedObject;
                                if (gameObj != null)
                                {
                                    // Step 2: Get transform from GameObject (like camera->ownerGameObject->transform)
                                    try
                                    {
                                        transformObj = gameObj.Call("get_Transform") as ManagedObject;
                                        if (transformObj == null)
                                        {
                                            LogInfo("GameState: Could not get Transform from GameObject");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogInfo($"GameState: Error getting Transform from GameObject - {ex.Message}");
                                    }
                                    
                                    if (transformObj != null)
                                    {
                                        _cameraTransform = transformObj;
                                        
                                        // Step 3: Get position from transform (simplified approach)
                                        try
                                        {
                                            var positionObj = transformObj.Call("get_Position");
                                            if (positionObj != null)
                                            {
                                                // Try to get Y using the same method that works for FOV
                                                var positionValueType = positionObj as REFrameworkNET.ValueType;
                                                if (positionValueType != null)
                                                {
                                                    // Try indexer first (most reliable)
                                                    var yObj = positionValueType.Call("get_Item", 1);
                                                    if (yObj != null)
                                                    {
                                                        _originalCameraY = Convert.ToSingle(yObj);
                                                        // Stored original camera Y position
                                                    }
                                                    else
                                                    {
                                                        // Try direct cast to via.vec3
                                                        try
                                                        {
                                                            var vec3Pos = positionValueType as via.vec3;
                                                            if (vec3Pos != null)
                                                            {
                                                                _originalCameraY = vec3Pos.y;
                                                                // Stored original camera Y position (direct cast)
                                                            }
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogInfo($"GameState: Error getting camera Y from Transform - {ex.Message}");
                                        }
                                        
                                        // Step 4: If transform position didn't work, try joint position (like FreeCam.cpp)
                                        // FreeCam.cpp line 164-165: m_last_camera_matrix[3] = sdk::get_joint_position(joint);
                                        if (_originalCameraY == null)
                                        {
                                            try
                                            {
                                                LogInfo("GameState: Attempting to get position from joint...");
                                                // Get joints from transform (like get_Joints in C++)
                                                var jointsObj = transformObj.Call("get_Joints");
                                                if (jointsObj != null)
                                                {
                                                    LogInfo($"GameState: Got joints object, type: {jointsObj.GetType().Name}");
                                                    
                                                    // Try as ManagedObject first (it might be a managed array wrapper)
                                                    var jointsManaged = jointsObj as ManagedObject;
                                                    if (jointsManaged != null)
                                                    {
                                                        // Try to get first element using array accessor or method
                                                        try
                                                        {
                                                            // Try get_Item or [] accessor
                                                            var firstJoint = jointsManaged.Call("get_Item", 0) ?? jointsManaged.Call("Get", 0);
                                                            if (firstJoint == null)
                                                            {
                                                                // Try as IEnumerable
                                                                var jointsEnumerable = jointsObj as System.Collections.IEnumerable;
                                                                if (jointsEnumerable != null)
                                                                {
                                                                    foreach (var joint in jointsEnumerable)
                                                                    {
                                                                        if (joint != null)
                                                                        {
                                                                            var jointObj = joint as ManagedObject;
                                                                            if (jointObj != null)
                                                                            {
                                                                                // Get position from joint (like sdk::get_joint_position(joint) in C++)
                                                                                var jointPosObj = jointObj.Call("get_Position");
                                                                                if (jointPosObj != null)
                                                                                {
                                                                                    // Try same approach as transform position
                                                                                    var jointPosValueType = jointPosObj as REFrameworkNET.ValueType;
                                                                                    if (jointPosValueType != null)
                                                                                    {
                                                                                        var yProp = jointPosValueType.GetType().GetProperty("y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                                                        if (yProp == null)
                                                                                        {
                                                                                            yProp = jointPosValueType.GetType().GetProperty("Y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                                                        }
                                                                                        if (yProp != null)
                                                                                        {
                                                                                            _originalCameraY = Convert.ToSingle(yProp.GetValue(jointPosValueType));
                                                                                            LogInfo($"GameState: Stored original camera Y position via Joint: {_originalCameraY}");
                                                                                            break;
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                var jointObj = firstJoint as ManagedObject;
                                                                if (jointObj != null)
                                                                {
                                                                    var jointPosObj = jointObj.Call("get_Position");
                                                                    if (jointPosObj != null)
                                                                    {
                                                                        var jointPosValueType = jointPosObj as REFrameworkNET.ValueType;
                                                                        if (jointPosValueType != null)
                                                                        {
                                                                            var yProp = jointPosValueType.GetType().GetProperty("y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                                            if (yProp == null)
                                                                            {
                                                                                yProp = jointPosValueType.GetType().GetProperty("Y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                                            }
                                                                            if (yProp != null)
                                                                            {
                                                                                _originalCameraY = Convert.ToSingle(yProp.GetValue(jointPosValueType));
                                                                                LogInfo($"GameState: Stored original camera Y position via Joint: {_originalCameraY}");
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            LogInfo($"GameState: Error accessing joint from ManagedObject - {ex.Message}");
                                                        }
                                                    }
                                                    
                                                    // Try as System.Array
                                                    if (_originalCameraY == null)
                                                    {
                                                        var jointsArray = jointsObj as System.Array;
                                                        if (jointsArray != null && jointsArray.Length > 0)
                                                        {
                                                            LogInfo($"GameState: Joints array has {jointsArray.Length} elements");
                                                            var joint = jointsArray.GetValue(0);
                                                            if (joint != null)
                                                            {
                                                                var jointObj = joint as ManagedObject;
                                                                if (jointObj != null)
                                                                {
                                                                    var jointPosObj = jointObj.Call("get_Position");
                                                                    if (jointPosObj != null)
                                                                    {
                                                                        var jointPosValueType = jointPosObj as REFrameworkNET.ValueType;
                                                                        if (jointPosValueType != null)
                                                                        {
                                                                            var yProp = jointPosValueType.GetType().GetProperty("y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                                            if (yProp == null)
                                                                            {
                                                                                yProp = jointPosValueType.GetType().GetProperty("Y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                                            }
                                                                            if (yProp != null)
                                                                            {
                                                                                _originalCameraY = Convert.ToSingle(yProp.GetValue(jointPosValueType));
                                                                                LogInfo($"GameState: Stored original camera Y position via Joint: {_originalCameraY}");
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            LogInfo($"GameState: Joints is not an Array or is empty");
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    LogInfo("GameState: Could not get Joints from transform");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogInfo($"GameState: Error getting camera Y from Joint - {ex.Message}");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    LogInfo($"GameState: GameObject is not a ManagedObject (type: {gameObject.GetType().Name})");
                                }
                            }
                            
                            // Alternative approach: Try to get camera position from WorldMatrix
                            if (_originalCameraY == null)
                            {
                                try
                                {
                                    var worldMatrix = cameraObj.Call("get_WorldMatrix");
                                    if (worldMatrix != null)
                                    {
                                        // WorldMatrix is a 4x4 matrix, position is in the 4th column (index 3)
                                        // Try to extract Y from matrix[3][1] or matrix[3].y
                                        var matrixValueType = worldMatrix as REFrameworkNET.ValueType;
                                        if (matrixValueType != null)
                                        {
                                            // Try to get the matrix array or fields
                                            var matrixType = matrixValueType.GetType();
                                            
                                            // Try common matrix field names
                                            var fields = matrixType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                            foreach (var field in fields)
                                            {
                                                if (field.Name.ToLower().Contains("m") || field.Name.ToLower().Contains("matrix"))
                                                {
                                                    try
                                                    {
                                                        var matrixData = field.GetValue(matrixValueType);
                                                        // Matrix position is typically at [3][1] for Y
                                                        // This is a simplified approach - we might need to access it differently
                                                    }
                                                    catch { }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogInfo($"GameState: Could not get camera Y from WorldMatrix - {ex.Message}");
                                }
                            }
                            
                            // If we still don't have it, try to get it from the player's head position as a fallback
                            if (_originalCameraY == null)
                            {
                                try
                                {
                                    var playerTransform = GetPlayerTransform();
                                    if (playerTransform != null)
                                    {
                                        var playerTransformObj = playerTransform as ManagedObject;
                                        if (playerTransformObj != null)
                                        {
                                            var positionObj = playerTransformObj.Call("get_Position");
                                            if (positionObj != null)
                                            {
                                                var positionValueType = positionObj as REFrameworkNET.ValueType;
                                                if (positionValueType != null)
                                                {
                                                    var yField = positionValueType.GetType().GetField("y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                    if (yField == null)
                                                    {
                                                        yField = positionValueType.GetType().GetField("Y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                    }
                                                    if (yField != null)
                                                    {
                                                        float playerY = Convert.ToSingle(yField.GetValue(positionValueType));
                                                        // Camera is typically slightly above player head, estimate ~1.6 units
                                                        _originalCameraY = playerY + 1.6f;
                                                        LogInfo($"GameState: Estimated original camera Y from player position: {_originalCameraY} (player Y: {playerY})");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogInfo($"GameState: Could not estimate camera Y from player position - {ex.Message}");
                                }
                            }
                            
                            if (_originalCameraY == null)
                            {
                                LogError("GameState: Could not get camera Y position using any method - camera height adjustment will not work");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"GameState: Could not access camera for height adjustment - {ex.Message}");
                }
            }
            
            LogInfo($"GameState: Scale effect started - Original: {_originalScale}, Target: {targetScale}, Duration: {_scaleTimer}s, IsGiant: {isGiant}");
            return true;
        }

        /// <summary>
        /// Update scale effect - applies target scale and counts down timer
        /// Returns true if scale effect is still active, false if it expired
        /// </summary>
        public bool UpdateScale(float deltaTime, bool isGameReady, out bool wasPaused, out bool justResumed)
        {
            wasPaused = false;
            justResumed = false;

            // If not active, ensure state is fully cleared
            if (!_scaleActive)
            {
                if (_originalScale != null || _scaleTimer > 0 || _scaleRequestId > 0 || _originalCameraY != null)
                {
                    LogInfo("GameState: Clearing stale scale state");
                    _originalScale = null;
                    _scaleTimer = -1.0f;
                    _scaleRequestId = 0;
                    _scaleRequestID = null;
                    _scaleWasPaused = false;
                    _scaleResumeDelay = 0.0f;
                    _targetScale = 1.0f;
                    _isGiant = false;
                    _originalCameraY = null;
                    _cameraTransform = null;
                    _aimZoomOffset = 0.0f;
                    _aimForwardX = 0.0f;
                    _aimForwardZ = 0.0f;
                    _aimBlend = 0.0f;
                }
                return false;
            }

            // Handle pause/resume
            if (!isGameReady)
            {
                wasPaused = true;
                if (!_scaleWasPaused)
                {
                    _scaleWasPaused = true;
                    _scaleResumeDelay = SCALE_RESUME_DELAY_SECONDS;
                    LogInfo("GameState: Scale effect paused (game not ready)");
                }
                // Restore scale immediately when a cutscene/menu starts
                if (_originalScale != null)
                {
                    RestoreScale();
                }
                
                if (_originalCameraY != null && _cameraTransform != null)
                {
                    RestoreCameraHeight();
                }
                
                return true; // Still active, just paused
            }
            else
            {
                // Game is ready
                if (_scaleWasPaused)
                {
                    if (_scaleResumeDelay > 0.0f)
                    {
                        _scaleResumeDelay -= deltaTime;
                        return true; // Delay re-applying scale after cutscene
                    }

                    _scaleResumeDelay = 0.0f;
                    justResumed = true;
                    _scaleWasPaused = false;
                    LogInfo("GameState: Scale effect resumed (game ready)");
                }
            }

            // Pause scale while on ladders (restore normal size temporarily)
            if (IsPlayerOnLadder())
            {
                wasPaused = true;
                if (!_scalePausedForLadder)
                {
                    _scalePausedForLadder = true;
                }

                if (_originalScale != null)
                {
                    RestoreScale();
                }

                if (_originalCameraY != null && _cameraTransform != null)
                {
                    RestoreCameraHeight();
                }

                return true;
            }
            else if (_scalePausedForLadder)
            {
                _scalePausedForLadder = false;
                justResumed = true;
            }

            // Pause scale during certain motion states (restore normal size temporarily)
            if (_motionPauseActive)
            {
                wasPaused = true;
                if (!_scalePausedForMotion)
                {
                    _scalePausedForMotion = true;
                    LogInfo("GameState: Scale effect paused (motion override)");
                }

                if (_originalScale != null)
                {
                    RestoreScale();
                }

                if (_originalCameraY != null && _cameraTransform != null)
                {
                    RestoreCameraHeight();
                }

                return true;
            }
            else if (_scalePausedForMotion)
            {
                _scalePausedForMotion = false;
                justResumed = true;
                LogInfo("GameState: Scale effect resumed (motion override cleared)");
            }

            // If timer already expired, clear state immediately
            if (_scaleTimer <= 0)
            {
                // Restore original scale
                if (_originalScale != null)
                {
                    LogInfo($"GameState: Scale effect expired, restoring scale to {_originalScale}");
                    RestoreScale();
                }

                // Restore camera height if it was adjusted
                if (_originalCameraY != null && _cameraTransform != null)
                {
                    RestoreCameraHeight();
                }
                
                // Clear scale state completely
                _originalScale = null;
                _scaleTimer = -1.0f;
                _scaleActive = false;
                _scaleRequestId = 0;
                _scaleRequestID = null;
                _scaleWasPaused = false;
                _targetScale = 1.0f;
                _isGiant = false;
                _originalCameraY = null;
                _cameraTransform = null;
                _aimZoomOffset = 0.0f;
                _aimForwardX = 0.0f;
                _aimForwardZ = 0.0f;
                _aimBlend = 0.0f;
                return false; // Scale effect expired
            }

            // Count down timer only when game is ready
            _scaleTimer -= deltaTime;

            // Apply target scale continuously
            ApplyScale();
            
            // Camera height adjustment is now handled in BeginRendering callback
            // No need to call it here

            // Check if timer expired after counting down
            if (_scaleTimer <= 0)
            {
                // Restore original scale
                if (_originalScale != null)
                {
                    LogInfo($"GameState: Scale effect expired, restoring scale to {_originalScale}");
                    RestoreScale();
                }

                // Clear scale state completely
                _originalScale = null;
                _scaleTimer = -1.0f;
                _scaleActive = false;
                _scaleRequestId = 0;
                _scaleRequestID = null;
                _scaleWasPaused = false;
                _targetScale = 1.0f;
                _isGiant = false;
                _aimZoomOffset = 0.0f;
                _aimForwardX = 0.0f;
                _aimForwardZ = 0.0f;
                _aimBlend = 0.0f;
                return false; // Scale effect expired
            }

            return true; // Still active
        }

        /// <summary>
        /// Apply target scale to player and all children (weapons, hands, etc.)
        /// </summary>
        private void ApplyScale()
        {
            if (!_scaleActive)
                return;

            var transform = GetPlayerTransform();
            if (transform == null)
                return;

            try
            {
                var transformObj = transform as ManagedObject;
                if (transformObj == null)
                    return;

                // Scale the main player transform
                ApplyScaleToTransform(transformObj);

                // Scale all children transforms recursively (weapons, hands, etc.)
                // Use Child and Next pattern to iterate through all children
                ScaleAllChildrenRecursive(transformObj);
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error applying scale - {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively scale all children transforms using Child/Next pattern
        /// </summary>
        private void ScaleAllChildrenRecursive(ManagedObject transformObj)
        {
            try
            {
                // Get first child
                var child = transformObj.Call("get_Child");
                if (child == null)
                    return;

                var childObj = child as ManagedObject;
                if (childObj == null)
                    return;

                // Iterate through all siblings using Next
                var current = childObj;
                while (current != null)
                {
                    // Scale this child
                    ApplyScaleToTransform(current);

                    // Recursively scale this child's children
                    ScaleAllChildrenRecursive(current);

                    // Move to next sibling
                    var next = current.Call("get_Next");
                    if (next == null)
                        break;

                    current = next as ManagedObject;
                    if (current == null)
                        break;
                }
            }
            catch
            {
                // Some transforms might not have children - that's okay
            }
        }

        /// <summary>
        /// Apply scale to a single transform
        /// </summary>
        private void ApplyScaleToTransform(ManagedObject transformObj)
        {
            try
            {
                // Get current scale
                var scaleObj = transformObj.Call("get_LocalScale");
                if (scaleObj == null)
                {
                    return; // Some transforms might not have scale
                }

                // Try as ManagedObject first (most common case)
                var scaleManagedObj = scaleObj as ManagedObject;
                if (scaleManagedObj != null)
                {
                    // Try set_x (lowercase) first, then set_X (uppercase)
                    scaleManagedObj.Call("set_x", _targetScale);
                    scaleManagedObj.Call("set_y", _targetScale);
                    scaleManagedObj.Call("set_z", _targetScale);

                    // Apply the modified scale back to transform
                    transformObj.Call("set_LocalScale", scaleManagedObj);
                    return;
                }

                // Try as ValueType - extract the TypeDefinition and create a new vector
                var valueType = scaleObj as REFrameworkNET.ValueType;
                if (valueType != null)
                {
                    try
                    {
                        // Get the TypeDefinition from the ValueType
                        var typeField = valueType.GetType().GetField("m_type", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        TypeDefinition? scaleTypeDef = null;
                        
                        if (typeField != null)
                        {
                            scaleTypeDef = typeField.GetValue(valueType) as TypeDefinition;
                        }
                        
                        // If we couldn't get it from the field, try to find it by common names
                        if (scaleTypeDef == null)
                        {
                            string[] possibleTypes = { "via.Vector3", "via.Vector3f", "via.Vec3", "via.Vec3f", "via.vec3" };
                            
                            foreach (var typeName in possibleTypes)
                            {
                                scaleTypeDef = API.GetTDB()?.FindType(typeName);
                                if (scaleTypeDef != null)
                                {
                                    break;
                                }
                            }
                        }
                        
                        if (scaleTypeDef != null)
                        {
                            // Try to create using ValueType.New<via.vec3>() directly (like reference code)
                            try
                            {
                                var newVec3 = REFrameworkNET.ValueType.New<via.vec3>();
                                
                                // Set x, y, z directly (like the reference code does)
                                newVec3.x = _targetScale;
                                newVec3.y = _targetScale;
                                newVec3.z = _targetScale;
                                
                                // Apply the new scale to transform
                                transformObj.Call("set_LocalScale", newVec3);
                                return;
                            }
                            catch (Exception ex)
                            {
                                LogError($"GameState: Could not use ValueType.New<via.vec3> - {ex.Message}");
                            }
                            
                            // Fallback: Try to create using TypeDefinition directly
                            try
                            {
                                // Try to invoke a constructor-like method
                                object? newScale = null;
                                try
                                {
                                    newScale = scaleTypeDef.Invoke("New", new object[] { _targetScale, _targetScale, _targetScale });
                                }
                                catch { }
                                
                                if (newScale == null)
                                {
                                    try
                                    {
                                        // Try with lowercase
                                        newScale = scaleTypeDef.Invoke("new", new object[] { _targetScale, _targetScale, _targetScale });
                                    }
                                    catch { }
                                }
                                
                                if (newScale == null)
                                {
                                    try
                                    {
                                        // Try with _ctor
                                        newScale = scaleTypeDef.Invoke("_ctor", new object[] { _targetScale, _targetScale, _targetScale });
                                    }
                                    catch { }
                                }
                                
                                if (newScale != null)
                                {
                                    transformObj.Call("set_LocalScale", newScale);
                                    LogInfo("GameState: Created new scale vector using TypeDefinition.Invoke with various method names");
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"GameState: Could not create new scale using TypeDefinition.Invoke - {ex.Message}");
                            }
                            
                            LogError($"GameState: Could not create new scale vector for type: {scaleTypeDef.GetName()}");
                        }
                        else
                        {
                            LogError("GameState: Could not find scale type definition from ValueType");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"GameState: Error handling ValueType scale - {ex.Message}");
                    }
                    
                    LogError("GameState: All methods to modify ValueType scale failed");
                }
                else
                {
                    LogError($"GameState: Scale is neither ManagedObject nor ValueType in ApplyScale (type: {scaleObj.GetType().Name})");
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error applying scale - {ex.Message}");
            }
        }

        /// <summary>
        /// <summary>
        /// Check if player is currently aiming - tries multiple methods
        /// </summary>
        private bool IsPlayerAiming()
        {
            // Cache the result to avoid calling every frame (reduces crash risk and performance impact)
            // Only update every 0.1 seconds (10 times per second)
            var now = DateTime.Now;
            if (_hasCachedAimingResult && (now - _lastAimingCheck).TotalSeconds < 0.1)
            {
                return _cachedAimingResult;
            }
            
            bool result = false;
            bool? isHold = null;
            
            // Method 1: Try SurvivorCondition.get_IsHold (from FirstPerson.cpp)
            try
            {
                isHold = TryGetConditionHold();
                if (isHold == true)
                {
                    result = true;
                    LogAimingStateIfChanged("Condition", result);
                    goto cache_and_return;
                }
                
                if (isHold.HasValue)
                {
                    result = false;
                    LogAimingStateIfChanged("Condition", result);
                    goto cache_and_return;
                }
            }
            catch (Exception) { }
            
        cache_and_return:
            _cachedAimingResult = result;
            _hasCachedAimingResult = true;
            _lastAimingCheck = now;
            return result;
        }

        private void LogAimingStateIfChanged(string source, bool value)
        {
            if (value != _lastLoggedAimingState)
            {
                LogInfo($"GameState: IsPlayerAiming - {source} returned: {value}");
                _lastLoggedAimingState = value;
            }
        }

        private bool? TryGetConditionHold()
        {
            bool? isHold = null;

            try
            {
                if (_playerManager == null)
                {
                    UpdatePlayerManager();
                }

                var mgr = _playerManager as ManagedObject;
                if (mgr == null)
                    return null;

                // Get PlayerContext
                var ctx = mgr.Call("getPlayerContextRef");

                var ctxObj = ctx as ManagedObject;
                if (ctxObj == null)
                    return null;

                // Get Common (this is where IsHolding lives)
                var common = ctxObj.Call("get_Common");
                var commonObj = common as ManagedObject;
                if (commonObj == null)
                    return null;

                try
                {
                    var isHoldObj = commonObj.Call("get_IsHolding");
                    if (isHoldObj != null)
                    {
                        isHold = Convert.ToBoolean(isHoldObj);
                    }
                }
                catch (Exception) { }
            }
            catch (Exception) { }

            return isHold;
        }

        // PlayerUpdater status helper removed to avoid missing member spam

        public void ApplyCameraHeight()
        {
            if (!_scaleActive || _isGiant || _scalePausedForLadder || _scalePausedForMotion)
            {
                return;
            }
            
            // Only run camera adjustments when the game is ready and player is active
            if (!_isGameReady || _playerManager == null)
            {
                return;
            }
            
            // If _originalCameraY is null, try to get it now (camera might not have been available when effect started)
            if (_originalCameraY == null)
            {
                try
                {
                    var camera = GetPrimaryCamera();
                    if (camera != null)
                    {
                        var cameraObj = camera as ManagedObject;
                        if (cameraObj != null)
                        {
                            // Try to get transform directly from camera first
                            var cameraTypeDef = cameraObj.GetTypeDefinition();
                            ManagedObject? transformObj = null;
                            if (cameraTypeDef?.FindMethod("get_Transform") != null)
                            {
                                transformObj = cameraObj.Call("get_Transform") as ManagedObject;
                            }
                            if (transformObj == null)
                            {
                                // Try GameObject approach
                                object? gameObject = null;
                                if (cameraTypeDef?.FindField("ownerGameObject") != null)
                                {
                                    gameObject = cameraObj.GetField("ownerGameObject");
                                }
                                if (gameObject == null && cameraTypeDef?.FindMethod("get_GameObject") != null)
                                {
                                    gameObject = cameraObj.Call("get_GameObject");
                                }
                                if (gameObject != null)
                                {
                                    var gameObj = gameObject as ManagedObject;
                                    if (gameObj != null)
                                    {
                                        transformObj = gameObj.Call("get_Transform") as ManagedObject;
                                    }
                                }
                            }
                            
                            if (transformObj != null)
                            {
                                var positionObj = transformObj.Call("get_Position");
                                if (positionObj != null)
                                {
                                    var positionValueType = positionObj as REFrameworkNET.ValueType;
                                    if (positionValueType != null)
                                    {
                                        // Try to get Y using indexer (same method that works for FOV)
                                        var yObj = positionValueType.Call("get_Item", 1);
                                        if (yObj != null)
                                        {
                                            _originalCameraY = Convert.ToSingle(yObj);
                                            // Stored original camera Y in ApplyCameraHeight
                                        }
                                        else
                                        {
                                            // Try direct cast to via.vec3
                                            try
                                            {
                                                var vec3Pos = positionValueType as via.vec3;
                                                if (vec3Pos != null)
                                                {
                                                    _originalCameraY = vec3Pos.y;
                                                    // Stored original camera Y in ApplyCameraHeight (cast)
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogInfo($"GameState: Error getting camera Y in ApplyCameraHeight: {ex.Message}");
                }
                
                // If we still don't have it, return early
                if (_originalCameraY == null)
                {
                    // Log once if we're in tiny mode but don't have original camera Y
                    if (!_cameraHeightAdjustmentLogged)
                    {
                        LogWarning("GameState: ApplyCameraHeight called but _originalCameraY is null - camera may not be available");
                        _cameraHeightAdjustmentLogged = true;
                    }
                    return;
                }
            }
            
            // Reset the logged flag each frame so we can log once per frame
            _cameraHeightAdjustmentLogged = false;

            try
            {
                // Get camera fresh each frame (like we do for FOV)
                var camera = GetPrimaryCamera();
                if (camera == null)
                    return;

                var cameraObj = camera as ManagedObject;
                if (cameraObj == null)
                    return;

                // Prefer transform directly from camera to avoid missing member spam
                ManagedObject? transformObj = null;
                try
                {
                    var cameraTypeDef = cameraObj.GetTypeDefinition();
                    if (cameraTypeDef?.FindMethod("get_Transform") != null)
                    {
                        transformObj = cameraObj.Call("get_Transform") as ManagedObject;
                    }
                }
                catch { }
                
                // Fallback: only try GameObject path when game is ready and player exists
                if (transformObj == null && _isGameReady && _playerManager != null)
                {
                    try
                    {
                        object? gameObject = null;
                        try
                        {
                            var cameraTypeDef = cameraObj.GetTypeDefinition();
                            if (cameraTypeDef?.FindField("ownerGameObject") != null)
                            {
                                gameObject = cameraObj.GetField("ownerGameObject");
                            }
                        }
                        catch { }
                        
                        if (gameObject == null)
                        {
                            try
                            {
                                var cameraTypeDef = cameraObj.GetTypeDefinition();
                                if (cameraTypeDef?.FindMethod("get_GameObject") != null)
                                {
                                    gameObject = cameraObj.Call("get_GameObject");
                                }
                            }
                            catch { }
                        }
                        
                        if (gameObject != null)
                        {
                            var gameObj = gameObject as ManagedObject;
                            if (gameObj != null)
                            {
                                transformObj = gameObj.Call("get_Transform") as ManagedObject;
                            }
                        }
                    }
                    catch { }
                }
                
                if (transformObj == null)
                {
                    if (!_cameraTransformErrorLogged)
                    {
                        LogError("GameState: Could not get camera transform in ApplyCameraHeight");
                        _cameraTransformErrorLogged = true;
                    }
                    return;
                }

                // Get current camera position - try transform first, then joint (like FreeCam.cpp)
                var positionObj = transformObj.Call("get_Position");
                if (positionObj == null)
                {
                    // Fallback: Try getting position from joint (like FreeCam.cpp does)
                    try
                    {
                        var jointsObj = transformObj.Call("get_Joints");
                        if (jointsObj != null)
                        {
                            var jointsArray = jointsObj as System.Array;
                            if (jointsArray != null && jointsArray.Length > 0)
                            {
                                var joint = jointsArray.GetValue(0);
                                if (joint != null)
                                {
                                    var jointObj = joint as ManagedObject;
                                    if (jointObj != null)
                                    {
                                        positionObj = jointObj.Call("get_Position");
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                
                if (positionObj == null)
                    return;

                // Get current Y position to calculate relative adjustment
                float currentY = _originalCameraY.Value;
                try
                {
                    var positionValueType = positionObj as REFrameworkNET.ValueType;
                    if (positionValueType != null)
                    {
                        // Try indexer first (this worked for storing the original Y)
                        try
                                        {
                                            var yObj = positionValueType.Call("get_Item", 1) ?? positionValueType.Call("Get", 1);
                                            if (yObj != null)
                                            {
                                                currentY = Convert.ToSingle(yObj);
                                            }
                                        }
                                        catch { }
                                        
                                        // Fallback: Try direct cast to via.vec3
                                        if (currentY == _originalCameraY.Value)
                                        {
                                            try
                                            {
                                                var vec3Pos = positionValueType as via.vec3;
                                                if (vec3Pos != null)
                                                {
                                                    currentY = vec3Pos.y;
                                                }
                                            }
                                            catch { }
                                        }
                                        
                                        // Fallback: Try reflection
                                        if (currentY == _originalCameraY.Value)
                                        {
                                            var yProp = positionValueType.GetType().GetProperty("y", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                            if (yProp != null)
                                            {
                                                currentY = Convert.ToSingle(yProp.GetValue(positionValueType));
                                            }
                                        }
                    }
                    else
                    {
                        var posManaged = positionObj as ManagedObject;
                        if (posManaged != null)
                        {
                            var yObj = posManaged.Call("get_y") ?? posManaged.Call("get_Y");
                            if (yObj != null)
                            {
                                currentY = Convert.ToSingle(yObj);
                            }
                        }
                    }
                }
                catch { }

                // Lower camera by a proportional amount based on scale difference
                // If player is 0.33x scale (tiny), lower camera to improve visibility
                // Calculate adjustment: for tiny (0.33 scale), we want to lower by about 0.6-0.7 units normally
                float scaleDifference = 1.0f - _targetScale; // 0.67 for tiny (0.33 scale)
                
                // Base adjustment - slightly lower when not aiming
                float baseAdjustment = 0.4f; // Base adjustment in world units
                float proportionalAdjustment = scaleDifference * 0.7f; // Proportional adjustment
                float normalHeightAdjustment = baseAdjustment + proportionalAdjustment;
                
                // Check if player is aiming - if so, reduce adjustment significantly to prevent clipping into ground
                bool isAiming = IsPlayerAiming();
                const float maxAimAdjustment = 0.08f;
                float aimHeightAdjustment = Math.Min(normalHeightAdjustment * 0.2f, maxAimAdjustment);
                float targetAimBlend = isAiming ? 1.0f : 0.0f;
                const float aimBlendSpeed = 0.05f;
                _aimBlend = _aimBlend + (targetAimBlend - _aimBlend) * aimBlendSpeed;
                float heightAdjustment = (normalHeightAdjustment * (1.0f - _aimBlend)) + (aimHeightAdjustment * _aimBlend);
                
                // Calculate new Y position: apply relative offset to current position
                // This ensures the camera follows the player (e.g., up stairs) but is just lower
                // We subtract the height adjustment from the current Y to maintain the offset
                float newY = currentY - heightAdjustment;

                // Always create a new vec3 with adjusted Y (most reliable approach)
                try
                {
                    float currentX = 0.0f;
                    float currentZ = 0.0f;
                    float forwardX = 0.0f;
                    float forwardZ = 0.0f;
                    bool gotForward = false;
                    
                    // Try to get current X and Z from position using the same indexer method
                    var positionValueType = positionObj as REFrameworkNET.ValueType;
                    if (positionValueType != null)
                    {
                        // Use indexer to get x (index 0) and z (index 2), same as we did for y (index 1)
                        try
                        {
                            var xObj = positionValueType.Call("get_Item", 0) ?? positionValueType.Call("Get", 0);
                            if (xObj != null)
                            {
                                currentX = Convert.ToSingle(xObj);
                            }
                            
                            var zObj = positionValueType.Call("get_Item", 2) ?? positionValueType.Call("Get", 2);
                            if (zObj != null)
                            {
                                currentZ = Convert.ToSingle(zObj);
                            }
                        }
                        catch { }
                        
                        // Fallback: Try direct cast to via.vec3
                        if (currentX == 0.0f || currentZ == 0.0f)
                        {
                            try
                            {
                                var vec3Pos = positionValueType as via.vec3;
                                if (vec3Pos != null)
                                {
                                    if (currentX == 0.0f) currentX = vec3Pos.x;
                                    if (currentZ == 0.0f) currentZ = vec3Pos.z;
                                }
                            }
                            catch { }
                        }
                        
                        // Fallback: Try reflection
                        if (currentX == 0.0f || currentZ == 0.0f)
                        {
                            try
                            {
                                var xProp = positionValueType.GetType().GetProperty("x", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (xProp != null && currentX == 0.0f)
                                {
                                    currentX = Convert.ToSingle(xProp.GetValue(positionValueType));
                                }
                                
                                var zProp = positionValueType.GetType().GetProperty("z", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (zProp != null && currentZ == 0.0f)
                                {
                                    currentZ = Convert.ToSingle(zProp.GetValue(positionValueType));
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // Try as ManagedObject to get x, z (final fallback)
                    var posManaged = positionObj as ManagedObject;
                    if (posManaged != null && (currentX == 0.0f || currentZ == 0.0f))
                    {
                        var xObj = posManaged.Call("get_x") ?? posManaged.Call("get_X");
                        var zObj = posManaged.Call("get_z") ?? posManaged.Call("get_Z");
                        
                        if (xObj != null) currentX = Convert.ToSingle(xObj);
                        if (zObj != null) currentZ = Convert.ToSingle(zObj);
                    }
                    
                    // Derive forward direction toward player (camera -> player) to avoid get_Forward calls
                    float playerX = 0.0f;
                    float playerZ = 0.0f;
                    bool gotPlayerPos = false;
                    try
                    {
                        var playman = _playerManager ?? API.GetManagedSingleton("offline.PlayerManager");
                        if (playman != null)
                        {
                            var playmanObj = playman as ManagedObject;
                            if (playmanObj != null)
                            {
                                var player = playmanObj.Call("get_CurrentPlayer");
                                if (player != null)
                                {
                                    var playerObj = player as ManagedObject;
                                    if (playerObj != null)
                                    {
                                        var playerTransform = playerObj.Call("get_Transform");
                                        if (playerTransform != null)
                                        {
                                            var playerTransformObj = playerTransform as ManagedObject;
                                            if (playerTransformObj != null)
                                            {
                                                var playerPosObj = playerTransformObj.Call("get_Position");
                                                if (playerPosObj != null)
                                                {
                                                    var posValueType = playerPosObj as REFrameworkNET.ValueType;
                                                    if (posValueType != null)
                                                    {
                                                        var xObj = posValueType.Call("get_Item", 0) ?? posValueType.Call("Get", 0);
                                                        var zObj = posValueType.Call("get_Item", 2) ?? posValueType.Call("Get", 2);
                                                        if (xObj != null) playerX = Convert.ToSingle(xObj);
                                                        if (zObj != null) playerZ = Convert.ToSingle(zObj);
                                                        gotPlayerPos = true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    
                    if (gotPlayerPos)
                    {
                        forwardX = playerX - currentX;
                        forwardZ = playerZ - currentZ;
                        float forwardLength = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
                        if (forwardLength > 0.001f)
                        {
                            forwardX /= forwardLength;
                            forwardZ /= forwardLength;
                            gotForward = true;
                        }
                    }
                    
                    if (gotForward)
                    {
                        const float forwardLerpSpeed = 0.05f;
                        _aimForwardX = _aimForwardX + (forwardX - _aimForwardX) * forwardLerpSpeed;
                        _aimForwardZ = _aimForwardZ + (forwardZ - _aimForwardZ) * forwardLerpSpeed;
                        
                        float smoothLen = (float)Math.Sqrt(_aimForwardX * _aimForwardX + _aimForwardZ * _aimForwardZ);
                        if (smoothLen > 0.001f)
                        {
                            _aimForwardX /= smoothLen;
                            _aimForwardZ /= smoothLen;
                        }
                        else
                        {
                            _aimForwardX = forwardX;
                            _aimForwardZ = forwardZ;
                        }
                    }
                    
                    float targetZoomOffset = 0.0f;
                    if (gotForward)
                    {
                        float aimZoomBase = 0.18f + (scaleDifference * 0.15f);
                        targetZoomOffset = aimZoomBase * _aimBlend;
                    }
                    
                    // Smooth zoom transition to avoid jumpy camera when aiming
                    const float zoomLerpSpeed = 0.05f;
                    _aimZoomOffset = _aimZoomOffset + (targetZoomOffset - _aimZoomOffset) * zoomLerpSpeed;
                    
                    if (Math.Abs(_aimZoomOffset) > 0.0001f && gotForward)
                    {
                        currentX += _aimForwardX * _aimZoomOffset;
                        currentZ += _aimForwardZ * _aimZoomOffset;
                    }
                    
                    // Create new vec3 with adjusted Y (like FreeCam.cpp: transform->position = m_last_camera_matrix[3])
                    var newPos = REFrameworkNET.ValueType.New<via.vec3>();
                    newPos.x = currentX;
                    newPos.y = newY;
                    newPos.z = currentZ;
                    
                    // Set position on transform (like FreeCam.cpp line 330: transform->position = m_last_camera_matrix[3])
                    transformObj.Call("set_Position", newPos);
                    
                    // CRITICAL: Also set position on the joint (like VR mods do)
                    // The joint is what actually controls the camera rendering position
                    // From RE8VR.cpp line 711/719: sdk::set_joint_position(camera_joint, camera_pos)
                    try
                    {
                        var jointsObj = transformObj.Call("get_Joints");
                        if (jointsObj != null)
                        {
                            var jointsManaged = jointsObj as ManagedObject;
                            if (jointsManaged != null)
                            {
                                // Try to get the first joint (index 0)
                                object? joint = null;
                                try
                                {
                                    joint = jointsManaged.Call("get_Item", 0);
                                }
                                catch { }
                                
                                if (joint == null)
                                {
                                    try
                                    {
                                        joint = jointsManaged.Call("Get", 0);
                                    }
                                    catch { }
                                }
                                
                                if (joint != null)
                                {
                                    var jointObj = joint as ManagedObject;
                                    if (jointObj != null)
                                    {
                                        // Set position on joint (this is what actually moves the camera)
                                        // Try set_Position first (most common)
                                        try
                                        {
                                            jointObj.Call("set_Position", newPos);
                                        }
                                        catch
                                        {
                                            // Fallback: Try setPosition or setPositionOffset
                                            try
                                            {
                                                jointObj.Call("setPosition", newPos);
                                            }
                                            catch
                                            {
                                                try
                                                {
                                                    jointObj.Call("setPositionOffset", newPos);
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"GameState: Error setting joint position - {ex.Message}");
                    }
                    
                    // Verify the position was actually set by reading it back
                    try
                    {
                        var verifyPos = transformObj.Call("get_Position");
                        if (verifyPos != null)
                        {
                            var verifyPosValueType = verifyPos as REFrameworkNET.ValueType;
                            if (verifyPosValueType != null)
                            {
                                var verifyYObj = verifyPosValueType.Call("get_Item", 1) ?? verifyPosValueType.Call("Get", 1);
                                if (verifyYObj != null)
                                {
                                    float verifyY = Convert.ToSingle(verifyYObj);
                                    // Log only if the position didn't actually change (to avoid spam)
                                    if (Math.Abs(verifyY - newY) > 0.01f)
                                    {
                                        //LogInfo($"GameState: Camera position set to Y={newY} but read back as Y={verifyY} (game may be overriding it)");
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    
                    // Log to verify we're setting it (only once to avoid spam)
                    if (!_cameraHeightAdjustmentLogged)
                    {
                        // Removed logging to reduce spam - only log on state changes if needed
                        _cameraHeightAdjustmentLogged = true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"GameState: Error adjusting camera height - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error adjusting camera height - {ex.Message}");
            }
        }

        /// <summary>
        /// Restore original camera height
        /// </summary>
        private void RestoreCameraHeight()
        {
            if (_originalCameraY == null || _cameraTransform == null)
                return;

            try
            {
                var transformObj = _cameraTransform as ManagedObject;
                if (transformObj == null)
                    return;

                // Get current camera position
                var positionObj = transformObj.Call("get_Position");
                if (positionObj == null)
                    return;

                // Always create a new vec3 with original Y (most reliable approach)
                try
                {
                    float currentX = 0.0f;
                    float currentZ = 0.0f;
                    
                    // Try to get current X and Z from position
                    var positionValueType = positionObj as REFrameworkNET.ValueType;
                    if (positionValueType != null)
                    {
                        // Try to get x, z fields
                        try
                        {
                            var xField = positionValueType.GetType().GetField("x", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (xField == null)
                            {
                                xField = positionValueType.GetType().GetField("X", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            }
                            if (xField != null)
                            {
                                currentX = Convert.ToSingle(xField.GetValue(positionValueType));
                            }
                            
                            var zField = positionValueType.GetType().GetField("z", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (zField == null)
                            {
                                zField = positionValueType.GetType().GetField("Z", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            }
                            if (zField != null)
                            {
                                currentZ = Convert.ToSingle(zField.GetValue(positionValueType));
                            }
                        }
                        catch { }
                    }
                    
                    // Try as ManagedObject to get x, z
                    var posManaged = positionObj as ManagedObject;
                    if (posManaged != null && (currentX == 0.0f || currentZ == 0.0f))
                    {
                        var xObj = posManaged.Call("get_x") ?? posManaged.Call("get_X");
                        var zObj = posManaged.Call("get_z") ?? posManaged.Call("get_Z");
                        
                        if (xObj != null) currentX = Convert.ToSingle(xObj);
                        if (zObj != null) currentZ = Convert.ToSingle(zObj);
                    }
                    
                    // Create new vec3 with original Y
                    var newPos = REFrameworkNET.ValueType.New<via.vec3>();
                    newPos.x = currentX;
                    newPos.y = _originalCameraY.Value;
                    newPos.z = currentZ;
                    
                    transformObj.Call("set_Position", newPos);
                    LogInfo($"GameState: Restored camera Y position to {_originalCameraY.Value}");
                }
                catch (Exception ex)
                {
                    LogError($"GameState: Error restoring camera height - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error restoring camera height - {ex.Message}");
            }
        }

        /// <summary>
        /// Restore original scale to player
        /// </summary>
        private void RestoreScale()
        {
            if (_originalScale == null)
                return;

            var transform = GetPlayerTransform();
            if (transform == null)
                return;

            try
            {
                var transformObj = transform as ManagedObject;
                if (transformObj == null)
                    return;

                // Restore the main player transform
                RestoreScaleToTransform(transformObj);

                // Restore all children transforms recursively
                RestoreAllChildrenRecursive(transformObj);
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error restoring scale - {ex.Message}");
            }
        }

        /// <summary>
        /// Stop scale effect and restore original scale
        /// </summary>
        public void StopScale()
        {
            if (!_scaleActive && _originalScale == null)
            {
                return;
            }

            if (_originalScale != null)
            {
                LogInfo($"GameState: Stopping scale effect, restoring scale to {_originalScale}");
                RestoreScale();
            }

            if (_originalCameraY != null && _cameraTransform != null)
            {
                RestoreCameraHeight();
            }

            _originalScale = null;
            _scaleTimer = -1.0f;
            _scaleRequestId = 0;
            _scaleRequestID = null;
            _scaleWasPaused = false;
            _scaleResumeDelay = 0.0f;
            _scalePausedForLadder = false;
            _scalePausedForMotion = false;
            _targetScale = 1.0f;
            _isGiant = false;
            _scaleActive = false;
            _originalCameraY = null;
            _cameraTransform = null;
            _aimZoomOffset = 0.0f;
            _aimForwardX = 0.0f;
            _aimForwardZ = 0.0f;
            _aimBlend = 0.0f;
        }

        /// <summary>
        /// Recursively restore all children transforms using Child/Next pattern
        /// </summary>
        private void RestoreAllChildrenRecursive(ManagedObject transformObj)
        {
            try
            {
                // Get first child
                var child = transformObj.Call("get_Child");
                if (child == null)
                    return;

                var childObj = child as ManagedObject;
                if (childObj == null)
                    return;

                // Iterate through all siblings using Next
                var current = childObj;
                while (current != null)
                {
                    // Restore this child
                    RestoreScaleToTransform(current);

                    // Recursively restore this child's children
                    RestoreAllChildrenRecursive(current);

                    // Move to next sibling
                    var next = current.Call("get_Next");
                    if (next == null)
                        break;

                    current = next as ManagedObject;
                    if (current == null)
                        break;
                }
            }
            catch
            {
                // Some transforms might not have children - that's okay
            }
        }

        /// <summary>
        /// Restore scale to a single transform
        /// </summary>
        private void RestoreScaleToTransform(ManagedObject transformObj)
        {
            try
            {
                // Get current scale
                var scaleObj = transformObj.Call("get_LocalScale");
                if (scaleObj == null)
                {
                    return; // Some transforms might not have scale
                }

                // Try as ManagedObject first
                var scaleManagedObj = scaleObj as ManagedObject;
                if (scaleManagedObj != null)
                {
                    // Try set_x (lowercase) first, then set_X (uppercase)
                    if (_originalScale.HasValue)
                    {
                        scaleManagedObj.Call("set_x", _originalScale.Value);
                        scaleManagedObj.Call("set_y", _originalScale.Value);
                        scaleManagedObj.Call("set_z", _originalScale.Value);
                    }

                    // Apply the modified scale back to transform
                    transformObj.Call("set_LocalScale", scaleManagedObj);
                    return;
                }

                // Try as ValueType - use the same approach as ApplyScale
                var valueType = scaleObj as REFrameworkNET.ValueType;
                if (valueType != null)
                {
                    try
                    {
                        var newVec3 = REFrameworkNET.ValueType.New<via.vec3>();
                        
                        // Set x, y, z to original scale
                        if (_originalScale.HasValue)
                        {
                            newVec3.x = _originalScale.Value;
                            newVec3.y = _originalScale.Value;
                            newVec3.z = _originalScale.Value;
                        }
                        
                        // Apply the new scale to transform
                        transformObj.Call("set_LocalScale", newVec3);
                        return;
                    }
                    catch (Exception)
                    {
                        // Some transforms might not support this - that's okay
                    }
                }
            }
            catch (Exception)
            {
                // Some transforms might not support scaling - that's okay
            }
        }

        /// <summary>
        /// Get scale request ID for sending Stopped response
        /// </summary>
        public int GetScaleRequestId() => _scaleRequestId;
        
        /// <summary>
        /// Get scale request ID string for sending Stopped response
        /// </summary>
        public string? GetScaleRequestID() => _scaleRequestID;

        /// <summary>
        /// Extract the actual object from InvokeRet wrapper
        /// </summary>
        private static object? ExtractFromInvokeRet(object invokeRet)
        {
            try
            {
                if (invokeRet == null) return null;
                
                // Try direct casting first (sometimes InvokeRet can be cast directly)
                if (invokeRet is ManagedObject managedObj)
                {
                    return managedObj;
                }
                
                // InvokeRet has a Ptr field that contains the pointer to the actual object
                var invokeRetType = invokeRet.GetType();
                
                // Try to get the Ptr field
                var ptrField = invokeRetType.GetField("Ptr", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (ptrField != null)
                {
                    var ptrValue = ptrField.GetValue(invokeRet);
                    if (ptrValue != null)
                    {
                        // Convert pointer to uintptr_t
                        ulong ptrUInt64 = 0;
                        if (ptrValue is System.UIntPtr uintPtr)
                        {
                            ptrUInt64 = uintPtr.ToUInt64();
                        }
                        else if (ptrValue is System.IntPtr intPtr)
                        {
                            ptrUInt64 = (ulong)intPtr.ToInt64();
                        }
                        else
                        {
                            // Try to convert to UInt64
                            ptrUInt64 = Convert.ToUInt64(ptrValue);
                        }
                        
                        // Check if pointer is valid (not null)
                        if (ptrUInt64 != 0)
                        {
                            // Use ManagedObject.ToManagedObject to convert pointer to ManagedObject
                            var managedObject = ManagedObject.ToManagedObject(ptrUInt64);
                            if (managedObject != null)
                            {
                                return managedObject;
                            }
                            
                            // If it's not a managed object, try creating a NativeObject
                            var cameraTypeDef = API.GetTDB()?.FindType("via.Camera");
                            if (cameraTypeDef != null)
                            {
                                var nativeObject = new NativeObject(ptrUInt64, cameraTypeDef);
                                return nativeObject;
                            }
                        }
                    }
                }
                
                // If all else fails, try to cast the invokeRet itself
                return invokeRet as ManagedObject;
            }
            catch (Exception ex)
            {
                Logger.LogError($"GameState: Error extracting from InvokeRet - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Set scale request ID for tracking
        /// </summary>
        public void SetScaleRequestId(int requestId, string? requestID)
        {
            _scaleRequestId = requestId;
            _scaleRequestID = requestID;
        }
        private ManagedObject GetPlayerMotion()
        {
            var cm = API.GetManagedSingleton("app.CharacterManager") as ManagedObject;
            if (cm == null)
                return null;

            var ctx = cm.Call("getPlayerContextRef") as ManagedObject;
            if (ctx == null)
                return null;

            var updater = ctx.Call("get_Updater") as ManagedObject;
            if (updater == null)
                return null;

            var go = updater.Call("get_GameObject") as ManagedObject;
            if (go == null)
                return null;

            var tdb = API.GetTDB();
            if (tdb == null)
                return null;

            var motionType = tdb.FindType("via.motion.Motion");
            if (motionType == null)
                return null;

            var motion = go.Call("getComponent", motionType) as ManagedObject;
            return motion;
        }
        /// <summary>
        /// Start player speed effect - stores original speed and sets target speed
        /// </summary>
        public bool StartSpeed(float targetSpeed, int durationMs, bool isFast)
        {
            if (_speedActive)
            {
                LogError("GameState: Speed effect already active");
                return false;
            }

            try
            {
                var motion = GetPlayerMotion();
                if (motion == null)
                {
                    LogError("GameState: Cannot get Motion");
                    return false;
                }

                var currentSpeedObj = motion.Call("get_SecondaryPlaySpeed");
                if (currentSpeedObj != null)
                    _originalSpeed = Convert.ToSingle(currentSpeedObj);
                else
                    _originalSpeed = 1.0f;

                _targetSpeed = targetSpeed;
                _speedTimer = durationMs / 1000.0f;
                _speedActive = true;
                _isFast = isFast;

                ApplySpeed();

                LogInfo($"GameState: Speed effect started - Original: {_originalSpeed}, Target: {targetSpeed}, Duration: {_speedTimer}s, IsFast: {isFast}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error starting speed effect - {ex.Message}");
                _originalSpeed = null;
                _speedTimer = -1.0f;
                _speedActive = false;
                return false;
            }
        }

        /// <summary>
        /// Update speed effect - maintains speed and counts down timer
        /// Returns true if speed is still active, false if it expired
        /// </summary>
        public bool UpdateSpeed(float deltaTime, bool isGameReady, out bool wasPaused, out bool justResumed)
        {
            wasPaused = false;
            justResumed = false;

            // If not active, ensure state is fully cleared
            if (!_speedActive)
            {
                if (_originalSpeed != null || _speedTimer > 0 || _speedRequestId > 0)
                {
                    LogInfo("GameState: Clearing stale speed state");
                    _originalSpeed = null;
                    _speedTimer = -1.0f;
                    _speedRequestId = 0;
                    _speedRequestID = null;
                    _speedWasPaused = false;
                }
                return false;
            }

            // If active but missing original speed, clear state
            if (_originalSpeed == null)
            {
                LogInfo("GameState: Speed active but missing original speed, clearing state");
                _speedTimer = -1.0f;
                _speedActive = false;
                _speedRequestId = 0;
                _speedRequestID = null;
                _speedWasPaused = false;
                return false;
            }

            // Handle pause/resume
            if (!isGameReady)
            {
                wasPaused = true;
                if (!_speedWasPaused)
                {
                    _speedWasPaused = true;
                    LogInfo("GameState: Speed paused (game not ready)");
                }
                // Don't count down timer when paused, but maintain speed
                ApplySpeed();
                return true;
            }
            else
            {
                // Game is ready - check if we just resumed
                if (_speedWasPaused)
                {
                    justResumed = true;
                    _speedWasPaused = false;
                    LogInfo("GameState: Speed resumed");
                }
            }

            if (_motionPauseActive)
            {
                wasPaused = true;
                if (!_speedPausedForMotion)
                {
                    _speedPausedForMotion = true;
                    LogInfo("GameState: Speed paused (motion override)");
                }

                ApplySpeed();
                return true;
            }
            else if (_speedPausedForMotion)
            {
                _speedPausedForMotion = false;
                justResumed = true;
                LogInfo("GameState: Speed resumed (motion override cleared)");
            }

            // Count down timer only when game is ready
            _speedTimer -= deltaTime;

            // Apply speed every frame
            ApplySpeed();

            // Check if timer expired after counting down
            if (_speedTimer <= 0)
            {
                // Restore original speed when effect expires
                LogInfo($"GameState: Speed effect expired, restoring speed to {_originalSpeed.Value}");
                RestoreSpeed();

                // Clear speed state completely
                _originalSpeed = null;
                _speedTimer = -1.0f;
                _speedActive = false;
                _speedRequestId = 0;
                _speedRequestID = null;
                _speedWasPaused = false;
                _speedPausedForMotion = false;
                return false; // Speed expired
            }

            return true; // Still active
        }

        /// <summary>
        /// Stop speed effect and restore original speed
        /// </summary>
        public void StopSpeed()
        {
            if (!_speedActive || _originalSpeed == null)
            {
                return;
            }

            RestoreSpeed();

            _originalSpeed = null;
            _speedTimer = -1.0f;
            _speedActive = false;
            _speedRequestId = 0;
            _speedRequestID = null;
            _speedWasPaused = false;
            _speedPausedForMotion = false;
        }

        /// <summary>
        /// Apply speed to player motion
        /// Temporarily restores speed to 1.0 during reload to prevent reload animation issues
        /// </summary>
        private void ApplySpeed()
        {
            if (!_speedActive)
                return;

            try
            {
                var motion = GetPlayerMotion();
                if (motion == null)
                    return;

                bool isReloading = false;

                try
                {
                    var cm = API.GetManagedSingleton("app.CharacterManager") as ManagedObject;
                    var ctx = cm?.Call("getPlayerContextRef") as ManagedObject;
                    var reloadObj = ctx?.Call("get_IsReloading");

                    if (reloadObj != null)
                        isReloading = Convert.ToBoolean(reloadObj);
                }
                catch { }

                float speedToApply = (isReloading || _motionPauseActive) ? 1.0f : _targetSpeed;

                motion.Call("set_SecondaryPlaySpeed", speedToApply);
            }
            catch (Exception ex)
            {
                LogError($"ApplySpeed error: {ex.Message}");
            }
        }

        private bool IsPlayerOnLadder()
        {
            try
            {
                var playman = API.GetManagedSingleton("offline.PlayerManager");
                if (playman == null)
                    return false;

                var playmanObj = playman as ManagedObject;
                if (playmanObj == null)
                    return false;

                var condition = playmanObj.Call("get_CurrentPlayerCondition");
                if (condition == null)
                    return false;

                var conditionObj = condition as ManagedObject;
                if (conditionObj == null)
                    return false;

                var conditionType = conditionObj.GetTypeDefinition();
                if (conditionType != null)
                {
                    string[] ladderMethods =
                    {
                        "get_IsLadder",
                        "get_IsOnLadder",
                        "get_IsLadderAction",
                        "get_IsClimb"
                    };

                    foreach (var methodName in ladderMethods)
                    {
                        if (conditionType.FindMethod(methodName) != null)
                        {
                            var isLadderObj = conditionObj.Call(methodName);
                            if (isLadderObj != null && Convert.ToBoolean(isLadderObj))
                                return true;
                        }
                    }

                    if (conditionType.FindMethod("get_MotionFsm2") != null)
                    {
                        var motionFsm2Obj = conditionObj.Call("get_MotionFsm2") as ManagedObject;
                        if (motionFsm2Obj != null && MotionFsmHasLadder(motionFsm2Obj))
                            return true;
                    }
                }

                var motion = conditionObj.GetField("<Motion>k__BackingField");
                if (motion != null)
                {
                    var motionObj = motion as ManagedObject;
                    if (motionObj != null)
                    {
                        var gameObject = motionObj.Call("get_GameObject");
                        if (gameObject != null)
                        {
                            var gameObj = gameObject as ManagedObject;
                            if (gameObj != null)
                            {
                                var tdb = API.GetTDB();
                                if (tdb != null)
                                {
                                    var motionFsm2Type = tdb.FindType("via.motion.MotionFsm2");
                                    if (motionFsm2Type != null)
                                    {
                                        var motionFsm = gameObj.Call("getComponent", motionFsm2Type);
                                        if (motionFsm is ManagedObject motionFsmObj &&
                                            MotionFsmHasLadder(motionFsmObj))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                var playmanType = playmanObj.GetTypeDefinition();
                if (playmanType?.FindMethod("get_IsJacked") != null)
                {
                    var isJackedObj = playmanObj.Call("get_IsJacked");
                    if (isJackedObj != null && Convert.ToBoolean(isJackedObj))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool MotionFsmHasLadder(ManagedObject motionFsmObj)
        {
            try
            {
                var treeCountObj = motionFsmObj.Call("getTreeCount");
                if (treeCountObj == null)
                    return false;

                uint treeCount = Convert.ToUInt32(treeCountObj);
                for (uint i = 0; i < treeCount; i++)
                {
                    var nodeNameObj = motionFsmObj.Call("getCurrentNodeName", i);
                    if (nodeNameObj == null)
                        continue;

                    string nodeName = nodeNameObj.ToString() ?? "";
                    if (nodeName.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        nodeName.IndexOf("climb", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }


        private bool TryGetMotionNodes(out List<string> nodeNames, out string source)
        {
            nodeNames = new List<string>();
            source = "";

            try
            {
                if (!(_currentPlayerCondition is ManagedObject conditionObj))
                    return false;

                var conditionType = conditionObj.GetTypeDefinition();
                if (conditionType?.FindMethod("get_MotionFsm2") != null)
                {
                    var motionFsm2Obj = conditionObj.Call("get_MotionFsm2") as ManagedObject;
                    if (motionFsm2Obj != null && TryReadMotionNodes(motionFsm2Obj, nodeNames))
                    {
                        source = "PlayerCondition.MotionFsm2";
                        return true;
                    }
                }

                var motion = conditionObj.GetField("<Motion>k__BackingField") as ManagedObject;
                if (motion == null)
                    return false;

                var gameObject = motion.Call("get_GameObject") as ManagedObject;
                if (gameObject == null)
                    return false;

                var tdb = API.GetTDB();
                var motionFsm2Type = tdb?.FindType("via.motion.MotionFsm2");
                if (motionFsm2Type == null)
                    return false;

                var motionFsm = gameObject.Call("getComponent", motionFsm2Type) as ManagedObject;
                if (motionFsm == null)
                    return false;

                if (TryReadMotionNodes(motionFsm, nodeNames))
                {
                    source = "GameObject.MotionFsm2";
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool TryReadMotionNodes(ManagedObject motionFsmObj, List<string> nodeNames)
        {
            try
            {
                var treeCountObj = motionFsmObj.Call("getTreeCount");
                if (treeCountObj == null)
                    return false;

                uint treeCount = Convert.ToUInt32(treeCountObj);
                for (uint i = 0; i < treeCount; i++)
                {
                    var nodeNameObj = motionFsmObj.Call("getCurrentNodeName", i);
                    if (nodeNameObj == null)
                        continue;

                    string nodeName = nodeNameObj.ToString() ?? "";
                    if (string.IsNullOrEmpty(nodeName))
                        continue;

                    nodeNames.Add(nodeName);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restore original speed
        /// </summary>
        private void RestoreSpeed()
        {
            if (_originalSpeed == null)
                return;

            try
            {
                var motion = GetPlayerMotion();
                if (motion == null)
                    return;

                motion.Call("set_SecondaryPlaySpeed", _originalSpeed.Value);

                LogInfo($"GameState: Speed restored to {_originalSpeed.Value}");
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error restoring speed - {ex.Message}");
            }
        }

        /// <summary>
        /// Get speed request ID for tracking
        /// </summary>
        public int GetSpeedRequestId() => _speedRequestId;
        public string? GetSpeedRequestID() => _speedRequestID;

        /// <summary>
        /// Set speed request ID for tracking
        /// </summary>
        public void SetSpeedRequestId(int requestId, string? requestID)
        {
            _speedRequestId = requestId;
            _speedRequestID = requestID;
        }

        private readonly System.Random _random = new System.Random();

        /// <summary>
        /// Get player inventory from current player condition
        /// </summary>
        public ManagedObject? GetInventory()
        {
            try
            {
                var itemManager = API.GetManagedSingleton("app.InventoryManager") as ManagedObject;
                if (itemManager == null)
                    return null;

                var dict = itemManager.GetField("_Inventories") as ManagedObject;
                if (dict == null)
                    return null;

                var values = dict.Call("get_Values") as ManagedObject;
                if (values == null)
                    return null;

                int count = Convert.ToInt32(values.Call("get_Count"));
                if (count == 0)
                    return null;

                var inventory = values.Call("get_Item", 0) as ManagedObject;
                return inventory;
            }
            catch (Exception ex)
            {
                LogError($"GetInventory failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get equipped weapon from inventory
        /// Returns the main slot index, or -1 if no weapon is equipped
        /// </summary>
        public int GetEquippedWeaponSlotIndex()
        {
            try
            {
                var inventory = GetInventory();
                if (inventory == null)
                    return -1;

                // Get the main weapon slot
                var mainSlot = inventory.Call("get_MainSlot");
                if (mainSlot == null)
                    return -1;

                var mainSlotObj = mainSlot as ManagedObject;
                if (mainSlotObj == null)
                    return -1;

                var slotIndexObj = mainSlotObj.Call("get_Index");
                if (slotIndexObj == null)
                    return -1;

                return Convert.ToInt32(slotIndexObj);
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error getting equipped weapon slot - {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Check if player has a weapon equipped
        /// </summary>
        public bool HasWeaponEquipped()
        {
            return GetEquippedWeaponSlotIndex() >= 0;
        }

        /// <summary>
        /// Find empty slot(s) in inventory
        /// Returns the first empty slot index, or -1 if no space
        /// For big weapons (2 slots), checks if two consecutive slots are available
        /// </summary>
        public int FindEmptySlot(bool requiresTwoSlots = false)
        {
            try
            {
                var inventory = GetInventory();
                if (inventory == null)
                    return -1;

                var numSlotsObj = inventory.GetField("_UnlockSlotSize");
                if (numSlotsObj == null)
                    return -1;

                int numSlots = Convert.ToInt32(numSlotsObj);
                var slots = inventory.GetField("_Slots");
                if (slots == null)
                    return -1;

                // Get slots as array/collection
                var slotsArray = slots as System.Array;
                if (slotsArray == null)
                {
                    // Try as ManagedObject with indexer
                    var slotsManaged = slots as ManagedObject;
                    if (slotsManaged == null)
                        return -1;

                    var values = slotsManaged.Call("get_Values") as ManagedObject;
                    int count = Convert.ToInt32(values.Call("get_Count"));
                    // Iterate through slots
                    for (int i = 0; i < count; i++)
                    {
                        var slot = slotsManaged.Call("get_Item", i) ?? slotsManaged.Call("get_Item", (uint)i);
                        if (slot == null)
                            continue;

                        var slotObj = slot as ManagedObject;
                        if (slotObj == null)
                            continue;

                        var isBlankObj = slotObj.Call("get_IsEmpty");
                        if (isBlankObj == null)
                            continue;

                        bool isBlank = Convert.ToBoolean(isBlankObj);
                        if (isBlank)
                        {
                            // For big weapons, check if next slot is also empty
                            if (requiresTwoSlots)
                            {
                                if (i + 1 >= numSlots)
                                    continue; // Can't fit 2 slots

                                var nextSlot = slotsManaged.Call("get_Item", i + 1) ?? slotsManaged.Call("get_Item", (uint)(i + 1));
                                if (nextSlot == null)
                                    continue;

                                var nextSlotObj = nextSlot as ManagedObject;
                                if (nextSlotObj == null)
                                    continue;

                                var nextIsBlankObj = nextSlotObj.Call("get_IsEmpty");
                                if (nextIsBlankObj == null)
                                    continue;

                                bool nextIsBlank = Convert.ToBoolean(nextIsBlankObj);
                                if (!nextIsBlank)
                                    continue; // Next slot is not empty
                            }

                            return i;
                        }
                    }
                }
                else
                {
                    // Use array indexing
                    for (int i = 0; i < numSlots && i < slotsArray.Length; i++)
                    {
                        var slot = slotsArray.GetValue(i);
                        if (slot == null)
                            continue;

                        var slotObj = slot as ManagedObject;
                        if (slotObj == null)
                            continue;

                        var isBlankObj = slotObj.Call("get_IsEmpty");
                        if (isBlankObj == null)
                            continue;

                        bool isBlank = Convert.ToBoolean(isBlankObj);
                        if (isBlank)
                        {
                            // For big weapons, check if next slot is also empty
                            if (requiresTwoSlots)
                            {
                                if (i + 1 >= numSlots || i + 1 >= slotsArray.Length)
                                    continue; // Can't fit 2 slots

                                var nextSlot = slotsArray.GetValue(i + 1);
                                if (nextSlot == null)
                                    continue;

                                var nextSlotObj = nextSlot as ManagedObject;
                                if (nextSlotObj == null)
                                    continue;

                                var nextIsBlankObj = nextSlotObj.Call("get_IsEmpty");
                                if (nextIsBlankObj == null)
                                    continue;

                                bool nextIsBlank = Convert.ToBoolean(nextIsBlankObj);
                                if (!nextIsBlank)
                                    continue; // Next slot is not empty
                            }

                            return i;
                        }
                    }
                }

                return -1; // No empty slot found
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error finding empty slot - {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Add healing item to inventory
        /// </summary>
        public bool AddHealingItem(string itemId)
        {
            try
            {
                var inventory = GetInventory();
                if (inventory == null)
                    return false;

                int slotIndex = FindEmptySlot(false);
                if (slotIndex < 0)
                    return false; // No space

                var slots = inventory.GetField("_Slots");
                if (slots == null)
                    return false;

                var slot = GetSlotAtIndex(slots, slotIndex);
                if (slot == null)
                    return false;

                slot.Call("set_ItemID", itemId);
                LogInfo($"GameState: Added healing item {itemId} to slot {slotIndex}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error adding healing item - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add ammo item to inventory
        /// </summary>
        public bool AddAmmoItem(int itemId, int amount)
        {
            try
            {
                var inventory = GetInventory();
                if (inventory == null)
                    return false;

                var numSlotsObj = inventory.GetField("_CurrentSlotSize");
                if (numSlotsObj == null)
                    return false;

                int numSlots = Convert.ToInt32(numSlotsObj);
                var slots = inventory.GetField("_Slots");
                if (slots == null)
                    return false;

                // First, check if we already have this ammo type and can add to it
                for (int i = 0; i < numSlots; i++)
                {
                    var slot = GetSlotAtIndex(slots, i);
                    if (slot == null)
                        continue;

                    var isBlankObj = slot.Call("get_IsBlank");
                    if (isBlankObj == null)
                        continue;

                    bool isBlank = Convert.ToBoolean(isBlankObj);
                    if (isBlank)
                        continue;

                    var existingItemIdObj = slot.Call("get_ItemID");
                    if (existingItemIdObj == null)
                        continue;

                    int existingItemId = Convert.ToInt32(existingItemIdObj);
                    if (existingItemId == itemId)
                    {
                        // Found existing ammo, try to add to it
                        var currentObj = slot.Call("get_Number");
                        var maxObj = slot.Call("get_MaxNumber");
                        if (currentObj != null && maxObj != null)
                        {
                            int current = Convert.ToInt32(currentObj);
                            int max = Convert.ToInt32(maxObj);
                            LogInfo($"GameState: Found existing ammo stack at slot {i}: {current}/{max}, trying to add {amount}");
                            if (max - current >= amount)
                            {
                                slot.Call("set_Number", current + amount);

                                // Verify the number was set
                                var verifyNumObj = slot.Call("get_Number");
                                int verifyNum = verifyNumObj != null ? Convert.ToInt32(verifyNumObj) : -1;

                                LogInfo($"GameState: Added {amount} ammo to existing stack (now {verifyNum}/{max})");

                                if (verifyNum != current + amount)
                                {
                                    LogError($"GameState: Failed to update ammo count! Expected {current + amount}, got {verifyNum}");
                                    return false;
                                }

                                return true;
                            }
                            else
                            {
                                LogInfo($"GameState: Existing stack at slot {i} is full ({current}/{max}), cannot add {amount}");
                            }
                        }
                    }
                }

                // No existing stack found or can't add to it, create new slot
                int emptySlotIndex = FindEmptySlot(false);
                if (emptySlotIndex < 0)
                {
                    LogError($"GameState: No empty slot found for ammo item {itemId}");
                    return false; // No space
                }

                var emptySlot = GetSlotAtIndex(slots, emptySlotIndex);
                if (emptySlot == null)
                {
                    LogError($"GameState: Could not get slot at index {emptySlotIndex}");
                    return false;
                }

                // Verify slot is blank before setting
                var isBlankBeforeObj = emptySlot.Call("get_IsBlank");
                bool isBlankBefore = isBlankBeforeObj != null && Convert.ToBoolean(isBlankBeforeObj);
                LogInfo($"GameState: Slot {emptySlotIndex} isBlank before: {isBlankBefore}");

                emptySlot.Call("set_ItemID", itemId);
                emptySlot.Call("set_Number", amount);

                // Verify the item was set correctly
                var verifyItemIdObj = emptySlot.Call("get_ItemID");
                var verifyNumberObj = emptySlot.Call("get_Number");
                var verifyIsBlankObj = emptySlot.Call("get_IsBlank");

                int verifyItemId = verifyItemIdObj != null ? Convert.ToInt32(verifyItemIdObj) : -1;
                int verifyNumber = verifyNumberObj != null ? Convert.ToInt32(verifyNumberObj) : -1;
                bool verifyIsBlank = verifyIsBlankObj != null && Convert.ToBoolean(verifyIsBlankObj);

                LogInfo($"GameState: Added new ammo item {itemId} with amount {amount} to slot {emptySlotIndex}");
                LogInfo($"GameState: Verification - ItemID: {verifyItemId} (expected {itemId}), Number: {verifyNumber} (expected {amount}), IsBlank: {verifyIsBlank}");

                if (verifyItemId != itemId || verifyNumber != amount)
                {
                    LogError($"GameState: Ammo item was not set correctly! Expected ItemID={itemId}, Number={amount}, but got ItemID={verifyItemId}, Number={verifyNumber}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error adding ammo item - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add weapon to inventory using InventoryManager
        /// </summary>
        public bool AddWeapon(string weaponKey, int weaponId, bool isBig, int ammoAmount)
        {
            try
            {
                // Use InventoryManager like LUA does
                var itemMan = API.GetManagedSingleton("offline.gamemastering.InventoryManager");
                if (itemMan == null)
                {
                    LogError("GameState: InventoryManager not found");
                    return false;
                }

                var itemManObj = itemMan as ManagedObject;
                if (itemManObj == null)
                    return false;

                // Check if weapon already exists
                var inventory = GetInventory();
                if (inventory != null)
                {
                    var numSlotsObj = inventory.GetField("_CurrentSlotSize");
                    if (numSlotsObj != null)
                    {
                        int numSlots = Convert.ToInt32(numSlotsObj);
                        var slots = inventory.GetField("_Slots");
                        if (slots != null)
                        {
                            for (int i = 0; i < numSlots; i++)
                            {
                                var slot = GetSlotAtIndex(slots, i);
                                if (slot == null)
                                    continue;

                                var isBlankObj = slot.Call("get_IsBlank");
                                if (isBlankObj == null)
                                    continue;

                                bool isBlank = Convert.ToBoolean(isBlankObj);
                                if (isBlank)
                                    continue;

                                var weaponTypeObj = slot.Call("get_WeaponType");
                                if (weaponTypeObj != null)
                                {
                                    int existingWeaponId = Convert.ToInt32(weaponTypeObj);
                                    if (existingWeaponId == weaponId)
                                    {
                                        // Weapon already exists, don't add another
                                        LogInfo($"GameState: Weapon {weaponId} already exists in inventory");
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }

                // Check if we have space (for big weapons, need 2 slots)
                int emptySlot = FindEmptySlot(isBig);
                if (emptySlot < 0)
                {
                    LogError($"GameState: No space for weapon (big={isBig})");
                    return false;
                }

                int bulletItemId = GetDefaultAmmoItemIdForWeapon(weaponKey) ?? 1;
                int bulletId = ResolveBulletId(itemManObj, bulletItemId);

                // Use addAndEquipMainWeapon
                // For grenades/flash, the LUA uses different parameters: addAndEquipMainWeapon(itemid, 1, bullet)
                // For others: addAndEquipMainWeapon(itemid, ammo_count, bullet)
                if (weaponKey == "grenade" || weaponKey == "flash")
                {
                    itemManObj.Call("addAndEquipMainWeapon", weaponId, 1, bulletId);
                }
                else
                {
                    itemManObj.Call("addAndEquipMainWeapon", weaponId, ammoAmount, bulletId);
                }

                TrySetWeaponBulletType(weaponId, bulletItemId, bulletId);
                DumpWeaponSlotInfo(weaponId);
                
                LogInfo($"GameState: Added weapon {weaponId} (big={isBig}, ammo={ammoAmount})");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error adding weapon - {ex.Message}");
                return false;
            }
        }

        private void TrySetWeaponBulletType(int weaponId, int bulletItemId, int bulletId)
        {
            try
            {
                var inventory = GetInventory();
                if (inventory == null)
                    return;

                var numSlotsObj = inventory.GetField("_CurrentSlotSize");
                if (numSlotsObj == null)
                    return;

                int numSlots = Convert.ToInt32(numSlotsObj);
                var slots = inventory.GetField("_Slots");
                if (slots == null)
                    return;

                for (int i = 0; i < numSlots; i++)
                {
                    var slot = GetSlotAtIndex(slots, i);
                    if (slot == null)
                        continue;

                    var weaponTypeObj = slot.Call("get_WeaponType");
                    if (weaponTypeObj == null)
                        continue;

                    int slotWeaponId = Convert.ToInt32(weaponTypeObj);
                    if (slotWeaponId != weaponId)
                        continue;

                    var slotType = slot.GetTypeDefinition();
                    if (slotType == null)
                        return;

                    string[] methodNames =
                    {
                        "set_BulletItemID",
                        "set_BulletItemId",
                        "set_BulletID",
                        "set_BulletId",
                        "set_BulletType",
                        "set_Bullet"
                    };

                    foreach (var methodName in methodNames)
                    {
                        if (slotType.FindMethod(methodName) == null)
                            continue;

                        if (methodName.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            slot.Call(methodName, bulletItemId);
                            continue;
                        }

                        // Try bullet ID first, then fall back to item ID if it doesn't stick.
                        slot.Call(methodName, bulletId);
                        var bulletIdObj = slot.Call("get_BulletID") ?? slot.Call("get_BulletId") ?? slot.Call("get_BulletType");
                        if (bulletIdObj != null)
                        {
                            int currentBulletId = Convert.ToInt32(bulletIdObj);
                            if (currentBulletId != bulletId && bulletItemId != bulletId)
                            {
                                slot.Call(methodName, bulletItemId);
                            }
                        }
                    }

                    return;
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void DumpWeaponSlotInfo(int weaponId)
        {
            try
            {
                var inventory = GetInventory();
                if (inventory == null)
                    return;

                var numSlotsObj = inventory.GetField("_CurrentSlotSize");
                if (numSlotsObj == null)
                    return;

                int numSlots = Convert.ToInt32(numSlotsObj);
                var slots = inventory.GetField("_Slots");
                if (slots == null)
                    return;

                for (int i = 0; i < numSlots; i++)
                {
                    var slot = GetSlotAtIndex(slots, i);
                    if (slot == null)
                        continue;

                    var weaponTypeObj = slot.Call("get_WeaponType");
                    if (weaponTypeObj == null)
                        continue;

                    int slotWeaponId = Convert.ToInt32(weaponTypeObj);
                    if (slotWeaponId != weaponId)
                        continue;

                    var slotType = slot.GetTypeDefinition();
                    if (slotType == null)
                        return;

                    var methodNames = new List<string>();
                    foreach (var method in slotType.Methods)
                    {
                        var name = method.Name;
                        if (name.IndexOf("Bullet", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Ammo", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            methodNames.Add(name);
                        }
                    }

                    LogInfo($"GameState: Weapon slot {i} for weapon {weaponId} has bullet/ammo methods: {string.Join(", ", methodNames)}");

                    var bulletIdObj = slot.Call("get_BulletID") ?? slot.Call("get_BulletId") ?? slot.Call("get_BulletType");
                    if (bulletIdObj != null)
                    {
                        LogInfo($"GameState: Slot {i} BulletID={Convert.ToInt32(bulletIdObj)}");
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                LogInfo($"GameState: DumpWeaponSlotInfo failed - {ex.Message}");
            }
        }

        private int? GetDefaultAmmoItemIdForWeapon(string weaponKey)
        {
            switch (weaponKey)
            {
                case "g19":
                case "burst":
                case "g18":
                case "edge":
                case "mup":
                    //return TryGetAmmoItemId("handgun");
                case "m3":
                    //return TryGetAmmoItemId("shotgun");
                case "cqbr":
                    //return TryGetAmmoItemId("submachine");
                case "lightning":
                    //return TryGetAmmoItemId("mag");
                case "raiden":
                    //return TryGetAmmoItemId("large");
                case "mgl":
                    //return SelectAmmoItemId("acid", "explode");
                default:
                    return null;
            }
        }

        private string? TryGetAmmoItemId(string ammoKey)
        {
            if (!ItemData.AmmoItems.TryGetValue(ammoKey, out string ammoItemId))
                return null;

            return ammoItemId;
        }

        private string? SelectAmmoItemId(string primaryAmmoKey, string fallbackAmmoKey)
        {
            string? primaryItemId = TryGetAmmoItemId(primaryAmmoKey);
            string? fallbackItemId = TryGetAmmoItemId(fallbackAmmoKey);

            //if (primaryItemId.HasValue && HasInventoryItemId(primaryItemId.Value))
               // return primaryItemId;

            //if (fallbackItemId.HasValue)
              //  return fallbackItemId;

            return primaryItemId;
        }

        private bool HasInventoryItemId(int itemId)
        {
            var items = GetInventoryItems();
            foreach (var entry in items)
            {
                if (entry.ItemId == itemId && entry.Count > 0)
                    return true;
            }

            return false;
        }

        private int ResolveBulletId(ManagedObject itemManObj, int ammoItemId)
        {
            try
            {
                var itemManType = itemManObj.GetTypeDefinition();
                if (itemManType == null)
                    return ammoItemId;

                string[] methodNames =
                {
                    "getBulletIDFromItemID",
                    "getBulletIDFromItemId",
                    "getBulletIdFromItemID",
                    "getBulletIdFromItemId",
                    "getBulletTypeFromItemID",
                    "getBulletTypeFromItemId",
                    "getBulletIDFromItem",
                    "getBulletIdFromItem",
                    "getBulletTypeFromItem",
                    "getBulletTypeByItemID",
                    "getBulletIdByItemId"
                };

                foreach (var methodName in methodNames)
                {
                    if (itemManType.FindMethod(methodName) == null)
                        continue;

                    var result = itemManObj.Call(methodName, ammoItemId);
                    if (result != null)
                        return Convert.ToInt32(result);
                }
            }
            catch
            {
                // Best-effort only.
            }

            return ammoItemId;
        }

        /// <summary>
        /// Helper to get slot at index from slots field
        /// </summary>
        private ManagedObject? GetSlotAtIndex(object slots, int index)
        {
            try
            {
                var slotsArray = slots as System.Array;
                if (slotsArray != null && index < slotsArray.Length)
                {
                    return slotsArray.GetValue(index) as ManagedObject;
                }

                var slotsManaged = slots as ManagedObject;
                if (slotsManaged != null)
                {
                    var slot = slotsManaged.Call("get_Item", index) ?? slotsManaged.Call("get_Item", (uint)index);
                    return slot as ManagedObject;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool TryGetInventorySlots(out object? slotsObj, out int numSlots)
        {
            slotsObj = null;
            numSlots = 0;

            try
            {
                var inventory = GetInventory();
                if (inventory == null)
                    return false;

                var numSlotsObj = inventory.GetField("_UnlockSlotSize");
                if (numSlotsObj == null)
                    return false;

                numSlots = Convert.ToInt32(numSlotsObj);
                if (numSlots <= 0)
                    return false;

                slotsObj = inventory.GetField("_Slots");
                return slotsObj != null;
            }
            catch
            {
                return false;
            }
        }

        private List<(ManagedObject Slot, int ItemId, int Count)> GetInventoryItems()
        {
            var items = new List<(ManagedObject, int, int)>();
            if (!TryGetInventorySlots(out var slotsObj, out var numSlots) || slotsObj == null)
                return items;

            for (int i = 0; i < numSlots; i++)
            {
                var slot = GetSlotAtIndex(slotsObj, i);
                if (slot == null)
                    continue;

                var isBlankObj = slot.Call("get_IsEmpty");
                if (isBlankObj != null && Convert.ToBoolean(isBlankObj))
                    continue;

                var itemIdObj = slot.Call("get_ItemID");
                if (itemIdObj == null)
                    continue;

                int itemId = Convert.ToInt32(itemIdObj);
                if (itemId <= 0)
                    continue;

                int count = 1;
                var countObj = slot.Call("get_Number");
                if (countObj != null)
                    count = Math.Max(1, Convert.ToInt32(countObj));

                items.Add((slot, itemId, count));
            }

            return items;
        }

        private bool TryClearSlot(ManagedObject slot)
        {
            try
            {
                var typeDef = slot.GetTypeDefinition();
                if (typeDef?.FindMethod("clear") != null)
                {
                    slot.Call("clear");
                    return true;
                }

                if (typeDef?.FindMethod("set_IsForceBlank") != null)
                    slot.Call("set_IsForceBlank", true);

                if (typeDef?.FindMethod("set_ItemID") != null)
                    slot.Call("set_ItemID", 0);

                if (typeDef?.FindMethod("set_Number") != null)
                    slot.Call("set_Number", 0);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryReduceItemCount(int itemId, int amount)
        {
            try
            {
                if (amount <= 0)
                    return false;

                var itemMan = API.GetManagedSingleton("offline.gamemastering.InventoryManager") as ManagedObject;
                var itemManType = itemMan?.GetTypeDefinition();
                if (itemMan != null && itemManType?.FindMethod("reduceItem") != null)
                {
                    itemMan.Call("reduceItem", itemId, amount);
                    return true;
                }

                // Fallback: reduce directly from slots
                var remaining = amount;
                var items = GetInventoryItems();
                foreach (var entry in items)
                {
                    if (entry.ItemId != itemId)
                        continue;

                    int current = entry.Count;
                    if (current <= 0)
                        continue;

                    int remove = Math.Min(remaining, current);
                    remaining -= remove;

                    if (remove >= current)
                    {
                        TryClearSlot(entry.Slot);
                    }
                    else
                    {
                        entry.Slot.Call("set_Number", current - remove);
                    }

                    if (remaining <= 0)
                        break;
                }

                return remaining <= 0;
            }
            catch
            {
                return false;
            }
        }

        public bool TryTakeRandomAmmo(out string? ammoKey, out int removedAmount)
        {
            ammoKey = null;
            removedAmount = 0;

            var ammoById = new Dictionary<int, int>();
            var items = GetInventoryItems();
            foreach (var entry in items)
            {
                //if (!ItemData.AmmoItems.ContainsValue(entry.ItemId))
                    //continue;

                if (!ammoById.TryGetValue(entry.ItemId, out var total))
                    total = 0;
                ammoById[entry.ItemId] = total + entry.Count;
            }

            if (ammoById.Count == 0)
                return false;

            var ammoIds = ammoById.Keys.ToList();
            int ammoId = ammoIds[_random.Next(ammoIds.Count)];
            int totalCount = ammoById[ammoId];

            //ammoKey = ItemData.AmmoItems.FirstOrDefault(kvp => kvp.Value == ammoId).Key;
            int baseAmount = 1;
            if (ammoKey != null && ItemData.AmmoAmounts.TryGetValue(ammoKey, out var baseAmt))
                baseAmount = Math.Max(1, baseAmt);

            int max = Math.Max(1, Math.Min(totalCount, Math.Max(1, baseAmount * 2)));
            int min = Math.Min(max, Math.Max(1, baseAmount / 2));
            int remove = _random.Next(min, max + 1);
            if (remove > totalCount)
                remove = totalCount;

            if (TryReduceItemCount(ammoId, remove))
            {
                removedAmount = remove;
                return true;
            }

            return false;
        }

        public bool TryTakeRandomHealingItem(out int itemId)
        {
            itemId = -1;
            var items = GetInventoryItems();
            var healingItems = new List<int>();

            foreach (var entry in items)
            {
                //if (!ItemData.HealingItems.ContainsValue(entry.ItemId))
                   // continue;

                for (int i = 0; i < entry.Count; i++)
                    healingItems.Add(entry.ItemId);
            }

            if (healingItems.Count == 0)
                return false;

            itemId = healingItems[_random.Next(healingItems.Count)];
            return TryReduceItemCount(itemId, 1);
        }

        public bool TryRemoveEquippedWeapon()
        {
            try
            {
                var inventory = GetInventory();
                if (inventory == null)
                    return false;

                var mainSlot = inventory.Call("get_MainSlot") as ManagedObject;
                if (mainSlot == null)
                    return false;

                var weaponTypeObj = mainSlot.Call("get_WeaponType");
                if (weaponTypeObj == null)
                    return false;

                var itemMan = API.GetManagedSingleton("offline.gamemastering.InventoryManager") as ManagedObject;
                var itemManType = itemMan?.GetTypeDefinition();
                if (itemMan != null && itemManType?.FindMethod("removeWeapon") != null)
                {
                    itemMan.Call("removeWeapon", weaponTypeObj);
                    return true;
                }

                var slotType = mainSlot.GetTypeDefinition();
                if (slotType?.FindMethod("remove") != null)
                {
                    mainSlot.Call("remove");
                    return true;
                }

                return TryClearSlot(mainSlot);
            }
            catch
            {
                return false;
            }
        }

        public int DowngradeHealingItems()
        {
            int changed = 0;
            if (!ItemData.HealingItems.TryGetValue("herbg", out string greenHerbId))
                return 0;

            var items = GetInventoryItems();
            foreach (var entry in items)
            {
                //if (!ItemData.HealingItems.ContainsValue(entry.ItemId))
                   // continue;

                //if (entry.ItemId == greenHerbId)
                    //continue;

                entry.Slot.Call("set_ItemID", greenHerbId);
                entry.Slot.Call("set_Number", 1);
                changed++;
            }

            return changed;
        }

        public int UpgradeHealingItems()
        {
            int changed = 0;
            if (!ItemData.HealingItems.TryGetValue("spray", out string sprayId))
                return 0;

            var items = GetInventoryItems();
            foreach (var entry in items)
            {
                //if (!ItemData.HealingItems.ContainsValue(entry.ItemId))
                    //continue;

                //if (entry.ItemId == sprayId)
                    //continue;

                entry.Slot.Call("set_ItemID", sprayId);
                entry.Slot.Call("set_Number", 1);
                changed++;
            }

            return changed;
        }

        #region Enemy Health Effects

        /// <summary>
        /// Get all active enemies from EnemyManager
        /// </summary>
        private System.Collections.IEnumerable? GetAllActiveEnemies()
        {
            try
            {
                var enemyManagerObj = _playerManager as ManagedObject;
                if (enemyManagerObj == null)
                {
                    return null;
                }

                // Get ActiveEnemyList field
                var activeEnemyList = enemyManagerObj.Call("getSpawnedEnemyContextRefList") as ManagedObject;
                if (activeEnemyList == null)
                {
                    API.LogInfo($"RE9DotNet-CC: Error, No Enemy List");
                    return null;
                }

                var activeEnemyListObj = activeEnemyList as ManagedObject;
                if (activeEnemyListObj == null)
                    return null;

                // Try to get size
                var sizeObj = activeEnemyListObj.Call("get_Count");
                if (sizeObj == null)
                    return null;
                API.LogInfo(sizeObj.ToString());
                API.LogInfo($"{sizeObj}");
                int size = Convert.ToInt32(sizeObj);
                if (size <= 0)
                    return null;

                API.LogInfo($"{size}");
                // Return enumerable of enemies
                // The list is accessed using get_Item method
                var list = new List<object>();
                for (int i = 0; i < size; i++)
                {
                    try
                    {
                        // Use get_Item to access list items (like LUA's list[i])
                        var item = activeEnemyListObj.Call("get_Item", i);
                        if (item != null)
                            list.Add(item);
                    }
                    catch { }
                }
                if (list != null) API.LogInfo("RE9DotNet-CC: Found Enemy List");
                return list;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error getting active enemies - {ex.Message}");
                return null;
            }
        }

        private bool TryAdjustEnemyHitPoint(ManagedObject hitPointObj, float currentHP, float targetHP)
        {
            try
            {
                if (Math.Abs(targetHP - currentHP) < 0.01f)
                    return false;

                int targetInt = (int)Math.Round(targetHP);
                if (targetInt < 1)
                    targetInt = 1;

                int delta = (int)Math.Round(Math.Abs(targetHP - currentHP));
                if (delta <= 0)
                    delta = 1;

                var typeDef = ((ManagedObject)hitPointObj).GetTypeDefinition();
                if (targetInt <= 1 && typeDef?.FindMethod("resetHitPoint") != null)
                {
                    ((ManagedObject)hitPointObj).Call("resetHitPoint", targetInt);
                    if (typeDef?.FindMethod("get_CurrentHitPoint") != null)
                    {
                        var verifyObj = ((ManagedObject)hitPointObj).Call("get_CurrentHitPoint");
                        if (verifyObj != null && Convert.ToInt32(verifyObj) <= targetInt)
                            return true;
                    }
                    // If resetHitPoint doesn't apply, fall through to other methods.
                }

                if (typeDef?.FindMethod("set_CurrentHitPoint") != null)
                {
                    ((ManagedObject)hitPointObj).Call("set_CurrentHitPoint", targetInt);
                    var newHP = ((ManagedObject)hitPointObj).Call("get_CurrentHitPoint");
                    LogInfo($"Final HP: {newHP}, Alive: {_isPlayerAlive}");
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        void DumpObject(dynamic obj, int depth = 0)
        {
            if (obj == null || depth > 3) return;

            var typeDef = obj.get_type_definition();

            foreach (var field in typeDef.get_fields())
            {
                var value = field.get_data(obj);
                API.LogInfo($"{new string(' ', depth * 2)}{field.get_name()}: {value}");

                // Dive deeper into objects
                if (value != null && value.GetType().Name.Contains("Object"))
                {
                    DumpObject(value, depth + 1);
                }
            }
        }
        /// <summary>
        /// Damage all enemies by 25% of their max HP
        /// </summary>
        public bool DamageAllEnemies()
        {
            try
            {
                var enemies = GetAllActiveEnemies();
                if (enemies == null)
                {
                    API.LogInfo($"RE9DotNet-CC: Error, No Enemy List");
                    return false;
                }

                bool found = false;

                foreach (var enemy in enemies)
                {
                    try
                    {
                        var enemyObj = enemy as ManagedObject;
                        if (enemyObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, No Enemy Context");
                            continue;
                        }
                        var typeDef = enemyObj.GetTypeDefinition();

                        DumpObject(enemyObj, 15);
                        var hpObj = enemyObj.Call("get_HitPoint") as ManagedObject;

                        if (hpObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, Failed to Find Enemy HitPoint");
                            continue;
                        }

                        var currentObj = ((ManagedObject)hpObj).Call("get_CurrentHitPoint");
                        var maxObj = ((ManagedObject)hpObj).Call("get_CurrentMaximumHitPoint");
                        if ((int)currentObj == 0) continue;
                        if (currentObj == null || maxObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, Failed to get Current / Max Hit Points {currentObj}/Current, {maxObj}/Max ");
                            continue;
                        }

                        float currentHP = Convert.ToSingle(currentObj);
                        float maxHP = Convert.ToSingle(maxObj);
                        float damage;
                        damage = maxHP / 4.0f;

                        if (currentHP >= damage / 2.0f)
                        {
                            float newHP = currentHP;
                            if (currentHP <= damage)
                            {
                                newHP = 1.0f;
                            }
                            else
                            {
                                newHP = currentHP - damage;
                            }
                            if (TryAdjustEnemyHitPoint(hpObj, currentHP, newHP))
                                found = true;
                        }
                    }
                    catch { }
                }

                return found;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error damaging enemies - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Heal all enemies by 25% of their max HP
        /// </summary>
        public bool HealAllEnemies()
        {
            try
            {
                var enemies = GetAllActiveEnemies();
                if (enemies == null)
                {
                    API.LogInfo($"RE9DotNet-CC: Error, No Enemy List");
                    return false;
                }

                bool found = false;

                foreach (var enemy in enemies)
                {
                    try
                    {
                        var enemyObj = enemy as ManagedObject;
                        if (enemyObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, No Enemy Context");
                            continue;
                        }
                        var hpObj = enemyObj.Call("get_HitPoint") as ManagedObject;

                        if (hpObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, Failed to Find Enemy HitPoint");
                            continue;
                        }

                        var currentObj = ((ManagedObject)hpObj).Call("get_CurrentHitPoint");
                        var maxObj = ((ManagedObject)hpObj).Call("get_CurrentMaximumHitPoint");
                        if ((int)currentObj == 0) continue;
                        if (currentObj == null || maxObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, Failed to get Current / Max Hit Points");
                            continue;
                        }

                        float currentHP = Convert.ToSingle(currentObj);
                        float maxHP = Convert.ToSingle(maxObj);

                        float heal = maxHP / 4.0f;

                        if (currentHP <= maxHP - (heal / 2.0f))
                        {
                            float newHP;

                            if (currentHP + heal > maxHP)
                            {
                                newHP = maxHP;
                            }
                            else
                            {
                                newHP = currentHP + heal;
                            }

                            if (TryAdjustEnemyHitPoint(hpObj, currentHP, newHP))
                                found = true;
                        }
                    }
                    catch { }
                }

                return found;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error healing enemies - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set all enemies' HP to 1
        /// </summary>
        public bool SetAllEnemiesToOneHP()
        {
            try
            {
                var enemies = GetAllActiveEnemies();
                if (enemies == null)
                    return false;

                bool found = false;
                foreach (var enemy in enemies)
                {
                    try
                    {
                        var enemyObj = enemy as ManagedObject;
                        if (enemyObj == null)
                            continue;

                        // Get HitPoint field
                        var hpObj = enemyObj.Call("get_HitPoint") as ManagedObject;

                        if (hpObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, Failed to Find Enemy HitPoint");
                            continue;
                        }

                        var currentObj = ((ManagedObject)hpObj).Call("get_CurrentHitPoint");
                        var maxObj = ((ManagedObject)hpObj).Call("get_CurrentMaximumHitPoint");
                        if ((int)currentObj == 0) continue;
                        if (currentObj == null || maxObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, Failed to get Current / Max Hit Points");
                            continue;
                        }
                        float currentHP = Convert.ToSingle(currentObj);
                        float maxHP = Convert.ToSingle(maxObj);

                        if (currentHP > 1.0f)
                        {
                            if (TryAdjustEnemyHitPoint(hpObj, currentHP, 1.0f))
                                found = true;
                        }
                    }
                    catch { }
                }

                return found;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error setting enemies to 1 HP - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fully heal all enemies
        /// </summary>
        public bool FullHealAllEnemies()
        {
            try
            {
                var enemies = GetAllActiveEnemies();
                if (enemies == null)
                    return false;

                bool found = false;
                foreach (var enemy in enemies)
                {
                    try
                    {
                        var enemyObj = enemy as ManagedObject;
                        if (enemyObj == null)
                            continue;

                        // Get HitPoint field
                        var hpObj = enemyObj.Call("get_HitPoint") as ManagedObject;

                        if (hpObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, Failed to Find Enemy HitPoint");
                            continue;
                        }

                        var currentObj = ((ManagedObject)hpObj).Call("get_CurrentHitPoint");
                        var maxObj = ((ManagedObject)hpObj).Call("get_CurrentMaximumHitPoint");
                        if ((int)currentObj == 0) continue;
                        if (currentObj == null || maxObj == null)
                        {
                            API.LogInfo($"RE9DotNet-CC: Error, Failed to get Current / Max Hit Points");
                            continue;
                        }

                        float currentHP = Convert.ToSingle(currentObj);
                        float maxHP = Convert.ToSingle(maxObj);

                        if (currentHP < maxHP)
                        {
                            if (TryAdjustEnemyHitPoint(hpObj, currentHP, maxHP))
                                found = true;
                        }
                    }
                    catch { }
                }

                return found;
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error fully healing enemies - {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Enemy Size Effects

        /// <summary>
        /// Start enemy size effect
        /// </summary>
        public bool StartEnemySize(float scale, int durationMs, bool isGiant)
        {
            if (_enemySizeActive)
                return false;

            _targetEnemySize = scale;
            _enemySizeTimer = durationMs / 1000.0f;
            _enemySizeActive = true;
            _isEnemyGiant = isGiant;
            _enemySizeWasPaused = false;
            _enemySizeResumeDelay = 0.0f;
            _enemySizeRestorePending = false;
            _enemySizeRestoreReadyAt = null;

            return true;
        }

        /// <summary>
        /// Stop enemy size effect and restore original scale
        /// </summary>
        public void StopEnemySize()
        {
            StopEnemySizeInternal(deferRestore: false);
        }

        private void StopEnemySizeInternal(bool deferRestore)
        {
            if (!_enemySizeActive)
                return;

            _enemySizeActive = false;
            _enemySizeTimer = -1.0f;
            _targetEnemySize = 1.0f;
            _isEnemyGiant = false;
            _enemySizeWasPaused = false;
            _enemySizeResumeDelay = 0.0f;

            if (deferRestore)
            {
                _enemySizeRestorePending = true;
                _enemySizeRestoreReadyAt = DateTime.UtcNow + EnemySizeRestoreDelay;
                return;
            }

            _enemySizeRestorePending = false;
            _enemySizeRestoreReadyAt = null;

            // Restore all enemies to original scale
            RestoreAllEnemyScales();
        }

        /// <summary>
        /// Set request ID for enemy size effect
        /// </summary>
        public void SetEnemySizeRequestId(int requestId, string? requestID)
        {
            _enemySizeRequestId = requestId;
            _enemySizeRequestID = requestID;
        }

        /// <summary>
        /// Apply enemy size effect to all enemies (call this continuously)
        /// </summary>
        public void ApplyEnemySize()
        {
            if (!_enemySizeActive)
                return;

            try
            {
                var enemies = GetAllActiveEnemies();
                if (enemies == null)
                    return;

                foreach (var enemy in enemies)
                {
                    try
                    {
                        // Enemy is an EnemyController, need to get GameObject first
                        var enemyController = enemy as ManagedObject;
                        if (enemyController == null)
                            continue;

                        // Get GameObject from EnemyController
                        var gameObject = enemyController.Call("get_GameObject");
                        if (gameObject == null)
                            continue;

                        var gameObjectObj = gameObject as ManagedObject;
                        if (gameObjectObj == null)
                            continue;

                        // Get Transform from GameObject
                        var transform = gameObjectObj.Call("get_Transform");
                        if (transform == null)
                            continue;

                        var transformObj = transform as ManagedObject;
                        if (transformObj == null)
                            continue;

                        // Create new scale with target value
                        var newScale = REFrameworkNET.ValueType.New<via.vec3>();
                        newScale.x = _targetEnemySize;
                        newScale.y = _targetEnemySize;
                        newScale.z = _targetEnemySize;

                        transformObj.Call("set_LocalScale", newScale);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error applying enemy size - {ex.Message}");
            }
        }

        /// <summary>
        /// Restore all enemies to original scale (1.0)
        /// </summary>
        private void RestoreAllEnemyScales()
        {
            try
            {
                var enemies = GetAllActiveEnemies();
                if (enemies == null)
                    return;

                var restoreScale = REFrameworkNET.ValueType.New<via.vec3>();
                restoreScale.x = 1.0f;
                restoreScale.y = 1.0f;
                restoreScale.z = 1.0f;

                foreach (var enemy in enemies)
                {
                    try
                    {
                        // Enemy is an EnemyController, need to get GameObject first
                        var enemyController = enemy as ManagedObject;
                        if (enemyController == null)
                            continue;

                        // Get GameObject from EnemyController
                        var gameObject = enemyController.Call("get_GameObject");
                        if (gameObject == null)
                            continue;

                        var gameObjectObj = gameObject as ManagedObject;
                        if (gameObjectObj == null)
                            continue;

                        // Get Transform from GameObject
                        var transform = gameObjectObj.Call("get_Transform");
                        if (transform == null)
                            continue;

                        var transformObj = transform as ManagedObject;
                        if (transformObj == null)
                            continue;

                        transformObj.Call("set_LocalScale", restoreScale);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error restoring enemy scales - {ex.Message}");
            }
        }

        #endregion

        #region Enemy Speed Effects

        /// <summary>
        /// Start enemy speed effect
        /// </summary>
        public bool StartEnemySpeed(float speedMultiplier, int durationMs, bool isFast)
        {
            if (_enemySpeedActive)
                return false;

            _targetEnemySpeed = speedMultiplier;
            _enemySpeedTimer = durationMs / 1000.0f;
            _enemySpeedActive = true;
            _isEnemyFast = isFast;
            _enemySpeedWasPaused = false;

            return true;
        }

        /// <summary>
        /// Stop enemy speed effect and restore original speed
        /// </summary>
        public void StopEnemySpeed()
        {
            if (!_enemySpeedActive)
                return;

            _enemySpeedActive = false;
            _enemySpeedTimer = -1.0f;
            _targetEnemySpeed = 1.0f;
            _isEnemyFast = false;
            _enemySpeedWasPaused = false;

            // Restore all enemies to original speed
            RestoreAllEnemySpeeds();
        }

        /// <summary>
        /// Set request ID for enemy speed effect
        /// </summary>
        public void SetEnemySpeedRequestId(int requestId, string? requestID)
        {
            _enemySpeedRequestId = requestId;
            _enemySpeedRequestID = requestID;
        }

        /// <summary>
        /// Apply enemy speed effect to all enemies (call this continuously)
        /// </summary>
        public void ApplyEnemySpeed()
        {
            if (!_enemySpeedActive)
                return;

            try
            {
                var enemies = GetAllActiveEnemies();
                if (enemies == null)
                    return;

                foreach (var enemy in enemies)
                {
                    try
                    {
                        // Enemy is an EnemyController, need to get GameObject first
                        var enemyController = enemy as ManagedObject;
                        if (enemyController == null)
                            continue;

                        // Get GameObject from EnemyController
                        var gameObject = enemyController.Call("get_GameObject");
                        if (gameObject == null)
                            continue;

                        var gameObjectObj = gameObject as ManagedObject;
                        if (gameObjectObj == null)
                            continue;

                        // Get Motion component from GameObject
                        var tdb = API.GetTDB();
                        if (tdb == null)
                            continue;

                        var motionType = tdb.FindType("via.motion.Motion");
                        if (motionType == null)
                            continue;

                        var motion = gameObjectObj.Call("getComponent", motionType);
                        if (motion == null)
                            continue;

                        var motionObj = motion as ManagedObject;
                        if (motionObj == null)
                            continue;

                        // Set SecondaryPlaySpeed (same as player speed)
                        motionObj.Call("set_SecondaryPlaySpeed", _targetEnemySpeed);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error applying enemy speed - {ex.Message}");
            }
        }

        /// <summary>
        /// Restore all enemies to original speed (1.0)
        /// </summary>
        private void RestoreAllEnemySpeeds()
        {
            try
            {
                var enemies = GetAllActiveEnemies();
                if (enemies == null)
                    return;

                var tdb = API.GetTDB();
                if (tdb == null)
                    return;

                var motionType = tdb.FindType("via.motion.Motion");
                if (motionType == null)
                    return;

                foreach (var enemy in enemies)
                {
                    try
                    {
                        // Enemy is an EnemyController, need to get GameObject first
                        var enemyController = enemy as ManagedObject;
                        if (enemyController == null)
                            continue;

                        // Get GameObject from EnemyController
                        var gameObject = enemyController.Call("get_GameObject");
                        if (gameObject == null)
                            continue;

                        var gameObjectObj = gameObject as ManagedObject;
                        if (gameObjectObj == null)
                            continue;

                        var motion = gameObjectObj.Call("getComponent", motionType);
                        if (motion == null)
                            continue;

                        var motionObj = motion as ManagedObject;
                        if (motionObj == null)
                            continue;

                        motionObj.Call("set_SecondaryPlaySpeed", 1.0f);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogError($"GameState: Error restoring enemy speeds - {ex.Message}");
            }
        }

        #endregion

        #region Enemy Size Timer Updates

        /// <summary>
        /// Update enemy size effect timer and handle pause/resume
        /// </summary>
        public bool UpdateEnemySize(float deltaTime, bool isGameReady, out bool wasPaused, out bool justResumed)
        {
            wasPaused = false;
            justResumed = false;

            if (!_enemySizeActive || _enemySizeTimer <= 0)
                return false;

            // Handle pause/resume
            if (!isGameReady)
            {
                if (!_enemySizeWasPaused)
                {
                    _enemySizeWasPaused = true;
                    wasPaused = true;
                    _enemySizeResumeDelay = ENEMY_SIZE_RESUME_DELAY_SECONDS;

                    // Restore scale immediately when a cutscene/menu starts
                    RestoreAllEnemyScales();
                }
                return true; // Don't count down while paused
            }
            else
            {
                if (_enemySizeWasPaused)
                {
                    if (_enemySizeResumeDelay > 0.0f)
                    {
                        _enemySizeResumeDelay -= deltaTime;
                        return true; // Delay re-applying tiny/giant after cutscene
                    }

                    _enemySizeResumeDelay = 0.0f;
                    _enemySizeWasPaused = false;
                    justResumed = true;
                }
            }

            // Count down timer
            _enemySizeTimer -= deltaTime;

            // Apply target size continuously
            ApplyEnemySize();

            // Check if timer expired
            if (_enemySizeTimer <= 0)
            {
                StopEnemySizeInternal(deferRestore: justResumed || wasPaused);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get request ID for enemy size effect
        /// </summary>
        public int GetEnemySizeRequestId()
        {
            return _enemySizeRequestId;
        }

        /// <summary>
        /// Get request ID string for enemy size effect
        /// </summary>
        public string? GetEnemySizeRequestID()
        {
            return _enemySizeRequestID;
        }

        #endregion

        #region Enemy Speed Timer Updates

        /// <summary>
        /// Update enemy speed effect timer and handle pause/resume
        /// </summary>
        public bool UpdateEnemySpeed(float deltaTime, bool isGameReady, out bool wasPaused, out bool justResumed)
        {
            wasPaused = false;
            justResumed = false;

            if (!_enemySpeedActive || _enemySpeedTimer <= 0)
                return false;

            // Handle pause/resume
            if (!isGameReady)
            {
                if (!_enemySpeedWasPaused)
                {
                    _enemySpeedWasPaused = true;
                    wasPaused = true;
                }
                return true; // Don't count down while paused
            }
            else
            {
                if (_enemySpeedWasPaused)
                {
                    _enemySpeedWasPaused = false;
                    justResumed = true;
                }
            }

            // Count down timer
            _enemySpeedTimer -= deltaTime;

            // Apply speed every frame
            ApplyEnemySpeed();

            // Check if timer expired
            if (_enemySpeedTimer <= 0)
            {
                StopEnemySpeed();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get request ID for enemy speed effect
        /// </summary>
        public int GetEnemySpeedRequestId()
        {
            return _enemySpeedRequestId;
        }

        /// <summary>
        /// Get request ID string for enemy speed effect
        /// </summary>
        public string? GetEnemySpeedRequestID()
        {
            return _enemySpeedRequestID;
        }

        #endregion
    }
}





