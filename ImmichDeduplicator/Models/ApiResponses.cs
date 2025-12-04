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
