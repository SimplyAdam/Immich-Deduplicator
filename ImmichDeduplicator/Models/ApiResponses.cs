namespace ImmichDeduplicator.Models;

/// <summary>
/// Response from the validate token endpoint
/// </summary>
public class ValidateTokenResponse
{
    public bool AuthStatus { get; set; }
}

/// <summary>
/// Response from the server version endpoint
/// </summary>
public class ServerVersionResponse
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }
}

/// <summary>
/// Response DTO for duplicates endpoint
/// </summary>
public class DuplicateResponseDto
{
    public string DuplicateId { get; set; } = string.Empty;
    public List<Asset> Assets { get; set; } = [];
}

/// <summary>
/// Request body for bulk operations
/// </summary>
public class BulkIdsDto
{
    public List<string> Ids { get; set; } = [];
}

/// <summary>
/// Request body for creating a stack
/// </summary>
public class StackCreateDto
{
    public List<string> AssetIds { get; set; } = [];
}

/// <summary>
/// Response from stack creation
/// </summary>
public class StackResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string PrimaryAssetId { get; set; } = string.Empty;
    public List<string> AssetIds { get; set; } = [];
}

/// <summary>
/// Request body for adding assets to albums
/// </summary>
public class AlbumsAddAssetsDto
{
    public Dictionary<string, List<string>> Albums { get; set; } = [];
}

/// <summary>
/// Response from adding assets to albums
/// </summary>
public class AlbumsAddAssetsResponseDto
{
    public List<BulkIdResponseDto> Albums { get; set; } = [];
}

/// <summary>
/// Response for bulk ID operations
/// </summary>
public class BulkIdResponseDto
{
    public string Id { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
