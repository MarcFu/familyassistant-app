using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FamilyAssistant.Configuration;
using Microsoft.Extensions.Options;

namespace FamilyAssistant.Services;

public class HomeAssistantService : IHomeAssistantService, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HomeAssistantOptions _config;
    private readonly ILogger<HomeAssistantService> _logger;

    // ─── WebSocket State ────────────────────────────────────────
    private ClientWebSocket? _ws;
    private readonly SemaphoreSlim _wsLock = new(1, 1);
    private int _messageId;
    private CancellationTokenSource? _wsLoopCts;
    private Task? _wsLoopTask;

    // Pending requests waiting for a response (id → TaskCompletionSource)
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();

    // Active event subscriptions (HA subscription_id → callback info)
    private readonly ConcurrentDictionary<int, SubscriptionInfo> _subscriptions = new();

    // Entity filter → HA subscription ID mapping (for targeted entity subscriptions)
    private readonly ConcurrentDictionary<int, string> _subscriptionEntityFilter = new();

    // Event log: circular buffer for live monitoring
    private const int MaxEventLogEntries = 200;
    private readonly LinkedList<HaEventLogEntry> _eventLog = new();
    private readonly object _eventLogLock = new();
    private int _monitorSubscriptionId;
    public bool IsEventLoggingEnabled { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HomeAssistantService(IHttpClientFactory httpClientFactory, IOptions<HomeAssistantOptions> options, ILogger<HomeAssistantService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = options.Value;
        _logger = logger;
    }

    public bool IsWebSocketConnected => _ws?.State == WebSocketState.Open;

    // ─── HTTP Client Helper ─────────────────────────────────────

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient("HomeAssistant");
        client.BaseAddress = new Uri(_config.BaseUrl.TrimEnd('/') + "/api/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    // ─── REST API ───────────────────────────────────────────────

    public async Task<IReadOnlyList<HaPerson>> GetPersonsAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync("states", ct);
            response.EnsureSuccessStatusCode();

            var states = await response.Content.ReadFromJsonAsync<List<HaStateResponse>>(JsonOptions, ct);
            if (states is null) return [];

            return states
                .Where(s => s.EntityId.StartsWith("person."))
                .Select(s => new HaPerson(
                    s.EntityId,
                    s.Attributes.TryGetValue("friendly_name", out var name) ? name?.ToString() ?? s.EntityId : s.EntityId,
                    s.State,
                    s.Attributes.TryGetValue("entity_picture", out var pic) ? pic?.ToString() : null,
                    s.Attributes.TryGetValue("user_id", out var userId) ? userId?.ToString() : null))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get persons from Home Assistant");
            return [];
        }
    }

    public async Task<HaEntityState?> GetEntityStateAsync(string entityId, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync($"states/{entityId}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var state = await response.Content.ReadFromJsonAsync<HaStateResponse>(JsonOptions, ct);
            if (state is null) return null;

            return new HaEntityState(state.EntityId, state.State, state.Attributes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entity state for {EntityId}", entityId);
            return null;
        }
    }

    public async Task SetEntityStateAsync(string entityId, string state, Dictionary<string, object?>? attributes = null, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateHttpClient();
            var payload = new
            {
                state,
                attributes = attributes ?? new Dictionary<string, object?>()
            };

            var response = await client.PostAsJsonAsync($"states/{entityId}", payload, JsonOptions, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Set entity {EntityId} to state {State}", entityId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set entity state for {EntityId}", entityId);
            throw;
        }
    }

    public async Task CallServiceAsync(string domain, string service, object? data = null, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateHttpClient();
            var response = await client.PostAsJsonAsync($"services/{domain}/{service}", data ?? new { }, JsonOptions, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Called service {Domain}.{Service}", domain, service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call service {Domain}.{Service}", domain, service);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync("", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Home Assistant connection test failed");
            return false;
        }
    }

    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(string path, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateHttpClient();
            var url = path.TrimStart('/');
            if (url.StartsWith("api/"))
                url = url[4..];

            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            var data = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            return (data, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get image from HA: {Path}", path);
            return null;
        }
    }

    public async Task<IReadOnlyList<HaEntityState>> GetEntitiesByDomainAsync(string domain, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync("states", ct);
            response.EnsureSuccessStatusCode();

            var states = await response.Content.ReadFromJsonAsync<List<HaStateResponse>>(JsonOptions, ct);
            if (states is null) return [];

            return states
                .Where(s => s.EntityId.StartsWith($"{domain}."))
                .Select(s => new HaEntityState(s.EntityId, s.State, s.Attributes))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entities for domain {Domain}", domain);
            return [];
        }
    }

    // ─── Entity Registry (cached) ──────────────────────────────

    private IReadOnlyDictionary<string, HaEntityRegistryInfo>? _registryCache;
    private DateTime _registryCacheTime;
    private static readonly TimeSpan RegistryCacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyDictionary<string, HaEntityRegistryInfo>> GetEntityRegistryAsync(CancellationToken ct = default)
    {
        // Return cache if still valid
        if (_registryCache is not null && DateTime.UtcNow - _registryCacheTime < RegistryCacheDuration)
            return _registryCache;

        if (!IsWebSocketConnected)
        {
            _logger.LogDebug("WebSocket not connected, cannot fetch entity registry");
            return _registryCache ?? new Dictionary<string, HaEntityRegistryInfo>();
        }

        try
        {
            // Fetch all three registries in parallel
            var areasTask = SendWsCommandAsync("config/area_registry/list", ct);
            var devicesTask = SendWsCommandAsync("config/device_registry/list", ct);
            var entitiesTask = SendWsCommandAsync("config/entity_registry/list", ct);

            await Task.WhenAll(areasTask, devicesTask, entitiesTask);

            var areas = areasTask.Result;
            var devices = devicesTask.Result;
            var entities = entitiesTask.Result;

            // Build area lookup: area_id → name
            var areaLookup = new Dictionary<string, string>();
            if (areas.ValueKind == JsonValueKind.Array)
            {
                foreach (var area in areas.EnumerateArray())
                {
                    var areaId = area.GetProperty("area_id").GetString();
                    var areaName = area.GetProperty("name").GetString();
                    if (areaId is not null && areaName is not null)
                        areaLookup[areaId] = areaName;
                }
            }

            // Build device lookup: device_id → (name, area_id)
            var deviceLookup = new Dictionary<string, (string? Name, string? AreaId)>();
            if (devices.ValueKind == JsonValueKind.Array)
            {
                foreach (var device in devices.EnumerateArray())
                {
                    var deviceId = device.GetProperty("id").GetString();
                    var deviceName = device.TryGetProperty("name_by_user", out var nbu) && nbu.ValueKind == JsonValueKind.String
                        ? nbu.GetString()
                        : (device.TryGetProperty("name", out var n) ? n.GetString() : null);
                    var deviceAreaId = device.TryGetProperty("area_id", out var a) && a.ValueKind == JsonValueKind.String
                        ? a.GetString()
                        : null;

                    if (deviceId is not null)
                        deviceLookup[deviceId] = (deviceName, deviceAreaId);
                }
            }

            // Build entity registry info
            var result = new Dictionary<string, HaEntityRegistryInfo>();
            if (entities.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in entities.EnumerateArray())
                {
                    var entityId = entity.GetProperty("entity_id").GetString();
                    if (entityId is null) continue;

                    var entityAreaId = entity.TryGetProperty("area_id", out var ea) && ea.ValueKind == JsonValueKind.String
                        ? ea.GetString()
                        : null;
                    var entityDeviceId = entity.TryGetProperty("device_id", out var ed) && ed.ValueKind == JsonValueKind.String
                        ? ed.GetString()
                        : null;
                    var platform = entity.TryGetProperty("platform", out var p) && p.ValueKind == JsonValueKind.String
                        ? p.GetString()
                        : null;

                    // Resolve area: entity-level area takes precedence, then device-level area
                    string? areaName = null;
                    string? deviceName = null;

                    if (entityDeviceId is not null && deviceLookup.TryGetValue(entityDeviceId, out var deviceInfo))
                    {
                        deviceName = deviceInfo.Name;
                        var effectiveAreaId = entityAreaId ?? deviceInfo.AreaId;
                        if (effectiveAreaId is not null && areaLookup.TryGetValue(effectiveAreaId, out var an))
                            areaName = an;
                    }
                    else if (entityAreaId is not null && areaLookup.TryGetValue(entityAreaId, out var directArea))
                    {
                        areaName = directArea;
                    }

                    result[entityId] = new HaEntityRegistryInfo(entityId, areaName, deviceName, platform);
                }
            }

            _registryCache = result;
            _registryCacheTime = DateTime.UtcNow;
            _logger.LogInformation("Entity registry loaded: {Count} entities, {Areas} areas, {Devices} devices",
                result.Count, areaLookup.Count, deviceLookup.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch entity registry");
            return _registryCache ?? new Dictionary<string, HaEntityRegistryInfo>();
        }
    }

    private async Task<JsonElement> SendWsCommandAsync(string commandType, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _messageId);
        var tcs = new TaskCompletionSource<JsonElement>();
        _pendingRequests[id] = tcs;

        await SendMessageAsync(new { type = commandType, id }, ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
        timeoutCts.Token.Register(() => tcs.TrySetCanceled());

        var result = await tcs.Task;

        if (result.TryGetProperty("result", out var resultArray))
            return resultArray;

        return result;
    }

    // ─── WebSocket API ──────────────────────────────────────────

    public async Task ConnectWebSocketAsync(CancellationToken ct = default)
    {
        await _wsLock.WaitAsync(ct);
        try
        {
            if (_ws?.State == WebSocketState.Open)
                return;

            _ws?.Dispose();
            _ws = new ClientWebSocket();

            var wsUrl = _config.BaseUrl
                .Replace("https://", "wss://")
                .Replace("http://", "ws://")
                .TrimEnd('/') + "/api/websocket";

            _logger.LogInformation("Connecting WebSocket to {Url}", wsUrl);
            await _ws.ConnectAsync(new Uri(wsUrl), ct);

            // Read auth_required
            var msg = await ReceiveMessageAsync(ct);
            if (msg.GetProperty("type").GetString() != "auth_required")
            {
                _logger.LogError("Unexpected first WebSocket message: {Type}", msg.GetProperty("type").GetString());
                return;
            }

            // Send auth
            await SendMessageAsync(new { type = "auth", access_token = _config.Token }, ct);

            // Read auth result
            msg = await ReceiveMessageAsync(ct);
            var authResult = msg.GetProperty("type").GetString();
            if (authResult != "auth_ok")
            {
                _logger.LogError("WebSocket auth failed: {Type}", authResult);
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Auth failed", ct);
                return;
            }

            _logger.LogInformation("WebSocket connected and authenticated to Home Assistant");

            // Start message loop
            _wsLoopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _wsLoopTask = Task.Run(() => WebSocketMessageLoop(_wsLoopCts.Token), _wsLoopCts.Token);
        }
        finally
        {
            _wsLock.Release();
        }
    }

    public async Task<bool> GetUserDarkModeAsync(CancellationToken ct = default)
    {
        if (!IsWebSocketConnected)
        {
            _logger.LogWarning("WebSocket not connected, defaulting to dark mode");
            return true;
        }

        try
        {
            var id = Interlocked.Increment(ref _messageId);
            var tcs = new TaskCompletionSource<JsonElement>();
            _pendingRequests[id] = tcs;

            await SendMessageAsync(new { type = "frontend/get_user_data", id, key = "core" }, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            var result = await tcs.Task;

            // Response: {"result": {"showAdvanced": ..., "selectedTheme": {"theme": "...", "dark": true/false}}}
            if (result.TryGetProperty("result", out var resultObj) &&
                resultObj.ValueKind == JsonValueKind.Object &&
                resultObj.TryGetProperty("selectedTheme", out var theme) &&
                theme.ValueKind == JsonValueKind.Object &&
                theme.TryGetProperty("dark", out var dark))
            {
                var isDark = dark.GetBoolean();
                _logger.LogInformation("HA user theme preference: dark={IsDark}", isDark);
                return isDark;
            }

            _logger.LogDebug("Could not parse theme from HA response, defaulting to dark");
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetUserDarkMode timed out");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user dark mode preference");
            return true;
        }
    }

    public async Task<string?> GetUserLanguageAsync(CancellationToken ct = default)
    {
        if (!IsWebSocketConnected)
        {
            _logger.LogWarning("WebSocket not connected, cannot read language preference");
            return null;
        }

        try
        {
            var id = Interlocked.Increment(ref _messageId);
            var tcs = new TaskCompletionSource<JsonElement>();
            _pendingRequests[id] = tcs;

            await SendMessageAsync(new { type = "frontend/get_user_data", id, key = "core" }, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            var result = await tcs.Task;

            // Response: {"result": {"selectedLanguage": "de", ...}}
            if (result.TryGetProperty("result", out var resultObj) &&
                resultObj.ValueKind == JsonValueKind.Object &&
                resultObj.TryGetProperty("selectedLanguage", out var lang) &&
                lang.ValueKind == JsonValueKind.String)
            {
                var language = lang.GetString();
                _logger.LogInformation("HA user language preference: {Language}", language);
                return language;
            }

            _logger.LogDebug("Could not parse language from HA response");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetUserLanguage timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user language preference");
            return null;
        }
    }

    public async Task<HaUserFrontendData> GetUserFrontendDataAsync(CancellationToken ct = default)
    {
        var defaultData = new HaUserFrontendData(DarkMode: null, Language: null, PrimaryColor: null, AccentColor: null);

        if (!IsWebSocketConnected)
        {
            _logger.LogWarning("WebSocket not connected, returning default frontend data");
            return defaultData;
        }

        try
        {
            var id = Interlocked.Increment(ref _messageId);
            var tcs = new TaskCompletionSource<JsonElement>();
            _pendingRequests[id] = tcs;

            await SendMessageAsync(new { type = "frontend/get_user_data", id, key = "core" }, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            var result = await tcs.Task;

            // Response: {"result": {"selectedLanguage": "de", "selectedTheme": {"theme": "...", "dark": true/false (missing=auto), "primaryColor": "#03a9f4", "accentColor": "#ff9800"}}}
            bool? darkMode = null; // null = Auto (follow OS preference)
            string? language = null;
            string? primaryColor = null;
            string? accentColor = null;

            if (result.TryGetProperty("result", out var resultObj) && resultObj.ValueKind == JsonValueKind.Object)
            {
                // Language
                if (resultObj.TryGetProperty("selectedLanguage", out var lang) && lang.ValueKind == JsonValueKind.String)
                {
                    language = lang.GetString();
                }

                // Theme
                if (resultObj.TryGetProperty("selectedTheme", out var theme) && theme.ValueKind == JsonValueKind.Object)
                {
                    // dark: true = forced dark, false = forced light, missing/null = auto
                    if (theme.TryGetProperty("dark", out var dark) && dark.ValueKind == JsonValueKind.True)
                    {
                        darkMode = true;
                    }
                    else if (theme.TryGetProperty("dark", out _) && dark.ValueKind == JsonValueKind.False)
                    {
                        darkMode = false;
                    }
                    // else: darkMode remains null (Auto)

                    if (theme.TryGetProperty("primaryColor", out var pc) && pc.ValueKind == JsonValueKind.String)
                    {
                        primaryColor = pc.GetString();
                    }
                    if (theme.TryGetProperty("accentColor", out var ac) && ac.ValueKind == JsonValueKind.String)
                    {
                        accentColor = ac.GetString();
                    }
                }
            }

            _logger.LogInformation("HA frontend data: darkMode={DarkMode}, lang={Language}, primary={Primary}, accent={Accent}",
                darkMode?.ToString() ?? "auto", language, primaryColor, accentColor);

            return new HaUserFrontendData(darkMode, language, primaryColor, accentColor);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetUserFrontendData timed out");
            return defaultData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user frontend data");
            return defaultData;
        }
    }

    public async Task<int> SubscribeStateChangedAsync(string entityId, StateChangedCallback callback, CancellationToken ct = default)
    {
        if (!IsWebSocketConnected)
            throw new InvalidOperationException("WebSocket not connected");

        var id = Interlocked.Increment(ref _messageId);
        var tcs = new TaskCompletionSource<JsonElement>();
        _pendingRequests[id] = tcs;

        // Subscribe to all state_changed events (HA doesn't support per-entity subscribe via WS)
        await SendMessageAsync(new { type = "subscribe_events", id, event_type = "state_changed" }, ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
        timeoutCts.Token.Register(() => tcs.TrySetCanceled());

        var result = await tcs.Task;

        // Store subscription with entity filter
        _subscriptions[id] = new SubscriptionInfo(entityId, callback);
        _subscriptionEntityFilter[id] = entityId;

        _logger.LogInformation("Subscribed to state_changed for {EntityId} (subscription {Id})", entityId, id);
        return id;
    }

    public async Task UnsubscribeAsync(int subscriptionId, CancellationToken ct = default)
    {
        if (!IsWebSocketConnected) return;

        _subscriptions.TryRemove(subscriptionId, out _);
        _subscriptionEntityFilter.TryRemove(subscriptionId, out _);

        var id = Interlocked.Increment(ref _messageId);
        await SendMessageAsync(new { type = "unsubscribe_events", id, subscription = subscriptionId }, ct);

        _logger.LogInformation("Unsubscribed from subscription {Id}", subscriptionId);
    }

    // ─── WebSocket Internals ────────────────────────────────────

    private async Task WebSocketMessageLoop(CancellationToken ct)
    {
        var buffer = new byte[8192];

        while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            try
            {
                var message = await ReceiveMessageAsync(ct);
                ProcessWebSocketMessage(message);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket connection lost, will attempt reconnect");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket message loop");
            }
        }

        // Attempt reconnect if not intentionally cancelled
        if (!ct.IsCancellationRequested)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                _logger.LogInformation("Attempting WebSocket reconnect...");
                try
                {
                    await ConnectWebSocketAsync(ct);
                    // Re-subscribe after reconnect
                    await ResubscribeAllAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WebSocket reconnect failed, will retry in 30s");
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    // Recursive retry (with backoff built into the loop above)
                }
            }, ct);
        }
    }

    private async Task ResubscribeAllAsync(CancellationToken ct)
    {
        var existingSubs = _subscriptions.ToArray();
        _subscriptions.Clear();
        _subscriptionEntityFilter.Clear();

        foreach (var (_, info) in existingSubs)
        {
            try
            {
                await SubscribeStateChangedAsync(info.EntityId, info.Callback, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resubscribe for {EntityId}", info.EntityId);
            }
        }
    }

    private void ProcessWebSocketMessage(JsonElement message)
    {
        var type = message.GetProperty("type").GetString();

        switch (type)
        {
            case "result":
                if (message.TryGetProperty("id", out var resultId))
                {
                    var id = resultId.GetInt32();
                    if (_pendingRequests.TryRemove(id, out var tcs))
                    {
                        tcs.TrySetResult(message);
                    }
                }
                break;

            case "event":
                HandleEvent(message);
                break;

            case "pong":
                // Keepalive response — ignore
                break;

            default:
                _logger.LogDebug("Unhandled WebSocket message type: {Type}", type);
                break;
        }
    }

    private void HandleEvent(JsonElement message)
    {
        if (!message.TryGetProperty("id", out var subIdEl)) return;
        var subId = subIdEl.GetInt32();

        try
        {
            var eventObj = message.GetProperty("event");
            var eventType = eventObj.TryGetProperty("event_type", out var etProp) ? etProp.GetString() ?? "unknown" : "unknown";

            // ── Monitor: log ALL events to the event buffer ──
            if (subId == _monitorSubscriptionId && IsEventLoggingEnabled)
            {
                LogMonitorEvent(eventType, eventObj);
                return; // Monitor subscription doesn't dispatch to trigger callbacks
            }

            // ── Trigger subscriptions: only state_changed with entity filter ──
            if (!_subscriptions.TryGetValue(subId, out var info)) return;
            if (eventType != "state_changed") return;

            var eventData = eventObj.GetProperty("data");
            var entityId = eventData.GetProperty("entity_id").GetString();
            if (entityId is null || entityId != info.EntityId) return;

            var oldState = eventData.GetProperty("old_state").GetProperty("state").GetString() ?? "";
            var newState = eventData.GetProperty("new_state").GetProperty("state").GetString() ?? "";

            // Also log to event buffer if monitoring
            AddEventLogEntry("state_changed", entityId, oldState, newState);

            if (oldState != newState)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await info.Callback(entityId, oldState, newState);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in state change callback for {EntityId}", entityId);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process event");
        }
    }

    private void LogMonitorEvent(string eventType, JsonElement eventObj)
    {
        try
        {
            if (eventType == "state_changed" && eventObj.TryGetProperty("data", out var data))
            {
                var entityId = data.TryGetProperty("entity_id", out var eid) ? eid.GetString() ?? "" : "";
                var oldState = "";
                var newState = "";

                if (data.TryGetProperty("old_state", out var os) && os.TryGetProperty("state", out var osv))
                    oldState = osv.GetString() ?? "";
                if (data.TryGetProperty("new_state", out var ns) && ns.TryGetProperty("state", out var nsv))
                    newState = nsv.GetString() ?? "";

                AddEventLogEntry(eventType, entityId, oldState, newState);
            }
            else
            {
                // Non-state_changed event: extract what we can
                var entityId = "";
                string? rawData = null;

                if (eventObj.TryGetProperty("data", out var d))
                {
                    if (d.TryGetProperty("entity_id", out var eid))
                        entityId = eid.GetString() ?? "";
                    else if (d.TryGetProperty("domain", out var dom))
                        entityId = dom.GetString() ?? "";

                    // Compact raw data for non-standard events
                    rawData = d.GetRawText();
                    if (rawData.Length > 200)
                        rawData = rawData[..200] + "...";
                }

                AddEventLogEntry(eventType, entityId, null, null, rawData);
            }
        }
        catch
        {
            AddEventLogEntry(eventType, "?", null, null, "(parse error)");
        }
    }

    private async Task SendMessageAsync(object message, CancellationToken ct)
    {
        if (_ws?.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket not connected");

        var json = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        await _ws.SendAsync(json, WebSocketMessageType.Text, true, ct);
    }

    private async Task<JsonElement> ReceiveMessageAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await _ws!.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("Server closed connection");

            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        ms.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(ms, cancellationToken: ct);
    }

    // ─── Internal Types ─────────────────────────────────────────

    private record SubscriptionInfo(string EntityId, StateChangedCallback Callback);

    private class HaStateResponse
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("attributes")]
        public Dictionary<string, object?> Attributes { get; set; } = new();
    }

    public void Dispose()
    {
        _wsLoopCts?.Cancel();
        _ws?.Dispose();
        _wsLock.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task SimulateStateChangeAsync(string entityId, string oldState, string newState)
    {
        // Also log to event buffer
        AddEventLogEntry("state_changed", entityId, oldState, newState);

        var matchingCallbacks = _subscriptions.Values
            .Where(s => string.Equals(s.EntityId, entityId, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Callback)
            .ToList();

        _logger.LogInformation("SimulateStateChange: {Entity} {Old} → {New} ({Count} callbacks)",
            entityId, oldState, newState, matchingCallbacks.Count);

        foreach (var callback in matchingCallbacks)
        {
            try
            {
                await callback(entityId, oldState, newState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simulated state change callback for {EntityId}", entityId);
            }
        }
    }

    public IReadOnlyList<HaEventLogEntry> GetRecentEvents()
    {
        lock (_eventLogLock)
        {
            return _eventLog.Reverse().ToList(); // newest first
        }
    }

    public void ClearEventLog()
    {
        lock (_eventLogLock)
        {
            _eventLog.Clear();
        }
    }

    public async Task StartEventMonitorAsync(CancellationToken ct = default)
    {
        if (!IsWebSocketConnected)
        {
            _logger.LogWarning("Cannot start event monitor: WebSocket not connected");
            return;
        }

        if (_monitorSubscriptionId != 0) return; // Already monitoring

        var id = Interlocked.Increment(ref _messageId);
        var tcs = new TaskCompletionSource<JsonElement>();
        _pendingRequests[id] = tcs;

        // Subscribe to ALL events (no event_type filter)
        await SendMessageAsync(new { type = "subscribe_events", id }, ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
        timeoutCts.Token.Register(() => tcs.TrySetCanceled());

        await tcs.Task;
        _monitorSubscriptionId = id;
        IsEventLoggingEnabled = true;

        _logger.LogInformation("Event monitor started (subscription {Id})", id);
    }

    public async Task StopEventMonitorAsync(CancellationToken ct = default)
    {
        if (_monitorSubscriptionId == 0) return;

        if (IsWebSocketConnected)
        {
            var id = Interlocked.Increment(ref _messageId);
            await SendMessageAsync(new { type = "unsubscribe_events", id, subscription = _monitorSubscriptionId }, ct);
        }

        _logger.LogInformation("Event monitor stopped (subscription {Id})", _monitorSubscriptionId);
        _monitorSubscriptionId = 0;
        IsEventLoggingEnabled = false;
    }

    private void AddEventLogEntry(string eventType, string entityId, string? oldState, string? newState, string? rawData = null)
    {
        if (!IsEventLoggingEnabled) return;

        var entry = new HaEventLogEntry(DateTime.Now, eventType, entityId, oldState, newState, rawData);
        lock (_eventLogLock)
        {
            _eventLog.AddLast(entry);
            while (_eventLog.Count > MaxEventLogEntries)
                _eventLog.RemoveFirst();
        }
    }
}
