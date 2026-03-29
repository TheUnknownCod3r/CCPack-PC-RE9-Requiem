using REFrameworkNET;
using REFrameworkNET.Attributes;
using REFrameworkNET.Callbacks;
using RE3DotNet_CC.Effects;
using Hexa.NET.ImGui;

namespace RE3DotNet_CC
{
    /// <summary>
    /// Main plugin entry point for RE3 Crowd Control .NET implementation
    /// </summary>
    public class RE3CrowdControlPlugin
    {
        private static CCConnection? _connection;
        private static GameState? _gameState;
        private static Dictionary<string, IEffect> _effects = new();
        private static bool _isInitialized = false;
        
        // For tracking delta time in OnUpdateMotion
        private static DateTime _lastUpdateTime = DateTime.Now;
        
        // For tracking pause state
        private static bool _wasPaused = false;
        private static bool _ohkoWasPaused = false;
        private static bool _invulWasPaused = false;
        private static bool _fovWasPaused = false;
        private static bool _scaleWasPaused = false;
        private static bool _speedWasPaused = false;
        private static bool _loadCleanupTriggered = false;
        private static HashSet<string>? _weaponEffectCodes;
        private static HashSet<string>? _ammoEffectCodes;
        private static HashSet<string>? _enemyEffectCodes;
        private static HashSet<string>? _weaponManipulationCodes;
        private static HashSet<string>? _costumeEffectCodes;
        private static bool _showConfigWindow = true;
        private const bool EnableRequestGate = false;
        private static readonly SemaphoreSlim _requestGate = new(8,8);

        // Configuration
        private static PluginConfig? _config;
        public static bool IsLoggingEnabled => Logger.IsEnabled;

        [PluginEntryPoint]
        public static void EntryPoint()
        {
            try
            {
                // Load configuration
                Logger.SetEnabled(false);
                _config = PluginConfig.Load();
                Logger.LogInfo($"RE9DotNet-CC: 2.0.1");
                EnemySpawnManager.SetMaxSpawnedEnemies(_config.MaxSpawnedEnemies);

                // Initialize game state
                _gameState = new GameState();
                Logger.LogInfo("RE9DotNet-CC: GameState initialized");

                // Register all effects
                RegisterEffects();
                Logger.LogInfo($"RE9DotNet-CC: Registered {_effects.Count} effects");

                // Initialize and start Crowd Control connection
                _connection = new CCConnection();
                _connection.OnRequestReceived += HandleRequest;
                _connection.Start();
                Logger.LogInfo("RE9DotNet-CC: Crowd Control connection started");

                _isInitialized = true;
                Logger.LogInfo("RE9DotNet-CC: Plugin loaded successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"RE9DotNet-CC: Failed to initialize - {ex.Message}");
                Logger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Hook into BeginRendering to apply FOV and camera height every frame (like LUA version)
        /// This ensures effects are continuously applied even if the game tries to reset them
        /// </summary>
        /// <summary>
        /// Draw UI in REFramework menu
        /// </summary>
        [Callback(typeof(ImGuiRender), CallbackType.Pre)]
        public static void OnDrawUI()
        {
            if (_config == null)
                return;

            if (!_showConfigWindow)
                return;

            if (!ImGui.Begin("Crowd Control Config", ref _showConfigWindow))
            {
                ImGui.End();
                return;
                    }

                    bool enableLogging = _config.EnableLogging;
                    if (ImGui.Checkbox("Enable Logging", ref enableLogging))
                    {
                        _config.EnableLogging = enableLogging;
                        Logger.SetEnabled(enableLogging);
                        _config.Save();
                        if (enableLogging)
                        {
                            Logger.LogInfo("RE9DotNet-CC: Logging enabled");
                        }
                    }

                    ImGui.Spacing();
                    int maxSpawnedEnemies = _config.MaxSpawnedEnemies;
            ImGui.Text("Max Spawned Enemies");
            if (ImGui.SliderInt("##MaxSpawnedEnemies", ref maxSpawnedEnemies, 1, 25))
                    {
                        _config.MaxSpawnedEnemies = maxSpawnedEnemies;
                        EnemySpawnManager.SetMaxSpawnedEnemies(maxSpawnedEnemies);
                _config.Save();
            }

                    ImGui.Spacing();
                    ImGui.Text($"Tracked Enemies: {EnemySpawnManager.GetSpawnedEnemyCount()}/{EnemySpawnManager.GetMaxSpawnedEnemies()}");
            ImGui.End();
        }

        [Callback(typeof(BeginRendering), CallbackType.Pre)]
        public static void OnPreBeginRendering()
        {
            try
            {
                if (!_isInitialized || _gameState == null)
                    return;

                // Apply FOV if the effect is active (independent of scale effects)
                if (_gameState.IsFOVActive)
                {
                    _gameState.ApplyFOV();
                }

                // Apply camera height adjustment if tiny player is active
                if (_gameState.IsScaleActive && !_gameState.IsGiant)
                {
                    _gameState.ApplyCameraHeight();
                }

                // Draw nameplates if enabled
                if (_config != null && _config.ShowNameplates)
                {
                    if (EnemySpawnManager.IsAtOrAboveLimit())
                        return;
                    DrawNameplates();
                }
            }
            catch
            {
                // Don't log errors here to avoid spam - effect application is best-effort
            }
        }

        /// <summary>
        /// Convert world position to screen coordinates
        /// </summary>
        private static System.Numerics.Vector2? WorldToScreen(System.Numerics.Vector3 worldPos)
        {
            try
            {
                // Get camera
                var camera = GetPrimaryCameraForNameplates();
                if (camera == null)
                    return null;

                var cameraObj = camera as ManagedObject;
                if (cameraObj == null)
                    return null;

                // Get projection and view matrices
                var projMatrixObj = cameraObj.Call("get_ProjectionMatrix");
                var viewMatrixObj = cameraObj.Call("get_ViewMatrix");
                
                // Get window size from ImGui (get_MainView may not exist, avoid spam)
                object? windowSizeObj = null;
                try
                {
                    var io = ImGui.GetIO();
                    // Create via.Size using ValueType
                    windowSizeObj = REFrameworkNET.ValueType.New<via.Size>();
                    if (windowSizeObj != null)
                    {
                        // via.Size uses w and h properties (not width/height)
                        var sizeValueType = windowSizeObj as REFrameworkNET.ValueType;
                        if (sizeValueType != null)
                        {
                            // Try to set w and h properties
                            try
                            {
                                sizeValueType.Call("set_w", io.DisplaySize.X);
                                sizeValueType.Call("set_h", io.DisplaySize.Y);
                            }
                            catch
                            {
                                // Property setter failed, try direct field access
                                try
                                {
                                    var sizeType = windowSizeObj.GetType();
                                    var wField = sizeType.GetField("w");
                                    var hField = sizeType.GetField("h");
                                    if (wField != null) wField.SetValue(windowSizeObj, io.DisplaySize.X);
                                    if (hField != null) hField.SetValue(windowSizeObj, io.DisplaySize.Y);
                                }
                                catch
                                {
                                    return null;
                                }
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                catch
                {
                    return null;
                }
                
                if (windowSizeObj == null)
                    return null;

                if (projMatrixObj == null || viewMatrixObj == null || windowSizeObj == null)
                    return null;

                // Get via.math type and worldPos2ScreenPos method
                var mathType = API.GetTDB()?.FindType("via.math");
                if (mathType == null)
                    return null;

                // Find the static method worldPos2ScreenPos(via.vec3, via.mat4, via.mat4, via.Size)
                var worldToScreenMethod = mathType.FindMethod("worldPos2ScreenPos(via.vec3, via.mat4, via.mat4, via.Size)");
                if (worldToScreenMethod == null)
                    return null;

                // Create world position vector
                var worldPosVec = REFrameworkNET.ValueType.New<via.vec3>();
                worldPosVec.x = worldPos.X;
                worldPosVec.y = worldPos.Y;
                worldPosVec.z = worldPos.Z;

                // Call static method (pass null as instance for static methods)
                var screenPosRet = worldToScreenMethod.Invoke(null, new object[] { worldPosVec, viewMatrixObj, projMatrixObj, windowSizeObj });
                
                // Extract actual value from InvokeRet wrapper (InvokeRet is a struct, can't be null)
                object? screenPosObj = ExtractFromInvokeRet(screenPosRet);
                if (screenPosObj == null)
                    return null;

                // Extract screen position
                var screenPosValueType = screenPosObj as REFrameworkNET.ValueType;
                if (screenPosValueType == null)
                    return null;

                var xObj = screenPosValueType.Call("get_Item", 0);
                var yObj = screenPosValueType.Call("get_Item", 1);

                if (xObj == null || yObj == null)
                    return null;

                float screenX = Convert.ToSingle(xObj);
                float screenY = Convert.ToSingle(yObj);

                return new System.Numerics.Vector2(screenX, screenY);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get primary camera for nameplate drawing
        /// </summary>
        private static object? GetPrimaryCameraForNameplates()
        {
            try
            {
                // Try CameraManager first
                var cameraManager = API.GetManagedSingleton("via.camera");
                if (cameraManager != null)
                {
                    var cameraManagerObj = cameraManager as ManagedObject;
                    if (cameraManagerObj != null)
                    {
                        var camera = cameraManagerObj.Call("get_MainCamera603180");
                        if (camera != null)
                            return camera;
                    }
                }

                // Don't try SceneManager->MainView approach - get_MainView may not exist
                // and causes massive error spam. CameraManager is the only reliable way.
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Draw nameplates for spawned enemies
        /// </summary>
        private static void DrawNameplates()
        {
            // Disabled per request to avoid crashes around large enemy piles.
                    return;
        }

        /// <summary>
        /// Extract the actual object from InvokeRet wrapper
        /// </summary>
        private static object? ExtractFromInvokeRet(object invokeRet)
        {
            try
            {
                if (invokeRet == null) return null;
                
                // Try direct casting first
                if (invokeRet is ManagedObject managedObj)
                {
                    return managedObj;
                }
                
                if (invokeRet is REFrameworkNET.ValueType valueType)
                {
                    return valueType;
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
                            try
                            {
                                ptrUInt64 = Convert.ToUInt64(ptrValue);
                            }
                            catch
                            {
                                return null;
                            }
                        }
                        
                        // Check if pointer is valid (not null)
                        if (ptrUInt64 != 0)
                        {
                            // Try ManagedObject first
                            var managedObject = ManagedObject.ToManagedObject(ptrUInt64);
                            if (managedObject != null)
                            {
                                return managedObject;
                            }
                            
                            // If it's a ValueType, try to create it from the pointer
                            // For now, just return the invokeRet as-is if it's already a ValueType
                        }
                    }
                }
                
                // If all else fails, return as-is (might already be the correct type)
                return invokeRet;
            }
            catch
            {
                return null;
            }
        }

        [PluginExitPoint]
        public static void ExitPoint()
        {
            try
            {
                Logger.LogInfo("RE9DotNet-CC: Plugin unloading...");

                // Set flag first to prevent new operations
                _isInitialized = false;

                // Stop and dispose connection
                if (_connection != null)
                {
                    Logger.LogInfo("RE9DotNet-CC: Stopping connection...");
                    
                    // Remove event handler first to prevent new requests
                    try
                    {
                        _connection.OnRequestReceived -= HandleRequest;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInfo($"RE9DotNet-CC: Error removing event handler - {ex.Message}");
                    }
                    
                    try
                    {
                        _connection.Stop();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInfo($"RE9DotNet-CC: Error stopping connection - {ex.Message}");
                    }
                    
                    try
                    {
                        _connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInfo($"RE9DotNet-CC: Error disposing connection - {ex.Message}");
                    }
                    
                    _connection = null;
                    Logger.LogInfo("RE9DotNet-CC: Connection stopped and disposed");
                }

                // Clear effects
                try
                {
                    _effects.Clear();
                }
                catch (Exception ex)
                {
                    Logger.LogInfo($"RE9DotNet-CC: Error clearing effects - {ex.Message}");
                }

                // Clear game state
                _gameState = null;

                try
                {
                    EnemySpawnManager.TryRequestDestroyAllTracked();
                    EnemySpawnManager.ClearAll();
                    EnemySpawner.ResetPrefabCache();
                }
                catch (Exception ex)
                {
                    Logger.LogInfo($"RE9DotNet-CC: Error clearing enemy state - {ex.Message}");
                }

                // Don't force GC - it can prevent unloading
                // Just clear references and let it happen naturally

                Logger.LogInfo("RE9DotNet-CC: Plugin unloaded successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"RE9DotNet-CC: Error during unload - {ex.Message}");
                Logger.LogError($"RE9DotNet-CC: Stack trace: {ex.StackTrace}");
            }
        }

        [Callback(typeof(UpdateMotion), CallbackType.Pre)]
        public static void OnUpdateMotion()
        {
            // Only update if initialized to avoid holding references after unload
            if (!_isInitialized)
                return;

            try
            {
                // Calculate delta time (in seconds)
                DateTime now = DateTime.Now;
                float deltaTime = (float)(now - _lastUpdateTime).TotalSeconds;
                _lastUpdateTime = now;

                // Update game state periodically
                _gameState?.Update();

                var gameState = _gameState;
                if (gameState == null)
                    return;

                bool isGameReady = gameState.IsGameReady;
                bool isPaused = !isGameReady;

                if (isGameReady)
                {
                    EnemySpawnManager.TickCleanup();
                    UpdateCharacterMenuVisibility(gameState);
                }

                // Handle pause state changes
                if (isPaused != _wasPaused)
                {
                    _wasPaused = isPaused;
                    if (isPaused)
                    {
                        Logger.LogInfo("RE9DotNet-CC: Game paused (menu/cutscene/loading)");
                    }
                    else
                    {
                        Logger.LogInfo("RE9DotNet-CC: Game resumed");
                    }
                }

                // Detect actual load request and run cleanup once
                if (IsLoadRequested())
                {
                    if (!_loadCleanupTriggered)
                    {
                        Logger.LogInfo("RE9DotNet-CC: Load request detected, running cleanup");
                        EnemySpawnManager.TryRequestDestroyAllTracked();
                        EnemySpawnManager.ClearAll();
                        EnemySpawner.ResetPrefabCache();
                        _loadCleanupTriggered = true;
                    }
                }
                else
                {
                    _loadCleanupTriggered = false;
                }

                // Update OHKO effect if active
                if (_gameState != null && _gameState.IsOneHitKO)
                {
                    bool stillActive = _gameState.UpdateOHKO(deltaTime, isGameReady, out bool wasPaused, out bool justResumed);
                    
                    // Send pause/resume status updates
                    if (_connection != null)
                    {
                        int requestId = _gameState.GetOHKORequestId();
                        string? requestID = _gameState.GetOHKORequestID();
                        
                        if (requestId > 0)
                        {
                            if (justResumed)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: OHKO resumed, sending Resumed response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Resumed,
                                    Message = "OHKO effect resumed",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _ohkoWasPaused = false;
                            }
                            else if (wasPaused && !_ohkoWasPaused)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: OHKO paused, sending Pause response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Pause,
                                    Message = "OHKO effect paused",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _ohkoWasPaused = true;
                            }
                            else if (!wasPaused)
                            {
                                _ohkoWasPaused = false;
                            }
                        }
                    }
                    
                    // If OHKO expired, send Stopped response
                    if (!stillActive && _connection != null)
                    {
                        int requestId = _gameState.GetOHKORequestId();
                        string? requestID = _gameState.GetOHKORequestID();
                        
                        if (requestId > 0)
                        {
                            Logger.LogInfo($"RE9DotNet-CC: OHKO expired, sending Stopped response for request ID: {requestId}");
                            
                            var response = new CCResponse
                            {
                                Id = requestId,
                                RequestID = requestID,
                                Status = CCStatus.Stopped,
                                Message = "OHKO effect ended",
                                TimeRemaining = 0,
                                ResponseType = 0
                            };

                            // Fire and forget - we're in a callback and can't await
                            Task.Run(async () => await _connection.SendResponseAsync(response));
                            
                            // Clear request IDs to allow effect to be triggered again
                            _gameState.SetOHKORequestId(0, null);
                        }
                    }
                }

                // Update Invincibility effect if active
                if (_gameState != null && _gameState.IsInvulnerable)
                {
                    bool stillActive = _gameState.UpdateInvincibility(deltaTime, isGameReady, out bool wasPaused, out bool justResumed);
                    
                    // Send pause/resume status updates
                    if (_connection != null)
                    {
                        int requestId = _gameState.GetInvincibilityRequestId();
                        string? requestID = _gameState.GetInvincibilityRequestID();
                        
                        if (requestId > 0)
                        {
                            if (justResumed)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: Invincibility resumed, sending Resumed response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Resumed,
                                    Message = "Invincibility effect resumed",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _invulWasPaused = false;
                            }
                            else if (wasPaused && !_invulWasPaused)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: Invincibility paused, sending Pause response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Pause,
                                    Message = "Invincibility effect paused",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _invulWasPaused = true;
                            }
                            else if (!wasPaused)
                            {
                                _invulWasPaused = false;
                            }
                        }
                    }
                    
                    // If Invincibility expired, send Stopped response
                    if (!stillActive && _connection != null)
                    {
                        int requestId = _gameState.GetInvincibilityRequestId();
                        string? requestID = _gameState.GetInvincibilityRequestID();
                        
                        if (requestId > 0)
                        {
                            Logger.LogInfo($"RE9DotNet-CC: Invincibility expired, sending Stopped response for request ID: {requestId}");
                            
                            var response = new CCResponse
                            {
                                Id = requestId,
                                RequestID = requestID,
                                Status = CCStatus.Stopped,
                                Message = "Invincibility effect ended",
                                TimeRemaining = 0,
                                ResponseType = 0
                            };

                            // Fire and forget - we're in a callback and can't await
                            Task.Run(async () => await _connection.SendResponseAsync(response));
                            
                            // Clear request IDs to allow effect to be triggered again
                            _gameState.SetInvincibilityRequestId(0, null);
                        }
                    }
                }

                // Update FOV effect if active
                if (_gameState != null && _gameState.IsFOVActive)
                {
                    bool stillActive = _gameState.UpdateFOV(deltaTime, isGameReady, out bool wasPaused, out bool justResumed);
                    
                    // Send pause/resume status updates
                    if (_connection != null)
                    {
                        int requestId = _gameState.GetFOVRequestId();
                        string? requestID = _gameState.GetFOVRequestID();
                        
                        if (requestId > 0)
                        {
                            if (justResumed)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: FOV effect resumed, sending Resumed response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Resumed,
                                    Message = "FOV effect resumed",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _fovWasPaused = false;
                            }
                            else if (wasPaused && !_fovWasPaused)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: FOV effect paused, sending Pause response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Pause,
                                    Message = "FOV effect paused",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _fovWasPaused = true;
                            }
                            else if (!wasPaused)
                            {
                                _fovWasPaused = false;
                            }
                        }
                    }
                    
                    // If FOV effect expired, send Stopped response
                    if (!stillActive && _connection != null)
                    {
                        int requestId = _gameState.GetFOVRequestId();
                        string? requestID = _gameState.GetFOVRequestID();
                        
                        if (requestId > 0)
                        {
                            Logger.LogInfo($"RE9DotNet-CC: FOV effect expired, sending Stopped response for request ID: {requestId}");
                            
                            var response = new CCResponse
                            {
                                Id = requestId,
                                RequestID = requestID,
                                Status = CCStatus.Stopped,
                                Message = "FOV effect ended",
                                TimeRemaining = 0,
                                ResponseType = 0
                            };

                            // Fire and forget - we're in a callback and can't await
                            Task.Run(async () => await _connection.SendResponseAsync(response));
                            
                            // Clear request IDs to allow effect to be triggered again
                            _gameState.SetFOVRequestId(0, null);
                        }
                    }
                }

                // Update scale effect if active
                if (_gameState != null && _gameState.IsScaleActive)
                {
                    bool stillActive = _gameState.UpdateScale(deltaTime, isGameReady, out bool wasPaused, out bool justResumed);
                    
                    // Send pause/resume status updates
                    if (_connection != null)
                    {
                        int requestId = _gameState.GetScaleRequestId();
                        string? requestID = _gameState.GetScaleRequestID();
                        
                        if (requestId > 0)
                        {
                            if (justResumed)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: Scale effect resumed, sending Resumed response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Resumed,
                                    Message = "Scale effect resumed",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _scaleWasPaused = false;
                            }
                            else if (wasPaused && !_scaleWasPaused)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: Scale effect paused, sending Pause response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Pause,
                                    Message = "Scale effect paused",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _scaleWasPaused = true;
                            }
                            else if (!wasPaused)
                            {
                                _scaleWasPaused = false;
                            }
                        }
                    }
                    
                    // If scale effect expired, send Stopped response
                    if (!stillActive && _connection != null)
                    {
                        int requestId = _gameState.GetScaleRequestId();
                        string? requestID = _gameState.GetScaleRequestID();
                        
                        if (requestId > 0)
                        {
                            Logger.LogInfo($"RE9DotNet-CC: Scale effect expired, sending Stopped response for request ID: {requestId}");
                            
                            var response = new CCResponse
                            {
                                Id = requestId,
                                RequestID = requestID,
                                Status = CCStatus.Stopped,
                                Message = "Scale effect ended",
                                TimeRemaining = 0,
                                ResponseType = 0
                            };

                            // Fire and forget - we're in a callback and can't await
                            Task.Run(async () => await _connection.SendResponseAsync(response));
                            
                            // Clear request IDs to allow effect to be triggered again
                            _gameState.SetScaleRequestId(0, null);
                        }
                    }
                }

                // Update speed effect if active
                if (_gameState != null && _gameState.IsSpeedActive)
                {
                    bool stillActive = _gameState.UpdateSpeed(deltaTime, isGameReady, out bool wasPaused, out bool justResumed);
                    
                    // Send pause/resume status updates
                    if (_connection != null)
                    {
                        int requestId = _gameState.GetSpeedRequestId();
                        string? requestID = _gameState.GetSpeedRequestID();
                        
                        if (requestId > 0)
                        {
                            if (justResumed)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: Speed effect resumed, sending Resumed response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Resumed,
                                    Message = "Speed effect resumed",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _speedWasPaused = false;
                            }
                            else if (wasPaused && !_speedWasPaused)
                            {
                                Logger.LogInfo($"RE9DotNet-CC: Speed effect paused, sending Pause response for request ID: {requestId}");
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Pause,
                                    Message = "Speed effect paused",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                _speedWasPaused = true;
                            }
                            else if (!wasPaused)
                            {
                                _speedWasPaused = false;
                            }
                        }
                    }
                    
                    // If speed effect expired, send Stopped response
                    if (!stillActive)
                    {
                        var connection = _connection;
                        if (connection == null)
                            return;

                        int requestId = _gameState.GetSpeedRequestId();
                        string? requestID = _gameState.GetSpeedRequestID();
                        
                        if (requestId > 0)
                        {
                            Logger.LogInfo($"RE9DotNet-CC: Speed effect expired, sending Stopped response for request ID: {requestId}");
                            
                            var response = new CCResponse
                            {
                                Id = requestId,
                                RequestID = requestID,
                                Status = CCStatus.Stopped,
                                Message = "Speed effect ended",
                                TimeRemaining = 0,
                                ResponseType = 0
                            };

                            // Fire and forget - we're in a callback and can't await
                            Task.Run(async () => await connection.SendResponseAsync(response));
                            
                            // Clear request IDs to allow effect to be triggered again
                            _gameState.SetSpeedRequestId(0, null);
                        }
                    }
                }

                // Update enemy size effect
                if (gameState.IsEnemySizeActive)
                {
                    bool stillActive = gameState.UpdateEnemySize(deltaTime, isGameReady, out bool wasPaused, out bool justResumed);
                    
                    if (_connection != null)
                    {
                        int requestId = gameState.GetEnemySizeRequestId();
                        string? requestID = gameState.GetEnemySizeRequestID();
                        if (requestId > 0 && requestID != null)
                        {
                            if (justResumed)
                            {
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Resumed,
                                    Message = "Enemy size effect resumed",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                            }
                            else if (wasPaused)
                            {
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Pause,
                                    Message = "Enemy size effect paused",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                            }
                        }

                        if (!stillActive)
                        {
                            int sizeRequestId = gameState.GetEnemySizeRequestId();
                            string? sizeRequestID = gameState.GetEnemySizeRequestID();
                            if (sizeRequestId > 0 && sizeRequestID != null)
                            {
                                var response = new CCResponse
                                {
                                    Id = sizeRequestId,
                                    RequestID = sizeRequestID,
                                    Status = CCStatus.Stopped,
                                    Message = "Enemy size effect stopped",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                gameState.SetEnemySizeRequestId(0, null);
                            }
                        }
                    }
                }

                // Update enemy speed effect
                if (gameState.IsEnemySpeedActive)
                {
                    bool stillActive = gameState.UpdateEnemySpeed(deltaTime, isGameReady, out bool wasPaused, out bool justResumed);
                    
                    if (_connection != null)
                    {
                        int requestId = gameState.GetEnemySpeedRequestId();
                        string? requestID = gameState.GetEnemySpeedRequestID();
                        if (requestId > 0 && requestID != null)
                        {
                            if (justResumed)
                            {
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Resumed,
                                    Message = "Enemy speed effect resumed",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                            }
                            else if (wasPaused)
                            {
                                var response = new CCResponse
                                {
                                    Id = requestId,
                                    RequestID = requestID,
                                    Status = CCStatus.Pause,
                                    Message = "Enemy speed effect paused",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                            }
                        }

                        if (!stillActive)
                        {
                            int speedRequestId = gameState.GetEnemySpeedRequestId();
                            string? speedRequestID = gameState.GetEnemySpeedRequestID();
                            if (speedRequestId > 0 && speedRequestID != null)
                            {
                                var response = new CCResponse
                                {
                                    Id = speedRequestId,
                                    RequestID = speedRequestID,
                                    Status = CCStatus.Stopped,
                                    Message = "Enemy speed effect stopped",
                                    TimeRemaining = 0,
                                    ResponseType = 0
                                };
                                Task.Run(async () => await _connection.SendResponseAsync(response));
                                gameState.SetEnemySpeedRequestId(0, null);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail - plugin might be unloading
            }
        }

        private static bool IsLoadRequested()
        {
            try
            {
                var sceneManagerNative = API.GetNativeSingleton("via.SceneManager");
                if (sceneManagerNative == null)
                    return false;

                var scene = sceneManagerNative.Call("get_CurrentScene");
                if (scene is not ManagedObject sceneObj)
                    return false;

                var tdb = API.GetTDB();
                var loadingType = tdb?.FindType("offline.gui.Loading5secBehavior");
                if (loadingType != null)
                {
                    var loadingComponents = sceneObj.Call("findComponents", loadingType);
                    if (AnyComponent(loadingComponents, component =>
                        component is ManagedObject compObj
                        && compObj.GetTypeDefinition()?.FindMethod("isBusyLoading5sec") != null
                        && compObj.Call("isBusyLoading5sec") is object busy
                        && Convert.ToBoolean(busy)))
                    {
                        return true;
                    }
                }

                var loadBehaviorType = tdb?.FindType("offline.gui.LoadBehavior");
                if (loadBehaviorType != null)
                {
                    var loadComponents = sceneObj.Call("findComponents", loadBehaviorType);
                    if (AnyComponent(loadComponents, component =>
                    {
                        if (component is not ManagedObject compObj)
                            return false;

                        var compType = compObj.GetTypeDefinition();
                        if (compType?.FindField("IsLoaded") != null)
                        {
                            var isLoaded = compObj.GetField("IsLoaded");
                            if (isLoaded != null && Convert.ToBoolean(isLoaded))
                                return true;
                        }

                        if (compType?.FindField("KeepRequest") != null)
                        {
                            var keepRequest = compObj.GetField("KeepRequest");
                            if (keepRequest != null && Convert.ToBoolean(keepRequest))
                                return true;
                        }

                        return false;
                    }))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // ignore lookup failures
            }

            return false;
        }

        private static bool AnyComponent(object? components, Func<object, bool> predicate)
        {
            if (components == null)
                return false;

            if (components is System.Array array)
            {
                foreach (var item in array)
                {
                    if (item != null && predicate(item))
                        return true;
                }

                return false;
            }

            if (components is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null && predicate(item))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Register all available effects
        /// </summary>
        private static void RegisterEffects()
        {
            // Health effects
            RegisterEffect(new HealEffect());
            RegisterEffect(new HurtEffect());
            RegisterEffect(new KillEffect());
            RegisterEffect(new OneHPEffect());
            RegisterEffect(new OHKOEffect());
            RegisterEffect(new FullHealEffect());
            RegisterEffect(new InvincibilityEffect());
            // RE3 LUA set does not include poison/burn/heal status effects

            // Camera effects
            RegisterEffect(new WideCameraEffect());
            RegisterEffect(new NarrowCameraEffect());

            // Player scale effects
            RegisterEffect(new GiantPlayerEffect());
            RegisterEffect(new TinyPlayerEffect());
            
            // Speed effects
            RegisterEffect(new FastSpeedEffect());
            RegisterEffect(new SlowSpeedEffect());
            RegisterEffect(new HyperSpeedEffect());

            // Enemy health effects
            RegisterEffect(new EnemyDamageEffect());
            RegisterEffect(new EnemyHealEffect());
            RegisterEffect(new EnemyOneHPEffect());
            RegisterEffect(new EnemyFullHealEffect());

            // Enemy size effects
            RegisterEffect(new EnemyGiantEffect());
            RegisterEffect(new EnemyTinyEffect());

            // Enemy speed effects
            RegisterEffect(new EnemyFastSpeedEffect());
            RegisterEffect(new EnemySlowSpeedEffect());

            // Weapon effects
            RegisterEffect(new EmptyAmmoEffect());
            RegisterEffect(new FillAmmoEffect());
            RegisterEffect(new UnequipWeaponEffect());
            RegisterEffect(new TakeAmmoEffect());
            RegisterEffect(new TakeWeaponEffect());
            RegisterEffect(new TakeHealingEffect());
            RegisterEffect(new UpgradeHealingItemsEffect());
            RegisterEffect(new DowngradeHealingItemsEffect());

            // Give weapon effects
            RegisterEffect(new GiveWeapG19Effect());
            RegisterEffect(new GiveWeapBurstEffect());
            RegisterEffect(new GiveWeapG18Effect());
            RegisterEffect(new GiveWeapEdgeEffect());
            RegisterEffect(new GiveWeapMupEffect());
            RegisterEffect(new GiveWeapM3Effect());
            RegisterEffect(new GiveWeapCqbrEffect());
            RegisterEffect(new GiveWeapLightningEffect());
            RegisterEffect(new GiveWeapRaidenEffect());
            RegisterEffect(new GiveWeapMglEffect());
            RegisterEffect(new GiveWeapKnifeEffect());
            RegisterEffect(new GiveWeapSurviveEffect());
            RegisterEffect(new GiveWeapHotEffect());
            RegisterEffect(new GiveWeapRocketEffect());
            RegisterEffect(new GiveWeapGrenadeEffect());
            RegisterEffect(new GiveWeapFlashEffect());

            // Give ammo effects - using single dynamic class
            RegisterEffect(new GiveAmmoEffect("giveammo_handgun"));
            RegisterEffect(new GiveAmmoEffect("giveammo_shotgun"));
            RegisterEffect(new GiveAmmoEffect("giveammo_submachine"));
            RegisterEffect(new GiveAmmoEffect("giveammo_mag"));
            RegisterEffect(new GiveAmmoEffect("giveammo_mine"));
            RegisterEffect(new GiveAmmoEffect("giveammo_explode"));
            RegisterEffect(new GiveAmmoEffect("giveammo_acid"));
            RegisterEffect(new GiveAmmoEffect("giveammo_flame"));
            RegisterEffect(new GiveAmmoEffect("giveammo_needle"));
            RegisterEffect(new GiveAmmoEffect("giveammo_fuel"));
            RegisterEffect(new GiveAmmoEffect("giveammo_large"));
            RegisterEffect(new GiveAmmoEffect("giveammo_slshigh"));
            RegisterEffect(new GiveAmmoEffect("giveammo_detonator"));
            RegisterEffect(new GiveAmmoEffect("giveammo_ink"));
            RegisterEffect(new GiveAmmoEffect("giveammo_board"));

            // Give healing item effects
            RegisterEffect(new GiveHealHerbgEffect());
            RegisterEffect(new GiveHealHerbbEffect());
            RegisterEffect(new GiveHealHerbrEffect());
            RegisterEffect(new GiveHealHerbggEffect());
            RegisterEffect(new GiveHealHerbgbEffect());
            RegisterEffect(new GiveHealHerbgrEffect());
            RegisterEffect(new GiveHealSprayEffect());
            RegisterEffect(new GiveHealHerbgrbEffect());

            // Spawn enemy effects - register known RE3 prefabs
            string[] enemyCodes = {
                "spawn_em0000",
                "spawn_em0020",
                "spawn_em0100",
                "spawn_em0200",
                "spawn_em0300",
                "spawn_em0400",
                "spawn_em0500",
                "spawn_em0600",
                "spawn_em0700",
                "spawn_em0800",
                "spawn_em1000",
                "spawn_em2500",
                "spawn_em2600",
                "spawn_em2700",
                "spawn_em3000",
                "spawn_em4000",
                "spawn_em3300",
                "spawn_em3400",
                "spawn_em3500",
                "spawn_em7000",
                "spawn_em7100",
                "spawn_em7200",
                "spawn_em8400",
                "spawn_em9000",
                "spawn_em9010",
                "spawn_em9020",
                "spawn_em9030",
                "spawn_em9040",
                "spawn_em9050",
                "spawn_em9091",
                "spawn_em9100",
                "spawn_em9200",
                "spawn_em9201",
                "spawn_em9210",
                "spawn_em9300",
                "spawn_em9400",
                "spawn_em9401",
                "spawn_em9410",
                "spawn_em9999"
            };

            foreach (var enemyCode in enemyCodes)
            {
                RegisterEffect(new SpawnEnemyEffect(enemyCode));
            }
        }

        /// <summary>
        /// Register a single effect
        /// </summary>
        private static void RegisterEffect(IEffect effect)
        {
            _effects[effect.Code] = effect;
            Logger.LogInfo($"RE9DotNet-CC: Registered effect '{effect.Code}'");
        }

        private static void UpdateCharacterMenuVisibility(GameState gameState)
        {
            // RE3 LUA mod does not apply character-based menu filtering, so keep all effects visible.
            if (_connection == null || !_connection.IsConnected)
            {
                return;
            }
        }

        private static HashSet<string> GetSherryAllowedCodes()
        {
            return new HashSet<string>(_effects.Keys, StringComparer.OrdinalIgnoreCase);
        }

        public static bool AllowAllWeapons => _config?.AllWeapons == true;
        public static bool AllowEnemySpawns(GameState gameState)
        {
                return true;
        }

        public static bool AllowWeaponManipulation(GameState gameState)
        {
            return true;
        }

        public static bool AllowCostumeSwap(GameState gameState)
        {
            return true;
        }

        public static bool AllowNoirCostume => _config?.AllowNoirCostume == true;

        private static HashSet<string> GetWeaponEffectCodes()
        {
            if (_weaponEffectCodes != null)
            {
                return _weaponEffectCodes;
            }

            _weaponEffectCodes = _effects.Keys
                .Where(code => code.StartsWith("giveweap_", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _weaponEffectCodes;
        }

        private static HashSet<string> GetAmmoEffectCodes()
        {
            if (_ammoEffectCodes != null)
            {
                return _ammoEffectCodes;
            }

            _ammoEffectCodes = _effects.Keys
                .Where(code => code.StartsWith("giveammo_", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _ammoEffectCodes;
        }

        private static HashSet<string> GetWeaponManipulationCodes()
        {
            if (_weaponManipulationCodes != null)
            {
                return _weaponManipulationCodes;
            }

            _weaponManipulationCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "emptyweap",
                "fillweap",
                "takeammo",
                "takeweap",
                "unequipweap"
            };

            return _weaponManipulationCodes;
        }

        private static HashSet<string> GetEnemyEffectCodes()
        {
            if (_enemyEffectCodes != null)
            {
                return _enemyEffectCodes;
            }

            _enemyEffectCodes = _effects.Keys
                .Where(code => code.StartsWith("spawn_", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _enemyEffectCodes;
        }

        private static HashSet<string> GetCostumeEffectCodes()
        {
            if (_costumeEffectCodes != null)
            {
                return _costumeEffectCodes;
            }

            _costumeEffectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return _costumeEffectCodes;
        }

        private enum PlayerCharacter
        {
            Unknown,
            Jill,
            Carlos,
            Ada,
            Sherry
        }

        /// <summary>
        /// Handle incoming Crowd Control request
        /// </summary>
        private static async void HandleRequest(CCRequest request)
        {
            // Check if plugin is still initialized
            if (!_isInitialized)
                return;

            // Log incoming request (simplified)
            Logger.LogInfo($"RE9DotNet-CC: Received effect '{request.Code}' (ID: {request.Id}, Duration: {request.Duration}ms)");

            if (_connection == null || _gameState == null)
            {
                Logger.LogError("RE9DotNet-CC: Cannot handle request - plugin not initialized");
                // Try to send error response anyway
                if (_connection != null)
                {
                    await SendResponseAsync(_connection, request, CCStatus.Retry, "Plugin not initialized");
                }
                return;
            }

            bool gateAcquired = false;
            try
            {
                if (EnableRequestGate && !_requestGate.Wait(0))
                {
                    Logger.LogWarning($"RE9DotNet-CC: Too many in-flight effects, retrying '{request.Code}' (ID: {request.Id})");
                    if (_connection != null)
                    {
                        await SendResponseAsync(_connection, request, CCStatus.Retry, "Too many active effects - please retry");
                    }
                    return;
                }

                gateAcquired = EnableRequestGate;

                // Find matching effect
                if (!_effects.TryGetValue(request.Code, out var effect))
                {
                    // Allow dynamic spawn_* effects without pre-registration
                    if (request.Code.StartsWith("spawn_", StringComparison.OrdinalIgnoreCase)
                        && !request.Code.Equals("spawn_costume", StringComparison.OrdinalIgnoreCase))
                    {
                        effect = new SpawnEnemyEffect(request.Code);
                        _effects[request.Code] = effect;
                        Logger.LogInfo($"RE9DotNet-CC: Dynamically registered '{request.Code}'");
                    }
                    else
                {
                    Logger.LogInfo($"RE9DotNet-CC: ⚠️ UNKNOWN EFFECT CODE: '{request.Code}' (ID: {request.Id}, RequestID: {request.RequestID})");
                    Logger.LogInfo($"RE9DotNet-CC: Registered effects: {string.Join(", ", _effects.Keys)}");
                    await SendResponseAsync(_connection, request, CCStatus.Failure, $"Unknown effect code: {request.Code}");
                    return;
                    }
                }

                Logger.LogInfo($"RE9DotNet-CC: Found effect handler for '{request.Code}' (ID: {request.Id}, RequestID: {request.RequestID})");

                // Execute effect with timeout to ensure we respond within SDK's 10 second limit
                var status = await ExecuteWithTimeoutAsync(() => effect.ExecuteAsync(_gameState, request), TimeSpan.FromSeconds(8));

                // Send response with descriptive messages
                string message;
                switch (status)
                {
                    case CCStatus.Success:
                        message = $"Effect '{request.Code}' executed successfully";
                        break;
                    case CCStatus.Failure:
                        message = $"Effect '{request.Code}' failed to execute";
                        break;
                    case CCStatus.Unavailable:
                        message = $"Effect '{request.Code}' is currently unavailable";
                        break;
                    case CCStatus.Retry:
                        message = $"Effect '{request.Code}' could not be executed - please try again";
                        break;
                    default:
                        message = $"Effect '{request.Code}' returned status {status}";
                        break;
                }

                Logger.LogInfo($"RE9DotNet-CC: Effect '{request.Code}' result: {status} - {message}");
                await SendResponseAsync(_connection, request, status, message);
            }
            catch (Exception ex)
            {
                Logger.LogError($"RE9DotNet-CC: ❌ ERROR handling request - {ex.Message}");
                Logger.LogError($"RE9DotNet-CC: Stack trace: {ex.StackTrace}");
                if (_connection != null)
                {
                    try
                    {
                        await SendResponseAsync(_connection, request, CCStatus.Retry, $"Error: {ex.Message}");
                    }
                    catch (Exception sendEx)
                    {
                        Logger.LogError($"RE9DotNet-CC: Failed to send error response - {sendEx.Message}");
                    }
                }
            }
            finally
            {
                if (gateAcquired)
                    _requestGate.Release();
            }
        }

        /// <summary>
        /// Execute an async task with a timeout
        /// </summary>
        private static async Task<int> ExecuteWithTimeoutAsync(Func<Task<int>> task, TimeSpan timeout)
        {
            try
            {
                var timeoutTask = Task.Delay(timeout);
                var resultTask = task();
                var completedTask = await Task.WhenAny(resultTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    Logger.LogError("RE9DotNet-CC: Effect execution timed out");
                    return CCStatus.Retry;
                }
                
                return await resultTask;
            }
            catch (Exception ex)
            {
                Logger.LogError($"RE9DotNet-CC: Error in ExecuteWithTimeoutAsync - {ex.Message}");
                return CCStatus.Retry;
            }
        }

        /// <summary>
        /// Send response to Crowd Control
        /// </summary>
        private static async Task SendResponseAsync(CCConnection connection, CCRequest request, int status, string message)
        {
            if (status == CCStatus.Failure)
            {
                status = CCStatus.Retry;
            }

            var response = new CCResponse
            {
                Id = request.Id,
                RequestID = request.RequestID, // Include requestID to match the request
                Status = status,
                Message = message,
                TimeRemaining = 0,
                ResponseType = 0
            };

            await connection.SendResponseAsync(response);
        }
    }
}




