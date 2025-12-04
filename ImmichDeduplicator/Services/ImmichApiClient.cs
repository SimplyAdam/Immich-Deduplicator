using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImmichDeduplicator.Models;

namespace ImmichDeduplicator.Services;

/// <summary>
/// Client for interacting with the Immich API
/// </summary>
public class ImmichApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;

    public ImmichApiClient(string serverUrl, string apiKey)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _apiKey = apiKey;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_serverUrl)
        };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Validates the API key and server connection
    /// </summary>
    public async Task<(bool IsValid, string? ServerVersion, string? Error)> ValidateConnectionAsync()
    {
        try
        {
            // First check if we can reach the server and get version
            var versionResponse = await _httpClient.GetAsync("/api/server/version");
            if (!versionResponse.IsSuccessStatusCode)
            {
                return (false, null, $"Failed to reach server: {versionResponse.StatusCode}");
            }

            var version = await versionResponse.Content.ReadFromJsonAsync<ServerVersionResponse>(_jsonOptions);
            var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Patch}" : "Unknown";

            // Then validate the API key
            var authResponse = await _httpClient.PostAsync("/api/auth/validateToken", null);
            if (!authResponse.IsSuccessStatusCode)
            {
                return (false, versionString, $"Invalid API key: {authResponse.StatusCode}");
            }

            return (true, versionString, null);
        }
        catch (HttpRequestException ex)
        {
            return (false, null, $"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, null, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all duplicate groups from the server
    /// </summary>
    public async Task<List<DuplicateGroup>> GetDuplicatesAsync()
    {
        var response = await _httpClient.GetAsync("/api/duplicates");
        response.EnsureSuccessStatusCode();

        var duplicates = await response.Content.ReadFromJsonAsync<List<DuplicateResponseDto>>(_jsonOptions);
        
        return duplicates?.Select(d => new DuplicateGroup
        {
            DuplicateId = d.DuplicateId,
            Assets = d.Assets
        }).ToList() ?? [];
    }

    /// <summary>
    /// Gets all albums that contain a specific asset
    /// </summary>
    public async Task<List<Album>> GetAlbumsForAssetAsync(string assetId)
    {
        var response = await _httpClient.GetAsync($"/api/albums?assetId={assetId}");
        response.EnsureSuccessStatusCode();

        var albums = await response.Content.ReadFromJsonAsync<List<Album>>(_jsonOptions);
        return albums ?? [];
    }

    /// <summary>
    /// Adds assets to albums
    /// </summary>
    public async Task<bool> AddAssetsToAlbumsAsync(Dictionary<string, List<string>> albumAssets)
    {
        var request = new AlbumsAddAssetsDto { Albums = albumAssets };
        var response = await _httpClient.PostAsJsonAsync("/api/albums/assets", request, _jsonOptions);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Adds assets to a specific album
    /// </summary>
    public async Task<bool> AddAssetsToAlbumAsync(string albumId, List<string> assetIds)
    {
        var request = new BulkIdsDto { Ids = assetIds };
        var response = await _httpClient.PutAsJsonAsync($"/api/albums/{albumId}/assets", request, _jsonOptions);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Creates a stack from the given asset IDs
    /// </summary>
    public async Task<StackResponseDto?> CreateStackAsync(List<string> assetIds)
    {
        var request = new StackCreateDto { AssetIds = assetIds };
        var response = await _httpClient.PostAsJsonAsync("/api/stacks", request, _jsonOptions);
        
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<StackResponseDto>(_jsonOptions);
    }

    /// <summary>
    /// Deletes assets by their IDs (moves to trash)
    /// </summary>
    public async Task<bool> DeleteAssetsAsync(List<string> assetIds)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/assets")
        {
            Content = JsonContent.Create(new BulkIdsDto { Ids = assetIds }, options: _jsonOptions)
        };
        
        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Deletes duplicate records (not the assets themselves)
    /// </summary>
    public async Task<bool> DeleteDuplicateRecordsAsync(List<string> duplicateIds)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/duplicates")
        {
            Content = JsonContent.Create(new BulkIdsDto { Ids = duplicateIds }, options: _jsonOptions)
        };
        
        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Gets detailed information about a specific asset
    /// </summary>
    public async Task<Asset?> GetAssetAsync(string assetId)
    {
        var response = await _httpClient.GetAsync($"/api/assets/{assetId}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<Asset>(_jsonOptions);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
