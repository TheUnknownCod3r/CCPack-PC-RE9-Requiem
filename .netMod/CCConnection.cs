using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using REFrameworkNET;

namespace RE9DotNet_CC
{
    /// <summary>
    /// Manages TCP socket connection to Crowd Control app/SDK
    /// Game plugin connects to SDK which listens on port 64772
    /// Uses a single bidirectional socket connection
    /// </summary>
    public class CCConnection : IDisposable
    {
        private static readonly JsonSerializerOptions RequestJsonOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _isRunning = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _connectionTask;
        private Task? _readerTask;
        private readonly SemaphoreSlim _sendMutex = new(1, 1);

        // Standard Crowd Control port - SDK listens here, game connects here
        // Crowd Control simple-TCP pack default (matches PC connector convention)
        private const int PORT = 58431;  // SDK listens here, game connects here
        private const string HOST = "127.0.0.1";
        private const int RECONNECT_DELAY_MS = 2000; // Wait 2 seconds before reconnecting

        public event Action<CCRequest>? OnRequestReceived;

        public bool IsConnected => _isRunning && _client?.Connected == true;

        /// <summary>
        /// Start connecting to Crowd Control SDK
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                Logger.LogInfo("CCConnection: Already running");
                return;
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;

                // Start connection task that will connect to SDK
                // Use LongRunning to ensure it's a background thread
                Logger.LogInfo("CCConnection: Starting connection task...");
                _connectionTask = Task.Factory.StartNew(async () => 
                {
                    try
                    {
                        await ConnectToSDK(_cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"CCConnection: ❌ Connection task crashed - {ex.Message}");
                        Logger.LogError($"CCConnection: Stack trace: {ex.StackTrace}");
                    }
                }, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
                Logger.LogInfo($"CCConnection: Connection task created (Status: {_connectionTask.Status})");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CCConnection: Failed to start - {ex.Message}");
                _isRunning = false;
                throw;
            }
        }

        /// <summary>
        /// Stop and close connections
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            
            // Cancel all tasks FIRST
            try
            {
                _cancellationTokenSource?.Cancel();
                // Give tasks a moment to respond to cancellation
                System.Threading.Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"CCConnection: Error cancelling token - {ex.Message}");
            }

            // Close streams and connections immediately (don't wait for tasks)
            try
            {
                if (_stream != null)
                {
                    _stream.Close();
                    _stream.Dispose();
                    _stream = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"CCConnection: Error closing stream - {ex.Message}");
            }

            try
            {
                if (_client != null)
                {
                    if (_client.Connected)
                    {
                        _client.Close();
                    }
                    _client.Dispose();
                    _client = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"CCConnection: Error closing client - {ex.Message}");
            }

            // Don't wait for tasks - just clear references
            // Waiting can prevent unloading
            _readerTask = null;
            _connectionTask = null;
            
            Logger.LogInfo("CCConnection: Stopped");
        }

        /// <summary>
        /// Connect to Crowd Control SDK and maintain connection
        /// </summary>
        private async Task ConnectToSDK(CancellationToken cancellationToken)
        {
            Logger.LogInfo($"CCConnection: Connection task started, attempting to connect to Crowd Control at {HOST}:{PORT}...");
            
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    // Try to connect to SDK
                    Logger.LogInfo($"CCConnection: Attempting to connect to {HOST}:{PORT}...");
                    _client = new TcpClient();
                    
                    // Try to connect with timeout
                    var connectTask = _client.ConnectAsync(HOST, PORT);
                    var timeoutTask = Task.Delay(5000, cancellationToken); // 5 second timeout
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                    
                    if (completedTask == timeoutTask)
                    {
                        Logger.LogInfo("CCConnection: Connection timeout, Crowd Control may not be running");
                        _client?.Close();
                        _client = null;
                        await Task.Delay(RECONNECT_DELAY_MS, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    
                    await connectTask.ConfigureAwait(false); // Wait for connection to complete
                    
                    if (!_client.Connected)
                    {
                        Logger.LogInfo("CCConnection: Connection failed");
                        _client?.Close();
                        _client = null;
                        await Task.Delay(RECONNECT_DELAY_MS, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    
                    _stream = _client.GetStream();
                    Logger.LogInfo($"CCConnection: ✅ Connected to SDK at {HOST}:{PORT}");
                    
                    // Start reading messages from the SDK
                    // Use LongRunning to ensure it's a background thread
                    Logger.LogInfo("CCConnection: Creating reader task...");
                    _readerTask = Task.Factory.StartNew(async () => 
                    {
                        Logger.LogInfo("CCConnection: Reader task lambda started");
                        try
                        {
                            await ReadMessages(cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"CCConnection: Reader task exception - {ex.Message}");
                            Logger.LogError($"CCConnection: Stack trace: {ex.StackTrace}");
                        }
                    }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
                    Logger.LogInfo($"CCConnection: Reader task created (Status: {_readerTask.Status})");
                    
                    // Wait for connection to close or cancellation
                    while (_client.Connected && !cancellationToken.IsCancellationRequested && _isRunning)
                    {
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }
                    
                    Logger.LogInfo("CCConnection: SDK disconnected, will attempt to reconnect...");
                    _stream?.Close();
                    _client?.Close();
                    _client = null;
                    _stream = null;
                    
                    // Wait before reconnecting
                    await Task.Delay(RECONNECT_DELAY_MS, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Logger.LogInfo("CCConnection: Connection task cancelled");
                    break;
                }
                catch (SocketException ex)
                {
                    Logger.LogInfo($"CCConnection: Socket error - {ex.Message} (ErrorCode: {ex.SocketErrorCode})");
                    if (ex.SocketErrorCode == SocketError.ConnectionRefused)
                    {
                        Logger.LogInfo("CCConnection: SDK refused connection (may not be running), will retry...");
                    }
                    _client?.Close();
                    _client = null;
                    _stream = null;
                    if (_isRunning)
                    {
                        await Task.Delay(RECONNECT_DELAY_MS, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"CCConnection: ❌ Error connecting to SDK - {ex.Message}");
                    Logger.LogError($"CCConnection: Stack trace: {ex.StackTrace}");
                    _client?.Close();
                    _client = null;
                    _stream = null;
                    if (_isRunning)
                    {
                        await Task.Delay(RECONNECT_DELAY_MS, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            
            Logger.LogInfo("CCConnection: Connection task ended");
        }

        /// <summary>
        /// Read messages from the connected SDK
        /// </summary>
        private async Task ReadMessages(CancellationToken cancellationToken)
        {
            Logger.LogInfo("CCConnection: ReadMessages function entered");
            Logger.LogInfo($"CCConnection: _stream is null: {_stream == null}");
            Logger.LogInfo($"CCConnection: _client is null: {_client == null}");
            Logger.LogInfo($"CCConnection: _client.Connected: {_client?.Connected}");
            Logger.LogInfo("CCConnection: Reader task started, waiting for messages...");
            
            var buffer = new byte[4096];
            var messageBuilder = new StringBuilder();
            
            while (!cancellationToken.IsCancellationRequested && _isRunning && _client?.Connected == true)
            {
                try
                {
                    if (_stream == null)
                    {
                        Logger.LogInfo("CCConnection: Stream is null, waiting...");
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // Read data from stream (this will block until data is available)
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    
                    if (bytesRead > 0)
                    {
                        var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(chunk);
                        
                        var fullMessage = messageBuilder.ToString();
                        int startIndex = 0;
                        int terminatorIndex;

                        while ((terminatorIndex = fullMessage.IndexOf('\0', startIndex)) >= 0)
                        {
                            var jsonMessage = fullMessage.Substring(startIndex, terminatorIndex - startIndex);
                            startIndex = terminatorIndex + 1;

                            if (string.IsNullOrWhiteSpace(jsonMessage))
                                continue;

                            try
                            {
                                if (TryDeserializeRequest(jsonMessage, out var request, out var ignoreReason))
                                {
                                    if (request != null)
                                    {
                                        // Log only essential info
                                        Logger.LogInfo($"CCConnection: Received request - ID: {request.Id}, Code: '{request.Code}', Duration: {request.Duration}ms");
                                        OnRequestReceived?.Invoke(request);
                                    }
                                    else
                                    {
                                        Logger.LogError("CCConnection: Failed to parse request - deserialized to null");
                                    }
                                }
                                else if (!string.IsNullOrEmpty(ignoreReason))
                                {
                                    LogIgnoredNonRequest(jsonMessage, ignoreReason);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"CCConnection: Error parsing JSON message - {ex.Message}");
                            }
                        }

                        if (startIndex > 0)
                        {
                            messageBuilder.Clear();
                            if (startIndex < fullMessage.Length)
                            {
                                messageBuilder.Append(fullMessage.Substring(startIndex));
                            }
                        }
                        else if (messageBuilder.Length > 65536)
                        {
                            Logger.LogWarning("CCConnection: Message buffer exceeded 64KB without terminator; clearing buffer");
                            messageBuilder.Clear();
                        }
                    }
                    else
                    {
                        Logger.LogInfo("CCConnection: No bytes read (connection closed?)");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"CCConnection: ❌ Error reading message - {ex.Message}");
                    Logger.LogError($"CCConnection: Stack trace: {ex.StackTrace}");
                    break;
                }
            }
            
            Logger.LogInfo("CCConnection: Reader task ended");
        }

        /// <summary>
        /// Send response back to Crowd Control SDK
        /// </summary>
        public async Task SendResponseAsync(CCResponse response)
        {
            if (!IsConnected || _stream == null)
            {
                Logger.LogError("CCConnection: Cannot send response - not connected to SDK");
                return;
            }

            try
            {
                // Verify stream is writable
                if (!_stream.CanWrite)
                {
                    Logger.LogError("CCConnection: Stream is not writable!");
                    return;
                }
                
                // Create response with requestID to match SDK's request
                var responseObj = new
                {
                    id = response.id,
                    requestID = response.requestID, // Include requestID so SDK can match responses
                    status = response.status,
                    message = response.message,
                    timeRemaining = response.timeRemaining,
                    type = response.type
                };
                
                var options = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                
                var json = JsonSerializer.Serialize(responseObj, options);
                // SDK expects null-terminated strings
                var jsonWithNullTerminator = json + "\0";
                var data = Encoding.UTF8.GetBytes(jsonWithNullTerminator);
                
                // Verify connection is still active
                if (_client == null || !_client.Connected)
                {
                    Logger.LogError("CCConnection: Client is not connected, cannot send response");
                    return;
                }
                
                // Write synchronously to ensure immediate sending (NetworkStream supports this)
                await _sendMutex.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Connection may have dropped while waiting for the lock
                    if (!IsConnected || _stream == null || _client == null || !_client.Connected)
                        return;

                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();

                    // Log only essential info
                    Logger.LogInfo($"CCConnection: Sent response - ID: {response.id}, Status: {response.status}, Message: {response.message}");
                }
                catch (Exception writeEx)
                {
                    Logger.LogError($"CCConnection: Error during write/flush - {writeEx.Message}");
                    Logger.LogError($"CCConnection: Write exception type: {writeEx.GetType().Name}");
                    throw;
                }
                finally
                {
                    _sendMutex.Release();
                }

                return;
            }
            catch (Exception ex)
            {
                Logger.LogError($"CCConnection: ❌ Error sending response - {ex.Message}");
                Logger.LogError($"CCConnection: Exception type: {ex.GetType().Name}");
                Logger.LogError($"CCConnection: Stack trace: {ex.StackTrace}");
                return;
            }
        }

        public async Task SendMenuVisibilityAsync(IEnumerable<string> effectIds, bool visible)
        {
            if (!IsConnected || _stream == null)
            {
                Logger.LogError("CCConnection: Cannot send menu visibility - not connected to SDK");
                return;
            }

            var ids = new List<string>(effectIds);
            if (ids.Count == 0)
            {
                return;
            }

            try
            {
                if (!_stream.CanWrite)
                {
                    Logger.LogError("CCConnection: Stream is not writable!");
                    return;
                }

                Logger.LogInfo(
                    $"CCConnection: Menu {(visible ? "visible" : "hidden")} for {ids.Count}: {string.Join(", ", ids)}"
                );

                int status = visible ? 0x80 : 0x81;
                await _sendMutex.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!IsConnected || _stream == null || _client == null || !_client.Connected)
                        return;

                    foreach (var code in ids)
                    {
                        var payload = new
                        {
                            id = 0,
                            message = "",
                            code = code,
                            status = status,
                            type = 1
                        };

                        var json = JsonSerializer.Serialize(payload);
                        var data = Encoding.UTF8.GetBytes(json + "\0");

                        _stream.Write(data, 0, data.Length);
                        _stream.Flush();
                    }
                }
                finally
                {
                    _sendMutex.Release();
                }

                return;
            }
            catch (Exception ex)
            {
                Logger.LogError($"CCConnection: ❌ Error sending menu visibility - {ex.Message}");
                Logger.LogError($"CCConnection: Exception type: {ex.GetType().Name}");
                Logger.LogError($"CCConnection: Stack trace: {ex.StackTrace}");
                return;
            }
        }

        public void Dispose()
        {
            Stop();
            
            // Dispose cancellation token source
            try
            {
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"CCConnection: Error disposing cancellation token - {ex.Message}");
            }

            try
            {
                _sendMutex.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"CCConnection: Error disposing send mutex - {ex.Message}");
            }
            
            // Clear all references
            _cancellationTokenSource = null;
            _stream = null;
            _client = null;
            _readerTask = null;
            _connectionTask = null;
            
            Logger.LogInfo("CCConnection: Disposed");
        }

        private static void LogMalformedRequest(string jsonMessage)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonMessage);
                var root = doc.RootElement;

                string? idValue = null;
                if (root.TryGetProperty("id", out var idProp))
                {
                    idValue = idProp.ValueKind == JsonValueKind.String
                        ? $"\"{idProp.GetString()}\""
                        : idProp.ToString();
                }

                string? typeValue = null;
                if (root.TryGetProperty("type", out var typeProp))
                {
                    typeValue = typeProp.ValueKind == JsonValueKind.String
                        ? $"\"{typeProp.GetString()}\""
                        : typeProp.ToString();
                }

                string? codeValue = null;
                if (root.TryGetProperty("code", out var codeProp))
                {
                    codeValue = codeProp.ValueKind == JsonValueKind.String
                        ? codeProp.GetString()
                        : codeProp.ToString();
                }

                string? sourceDetailsType = null;
                if (root.TryGetProperty("sourceDetails", out var sourceProp)
                    && sourceProp.ValueKind == JsonValueKind.Object
                    && sourceProp.TryGetProperty("type", out var sourceTypeProp))
                {
                    sourceDetailsType = sourceTypeProp.ValueKind == JsonValueKind.String
                        ? sourceTypeProp.GetString()
                        : sourceTypeProp.ToString();
                }

                Logger.LogError(
                    $"CCConnection: Malformed request payload (id={idValue ?? "null"}, type={typeValue ?? "null"}, code={codeValue ?? "null"}, sourceDetails.type={sourceDetailsType ?? "null"})");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CCConnection: Failed to inspect malformed JSON - {ex.Message}");
            }

            const int maxLen = 512;
            if (jsonMessage.Length > maxLen)
            {
                Logger.LogError($"CCConnection: Malformed JSON (truncated): {jsonMessage.Substring(0, maxLen)}...");
            }
            else
            {
                Logger.LogError($"CCConnection: Malformed JSON: {jsonMessage}");
            }
        }

        private static bool TryDeserializeRequest(string jsonMessage, out CCRequest? request, out string? ignoreReason)
        {
            request = null;
            ignoreReason = null;

            try
            {
                using var doc = JsonDocument.Parse(jsonMessage);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    ignoreReason = "payload is not an object";
                    return false;
                }

                if (!TryGetString(root, "code", out var _))
                {
                    ignoreReason = "missing code";
                    return false;
                }

                if (!TryGetInt(root, "id", out var _))
                {
                    ignoreReason = "id is not numeric";
                    return false;
                }

                if (root.TryGetProperty("duration", out var durationProp)
                    && !TryGetInt(root, "duration", out var _))
                {
                    ignoreReason = "duration is not numeric";
                    return false;
                }

                request = JsonSerializer.Deserialize<CCRequest>(jsonMessage, RequestJsonOptions);
                return true;
            }
            catch (JsonException ex)
            {
                Logger.LogError($"CCConnection: Error parsing JSON message - {ex.Message}");
                LogMalformedRequest(jsonMessage);
                return false;
            }
        }

        private static void LogIgnoredNonRequest(string jsonMessage, string reason)
        {
            if (reason == "missing code")
                return;

            try
            {
                using var doc = JsonDocument.Parse(jsonMessage);
                var root = doc.RootElement;

                string? service = TryGetString(root, "service", out var serviceValue) ? serviceValue : null;
                string? type = TryGetString(root, "type", out var typeValue) ? typeValue : null;
                string? name = TryGetString(root, "name", out var nameValue) ? nameValue : null;
                string? id = TryGetString(root, "id", out var idValue) ? idValue : null;

                Logger.LogInfo($"CCConnection: Ignoring non-request payload ({reason}) service={service ?? "null"} type={type ?? "null"} name={name ?? "null"} id={id ?? "null"}");
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"CCConnection: Ignoring non-request payload ({reason}); failed to inspect - {ex.Message}");
            }

            const int maxLen = 256;
            if (jsonMessage.Length > maxLen)
            {
                Logger.LogInfo($"CCConnection: Ignored JSON (truncated): {jsonMessage.Substring(0, maxLen)}...");
            }
            else
            {
                Logger.LogInfo($"CCConnection: Ignored JSON: {jsonMessage}");
            }
        }

        private static bool TryGetString(JsonElement root, string propertyName, out string? value)
        {
            value = null;
            if (!root.TryGetProperty(propertyName, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                value = prop.ToString();
                return true;
            }

            return false;
        }

        private static bool TryGetInt(JsonElement root, string propertyName, out int value)
        {
            value = 0;
            if (!root.TryGetProperty(propertyName, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.Number)
                return prop.TryGetInt32(out value);

            if (prop.ValueKind == JsonValueKind.String)
                return int.TryParse(prop.GetString(), out value);

            return false;
        }
    }

    /// <summary>
    /// Crowd Control request structure
    /// </summary>
    public class CCRequest
    {
        public int id { get; set; }
        public string? requestID { get; set; }
        public string code { get; set; } = "";
        public object? parameters { get; set; }
        public object? viewer { get; set; }
        public int cost { get; set; }
        public int duration { get; set; }
        public int type { get; set; } = 1;
        
        // C# property accessors for convenience
        public int Id { get => id; set => id = value; }
        public string? RequestID { get => requestID; set => requestID = value; }
        public string Code { get => code; set => code = value; }
        public object? Parameters { get => parameters; set => parameters = value; }
        public object? Viewer { get => viewer; set => viewer = value; }
        public int Cost { get => cost; set => cost = value; }
        public int Duration { get => duration; set => duration = value; }
        public int RequestType { get => type; set => type = value; }
    }

    /// <summary>
    /// Crowd Control response structure
    /// </summary>
    public class CCResponse
    {
        public int id { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? requestID { get; set; }
        
        public int status { get; set; }
        public string message { get; set; } = "";
        public int timeRemaining { get; set; }
        public int type { get; set; } = 0;
        
        // C# property accessors for convenience (not serialized)
        [System.Text.Json.Serialization.JsonIgnore]
        public int Id { get => id; set => id = value; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string? RequestID { get => requestID; set => requestID = value; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public int Status { get => status; set => status = value; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string Message { get => message; set => message = value; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public int TimeRemaining { get => timeRemaining; set => timeRemaining = value; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public int ResponseType { get => type; set => type = value; }
    }

    /// <summary>
    /// Response status codes matching LUA version
    /// </summary>
    public static class CCStatus
    {
        public const int Success = 0;
        public const int Failure = 1;
        public const int Unavailable = 2;
        public const int Retry = 3;
        public const int Pause = 6;
        public const int Resumed = 7;
        public const int Stopped = 8;
    }
}



