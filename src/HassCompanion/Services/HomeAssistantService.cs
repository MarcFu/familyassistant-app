using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using HassCompanion.Configuration;
using Microsoft.Extensions.Options;

namespace HassCompanion.Services;

public class HomeAssistantService : IHomeAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HomeAssistantService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HomeAssistantService(HttpClient httpClient, IOptions<HomeAssistantOptions> options, ILogger<HomeAssistantService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var config = options.Value;
        _httpClient.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/api/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<HaPerson>> GetPersonsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("states", ct);
            response.EnsureSuccessStatusCode();

            var states = await response.Content.ReadFromJsonAsync<List<HaStateResponse>>(JsonOptions, ct);
            if (states is null) return [];

            return states
                .Where(s => s.EntityId.StartsWith("person."))
                .Select(s => new HaPerson(
                    s.EntityId,
                    s.Attributes.TryGetValue("friendly_name", out var name) ? name?.ToString() ?? s.EntityId : s.EntityId,
                    s.State,
                    s.Attributes.TryGetValue("entity_picture", out var pic) ? pic?.ToString() : null))
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
            var response = await _httpClient.GetAsync($"states/{entityId}", ct);
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
            var payload = new
            {
                state,
                attributes = attributes ?? new Dictionary<string, object?>()
            };

            var response = await _httpClient.PostAsJsonAsync($"states/{entityId}", payload, JsonOptions, ct);
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
            var response = await _httpClient.PostAsJsonAsync($"services/{domain}/{service}", data ?? new { }, JsonOptions, ct);
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
            var response = await _httpClient.GetAsync("", ct);
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
            // path comes as relative from HA (e.g., "/api/image/serve/xxx/512x512")
            // We need to call it relative to the base URL (without /api/ prefix since our baseAddress already includes it)
            var url = path.TrimStart('/');
            if (url.StartsWith("api/"))
                url = url[4..]; // Remove "api/" since base address already has it

            var response = await _httpClient.GetAsync(url, ct);
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
            var response = await _httpClient.GetAsync("states", ct);
            response.EnsureSuccessStatusCode();

            var states = await response.Content.ReadFromJsonAsync<List<HaStateResponse>>(JsonOptions, ct);
            if (states is null) return [];

            return states
                .Where(s => s.EntityId.StartsWith($"{domain}."))
                .Select(s => new HaEntityState(
                    s.EntityId,
                    s.State,
                    s.Attributes))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entities for domain {Domain}", domain);
            return [];
        }
    }

    private class HaStateResponse
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; } = "";

        [JsonPropertyName("state")]
        public string State { get; set; } = "";

        [JsonPropertyName("attributes")]
        public Dictionary<string, object?> Attributes { get; set; } = new();
    }
}
