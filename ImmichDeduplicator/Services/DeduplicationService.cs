using ImmichDeduplicator.Models;

namespace ImmichDeduplicator.Services;

/// <summary>
/// Options to skip processing specific categories
/// </summary>
public record SkipOptions(bool SkipChecksum, bool SkipExtension, bool SkipNameDate, bool SkipBurst, bool SkipSameTime);

/// <summary>
/// Handles the logic for categorizing and processing duplicate assets
/// </summary>
public class DeduplicationService
{
    private readonly ImmichApiClient _apiClient;
    private readonly ConsoleUI _ui;
    private readonly FileLogger _logger;
    private readonly bool _isDryRun;
    private readonly SkipOptions _skipOptions;

    public DeduplicationService(ImmichApiClient apiClient, ConsoleUI ui, FileLogger logger, bool isDryRun, SkipOptions skipOptions)
    {
        _apiClient = apiClient;
        _ui = ui;
        _logger = logger;
        _isDryRun = isDryRun;
        _skipOptions = skipOptions;
    }

    /// <summary>
    /// Categorizes duplicate groups based on the defined criteria
    /// </summary>
    public CategorizedDuplicates CategorizeDuplicates(List<DuplicateGroup> duplicates)
    {
        var result = new CategorizedDuplicates();

        for (int i = 0; i < duplicates.Count; i++)
        {
            var group = duplicates[i];
            group.OriginalIndex = i;
            var category = DetermineCategory(group);
            
            // If category is skipped, treat as unchanged
            bool isSkipped = category switch
            {
                DuplicateCategory.SameChecksumSameDate => _skipOptions.SkipChecksum,
                DuplicateCategory.DifferentExtension => _skipOptions.SkipExtension,
                DuplicateCategory.SameNameSameDate => _skipOptions.SkipNameDate,
                DuplicateCategory.BurstPhotos => _skipOptions.SkipBurst,
                DuplicateCategory.SameTimeDifferentName => _skipOptions.SkipSameTime,
                _ => false
            };

            if (isSkipped)
            {
                result.Unchanged.Add(group);
                continue;
            }
            
            switch (category)
            {
                case DuplicateCategory.SameChecksumSameDate:
                    result.SameChecksumSameDate.Add(group);
                    break;
                case DuplicateCategory.DifferentExtension:
                    result.DifferentExtension.Add(group);
                    break;
                case DuplicateCategory.SameNameSameDate:
                    result.SameNameSameDate.Add(group);
                    break;
                case DuplicateCategory.BurstPhotos:
                    result.BurstPhotos.Add(group);
                    break;
                case DuplicateCategory.SameTimeDifferentName:
                    result.SameTimeDifferentName.Add(group);
                    break;
                default:
                    result.Unchanged.Add(group);
                    break;
            }
        }

        return result;
    }

    private DuplicateCategory DetermineCategory(DuplicateGroup group)
    {
        if (group.Assets.Count < 2)
            return DuplicateCategory.Unchanged;

        var assets = group.Assets;
        
        // Check if all assets have the same checksum and same creation date (identical files)
        // Use LocalDateTime to avoid timezone issues
        var firstChecksum = assets[0].Checksum;
        var firstDate = assets[0].LocalDateTime.Date;
        
        bool allSameChecksum = assets.All(a => a.Checksum == firstChecksum);
        bool allSameDate = assets.All(a => a.LocalDateTime.Date == firstDate);

        if (allSameChecksum && allSameDate)
        {
            return DuplicateCategory.SameChecksumSameDate;
        }

        // Check for same name same date (works for any number of assets)
        var firstName = GetFileNameWithoutExtension(assets[0].OriginalFileName);
        bool allSameName = assets.All(a => GetFileNameWithoutExtension(a.OriginalFileName).Equals(firstName, StringComparison.OrdinalIgnoreCase));
        
        // Also check all extensions are the same for SameNameSameDate (to avoid mixing JPG+NEF cases)
        var firstExt = GetExtension(assets[0].OriginalFileName);
        bool allSameExtension = assets.All(a => GetExtension(a.OriginalFileName).Equals(firstExt, StringComparison.OrdinalIgnoreCase));
        
        if (allSameName && allSameDate && allSameExtension)
        {
            return DuplicateCategory.SameNameSameDate;
        }

        // For pairs only (more specific matching)
        if (assets.Count == 2)
        {
            var ext1 = GetExtension(assets[0].OriginalFileName);
            var ext2 = GetExtension(assets[1].OriginalFileName);
            // Use LocalDateTime for comparisons to avoid timezone issues
            var date1 = assets[0].LocalDateTime.Date;
            var date2 = assets[1].LocalDateTime.Date;
            var time1 = assets[0].LocalDateTime;
            var time2 = assets[1].LocalDateTime;
            var name1 = GetFileNameWithoutExtension(assets[0].OriginalFileName);
            var name2 = GetFileNameWithoutExtension(assets[1].OriginalFileName);
            bool sameExtension = ext1.Equals(ext2, StringComparison.OrdinalIgnoreCase);
            bool sameDate = date1 == date2;
            bool sameName = name1.Equals(name2, StringComparison.OrdinalIgnoreCase);
            // Compare times at second precision (ignore milliseconds)
            var timeDiff = Math.Abs((time1 - time2).TotalSeconds);
            bool sameTime = timeDiff < 1; // Within 1 second = same time
            
            // Different file extensions with same creation date -> create stack
            if (!sameExtension && sameDate)
            {
                return DuplicateCategory.DifferentExtension;
            }

            // Same time different names: different names but same extension, date, and same time (within 1 sec) -> keep largest
            if (!sameName && sameExtension && sameDate && sameTime)
            {
                return DuplicateCategory.SameTimeDifferentName;
            }

            // Burst photos: same extension, same date, time within 5 seconds (but not exact), similar naming pattern
            if (sameExtension && !sameName && sameDate && !sameTime && timeDiff <= 5 && AreBurstNames(name1, name2))
            {
                return DuplicateCategory.BurstPhotos;
            }
        }

        return DuplicateCategory.Unchanged;
    }

    /// <summary>
    /// Checks if two filenames appear to be from a burst/sequence (e.g., IMG_001 and IMG_002, or PXL_20230725_094339303 and PXL_20230725_094340497)
    /// </summary>
    private static bool AreBurstNames(string name1, string name2)
    {
        // First check: strictly sequential numbers (differ by 1)
        var num1 = ExtractTrailingNumber(name1);
        var num2 = ExtractTrailingNumber(name2);
        
        if (num1 != null && num2 != null && Math.Abs(num1.Value - num2.Value) == 1)
            return true;

        // Second check: same prefix pattern with different numeric suffixes
        // This handles cases like PXL_20230725_094339303 and PXL_20230725_094340497
        var prefix1 = ExtractNonNumericPrefix(name1);
        var prefix2 = ExtractNonNumericPrefix(name2);
        
        if (!string.IsNullOrEmpty(prefix1) && prefix1.Equals(prefix2, StringComparison.OrdinalIgnoreCase))
        {
            // Same prefix, different numbers = likely burst
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts the non-numeric prefix from a filename (everything before the last number sequence)
    /// </summary>
    private static string ExtractNonNumericPrefix(string name)
    {
        // Find where the trailing number starts
        int endIndex = name.Length - 1;
        while (endIndex >= 0 && !char.IsDigit(name[endIndex]))
        {
            endIndex--;
        }
        
        if (endIndex < 0)
            return name; // No numbers found

        int startIndex = endIndex;
        while (startIndex > 0 && char.IsDigit(name[startIndex - 1]))
        {
            startIndex--;
        }

        return name.Substring(0, startIndex);
    }

    /// <summary>
    /// Extracts trailing number from a filename
    /// </summary>
    private static int? ExtractTrailingNumber(string name)
    {
        // Find the last sequence of digits in the name
        int endIndex = name.Length - 1;
        while (endIndex >= 0 && !char.IsDigit(name[endIndex]))
        {
            endIndex--;
        }
        
        if (endIndex < 0)
            return null;

        int startIndex = endIndex;
        while (startIndex > 0 && char.IsDigit(name[startIndex - 1]))
        {
            startIndex--;
        }

        var numberStr = name.Substring(startIndex, endIndex - startIndex + 1);
        if (int.TryParse(numberStr, out int result))
            return result;
        
        return null;
    }

    /// <summary>
    /// Processes all categorized duplicates
    /// </summary>
    public async Task<(int Processed, int Deleted, int Stacked, int AlbumsUpdated, int Unchanged)> ProcessDuplicatesAsync(
        CategorizedDuplicates categories, 
        CancellationToken cancellationToken)
    {
        int processed = 0;
        int deleted = 0;
        int stacked = 0;
        int albumsUpdated = 0;

        int total = categories.SameChecksumSameDate.Count + categories.DifferentExtension.Count + 
                    categories.SameNameSameDate.Count + categories.BurstPhotos.Count + categories.SameTimeDifferentName.Count;

        if (total == 0)
        {
            _ui.LogInfo("No duplicates to process.");
            return (0, 0, 0, 0, categories.Unchanged.Count);
        }

        _ui.StartStatusLine();

        // Process same checksum same date (identical files)
        foreach (var group in categories.SameChecksumSameDate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _ui.UpdateStatus($"[cyan]Processing[/] #{group.OriginalIndex} [dim]({processed + 1}/{total})[/] - Same checksum: [white]{group.Assets[0].OriginalFileName}[/]");
            
            var result = await ProcessSameChecksumSameDateAsync(group);
            processed++;
            deleted += result.Deleted;
            albumsUpdated += result.AlbumsUpdated;
        }

        // Process different extensions (create stacks)
        foreach (var group in categories.DifferentExtension)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _ui.UpdateStatus($"[magenta]Processing[/] #{group.OriginalIndex} [dim]({processed + 1}/{total})[/] - Stacking: [white]{group.Assets[0].OriginalFileName}[/]");
            
            var wasStacked = await ProcessDifferentExtensionAsync(group);
            processed++;
            if (wasStacked) stacked++;
        }

        // Process same name same date
        foreach (var group in categories.SameNameSameDate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _ui.UpdateStatus($"[yellow]Processing[/] #{group.OriginalIndex} [dim]({processed + 1}/{total})[/] - Same name/date: [white]{group.Assets[0].OriginalFileName}[/]");
            
            var result = await ProcessSameNameSameDateAsync(group);
            processed++;
            deleted += result.Deleted;
            albumsUpdated += result.AlbumsUpdated;
        }

        // Process burst photos (create stacks)
        foreach (var group in categories.BurstPhotos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _ui.UpdateStatus($"[green]Processing[/] #{group.OriginalIndex} [dim]({processed + 1}/{total})[/] - Burst: [white]{group.Assets[0].OriginalFileName}[/]");
            
            var wasStacked = await ProcessBurstPhotosAsync(group);
            processed++;
            if (wasStacked) stacked++;
        }

        // Process same time different name (keep largest)
        foreach (var group in categories.SameTimeDifferentName)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _ui.UpdateStatus($"[blue]Processing[/] #{group.OriginalIndex} [dim]({processed + 1}/{total})[/] - Same time: [white]{group.Assets[0].OriginalFileName}[/]");
            
            var result = await ProcessSameTimeDifferentNameAsync(group);
            processed++;
            deleted += result.Deleted;
            albumsUpdated += result.AlbumsUpdated;
        }

        // Log unchanged duplicates
        foreach (var group in categories.Unchanged)
        {
            LogUnchangedDuplicate(group);
        }

        _ui.ClearStatusLine();

        return (processed, deleted, stacked, albumsUpdated, categories.Unchanged.Count);
    }

    private async Task<(int Deleted, int AlbumsUpdated)> ProcessSameChecksumSameDateAsync(DuplicateGroup group)
    {
        _logger.LogVerbose($"Processing same checksum group #{group.OriginalIndex}: {group.DuplicateId}");

        // Find the asset with most album assignments
        var assetAlbumCounts = new Dictionary<string, List<Album>>();
        
        foreach (var asset in group.Assets)
        {
            var albums = await _apiClient.GetAlbumsForAssetAsync(asset.Id);
            assetAlbumCounts[asset.Id] = albums;
        }

        // Select the one with most albums as keeper
        var keeper = group.Assets
            .OrderByDescending(a => assetAlbumCounts[a.Id].Count)
            .ThenByDescending(a => a.IsFavorite) // Prefer favorites
            .First();

        var toDelete = group.Assets.Where(a => a.Id != keeper.Id).ToList();
        
        // Collect all albums from assets to delete
        var albumsToAdd = new HashSet<string>();
        foreach (var asset in toDelete)
        {
            foreach (var album in assetAlbumCounts[asset.Id])
            {
                if (!assetAlbumCounts[keeper.Id].Any(a => a.Id == album.Id))
                {
                    albumsToAdd.Add(album.Id);
                }
            }
        }

        // Log the action
        _logger.LogDuplicate(
            group.OriginalIndex,
            group.DuplicateId,
            "SameChecksumSameDate",
            group.Assets.Select(a => a.Id).ToList(),
            $"Keep: {keeper.Id} ({keeper.OriginalFileName}), Delete: {toDelete.Count} assets, Add to {albumsToAdd.Count} albums"
        );

        if (!_isDryRun)
        {
            // Add keeper to albums
            foreach (var albumId in albumsToAdd)
            {
                await _apiClient.AddAssetsToAlbumAsync(albumId, [keeper.Id]);
            }

            // Delete the duplicates
            await _apiClient.DeleteAssetsAsync(toDelete.Select(a => a.Id).ToList());
        }

        return (toDelete.Count, albumsToAdd.Count);
    }

    private async Task<bool> ProcessDifferentExtensionAsync(DuplicateGroup group)
    {
        _logger.LogVerbose($"Processing different extension group #{group.OriginalIndex}: {group.DuplicateId}");

        var assetIds = group.Assets.Select(a => a.Id).ToList();
        var fileNames = string.Join(", ", group.Assets.Select(a => a.OriginalFileName));

        _logger.LogDuplicate(
            group.OriginalIndex,
            group.DuplicateId,
            "DifferentExtension",
            assetIds,
            $"Create stack with assets: {fileNames}"
        );

        if (!_isDryRun)
        {
            var stack = await _apiClient.CreateStackAsync(assetIds);
            if (stack != null)
            {
                return true;
            }
            else
            {
                _logger.LogError($"Failed to create stack for group #{group.OriginalIndex} {group.DuplicateId}");
                return false;
            }
        }

        return true;
    }

    private async Task<(int Deleted, int AlbumsUpdated)> ProcessSameNameSameDateAsync(DuplicateGroup group)
    {
        _logger.LogVerbose($"Processing same name/date group #{group.OriginalIndex}: {group.DuplicateId}");

        // Keep the largest file
        var keeper = group.Assets
            .OrderByDescending(a => a.ExifInfo?.FileSizeInByte ?? 0)
            .ThenByDescending(a => a.IsFavorite)
            .First();

        var toDelete = group.Assets.Where(a => a.Id != keeper.Id).ToList();

        // Get albums from asset to delete and add keeper to them
        var albumsToAdd = new HashSet<string>();
        foreach (var asset in toDelete)
        {
            var albums = await _apiClient.GetAlbumsForAssetAsync(asset.Id);
            foreach (var album in albums)
            {
                albumsToAdd.Add(album.Id);
            }
        }

        _logger.LogDuplicate(
            group.OriginalIndex,
            group.DuplicateId,
            "SameNameSameDate",
            group.Assets.Select(a => a.Id).ToList(),
            $"Keep largest: {keeper.Id} ({keeper.OriginalFileName}, {keeper.ExifInfo?.FileSizeInByte:N0} bytes), Delete: {toDelete.Count} assets"
        );

        if (!_isDryRun)
        {
            // Add keeper to albums
            foreach (var albumId in albumsToAdd)
            {
                await _apiClient.AddAssetsToAlbumAsync(albumId, [keeper.Id]);
            }

            // Delete the smaller duplicates
            await _apiClient.DeleteAssetsAsync(toDelete.Select(a => a.Id).ToList());
        }

        return (toDelete.Count, albumsToAdd.Count);
    }

    private async Task<bool> ProcessBurstPhotosAsync(DuplicateGroup group)
    {
        _logger.LogVerbose($"Processing burst photos group #{group.OriginalIndex}: {group.DuplicateId}");

        var assetIds = group.Assets.Select(a => a.Id).ToList();
        var fileNames = string.Join(", ", group.Assets.Select(a => a.OriginalFileName));

        _logger.LogDuplicate(
            group.OriginalIndex,
            group.DuplicateId,
            "BurstPhotos",
            assetIds,
            $"Create stack with burst photos: {fileNames}"
        );

        if (!_isDryRun)
        {
            var stack = await _apiClient.CreateStackAsync(assetIds);
            if (stack != null)
            {
                return true;
            }
            else
            {
                _logger.LogError($"Failed to create stack for burst group #{group.OriginalIndex} {group.DuplicateId}");
                return false;
            }
        }

        return true;
    }

    private async Task<(int Deleted, int AlbumsUpdated)> ProcessSameTimeDifferentNameAsync(DuplicateGroup group)
    {
        _logger.LogVerbose($"Processing same time/different name group #{group.OriginalIndex}: {group.DuplicateId}");

        // Keep the largest file
        var keeper = group.Assets
            .OrderByDescending(a => a.ExifInfo?.FileSizeInByte ?? 0)
            .ThenByDescending(a => a.IsFavorite)
            .First();

        var toDelete = group.Assets.Where(a => a.Id != keeper.Id).ToList();

        // Get albums from asset to delete and add keeper to them
        var albumsToAdd = new HashSet<string>();
        foreach (var asset in toDelete)
        {
            var albums = await _apiClient.GetAlbumsForAssetAsync(asset.Id);
            foreach (var album in albums)
            {
                albumsToAdd.Add(album.Id);
            }
        }

        _logger.LogDuplicate(
            group.OriginalIndex,
            group.DuplicateId,
            "SameTimeDifferentName",
            group.Assets.Select(a => a.Id).ToList(),
            $"Keep largest: {keeper.Id} ({keeper.OriginalFileName}, {keeper.ExifInfo?.FileSizeInByte:N0} bytes), Delete: {toDelete.Count} assets"
        );

        if (!_isDryRun)
        {
            // Add keeper to albums
            foreach (var albumId in albumsToAdd)
            {
                await _apiClient.AddAssetsToAlbumAsync(albumId, [keeper.Id]);
            }

            // Delete the smaller duplicates
            await _apiClient.DeleteAssetsAsync(toDelete.Select(a => a.Id).ToList());
        }

        return (toDelete.Count, albumsToAdd.Count);
    }

    private void LogUnchangedDuplicate(DuplicateGroup group)
    {
        var assetDetails = group.Assets.Select(a => (
            Id: a.Id,
            FileName: a.OriginalFileName,
            Checksum: a.Checksum,
            Extension: GetExtension(a.OriginalFileName),
            CreatedAt: a.FileCreatedAt,
            Size: a.ExifInfo?.FileSizeInByte ?? 0
        )).ToList();

        _logger.LogUnchanged(group.OriginalIndex, group.DuplicateId, assetDetails);
    }

    private static string GetExtension(string fileName)
    {
        return Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
    }

    private static string GetFileNameWithoutExtension(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName);
    }
}
