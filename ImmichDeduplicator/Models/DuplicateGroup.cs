namespace ImmichDeduplicator.Models;

/// <summary>
/// Represents a group of duplicate assets from the Immich API
/// </summary>
public class DuplicateGroup
{
    public string DuplicateId { get; set; } = string.Empty;
    public List<Asset> Assets { get; set; } = [];
    
    /// <summary>
    /// The original index in the duplicates array from the API (for reference in Immich UI)
    /// </summary>
    public int OriginalIndex { get; set; }
}

/// <summary>
/// Represents an asset from the Immich API
/// </summary>
public class Asset
{
    public string Id { get; set; } = string.Empty;
    public string DeviceAssetId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalMimeType { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public string? DuplicateId { get; set; }
    public DateTime FileCreatedAt { get; set; }
    public DateTime FileModifiedAt { get; set; }
    public DateTime LocalDateTime { get; set; }
    public long FileSize { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public bool IsTrashed { get; set; }
    public string Type { get; set; } = string.Empty;
    public ExifInfo? ExifInfo { get; set; }
}

/// <summary>
/// EXIF metadata for an asset
/// </summary>
public class ExifInfo
{
    public long? FileSizeInByte { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Represents an album from the Immich API
/// </summary>
public class Album
{
    public string Id { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public int AssetCount { get; set; }
}

/// <summary>
/// Categories for duplicate processing
/// </summary>
public enum DuplicateCategory
{
    SameChecksumSameDate,
    DifferentExtension,
    SameNameSameDate,
    BurstPhotos,
    SameTimeDifferentName,
    Unchanged
}

/// <summary>
/// Result of categorizing duplicates
/// </summary>
public class CategorizedDuplicates
{
    public List<DuplicateGroup> SameChecksumSameDate { get; set; } = [];
    public List<DuplicateGroup> DifferentExtension { get; set; } = [];
    public List<DuplicateGroup> SameNameSameDate { get; set; } = [];
    public List<DuplicateGroup> BurstPhotos { get; set; } = [];
    public List<DuplicateGroup> SameTimeDifferentName { get; set; } = [];
    public List<DuplicateGroup> Unchanged { get; set; } = [];
    
    public int TotalCount => SameChecksumSameDate.Count + DifferentExtension.Count + SameNameSameDate.Count + BurstPhotos.Count + SameTimeDifferentName.Count + Unchanged.Count;
}
