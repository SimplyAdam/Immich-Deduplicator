namespace ImmichDeduplicator.Services;

/// <summary>
/// Handles plain text logging to file
/// </summary>
public class FileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly bool _verbose;
    private readonly object _lock = new();

    public FileLogger(string logPath, bool verbose)
    {
        _verbose = verbose;
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create or overwrite the log file
        _writer = new StreamWriter(logPath, append: false)
        {
            AutoFlush = true
        };

        WriteHeader();
    }

    private void WriteHeader()
    {
        _writer.WriteLine("=".PadRight(80, '='));
        _writer.WriteLine("IMMICH DEDUPLICATOR LOG");
        _writer.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _writer.WriteLine("=".PadRight(80, '='));
        _writer.WriteLine();
    }

    public void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    public void LogSuccess(string message)
    {
        WriteLog("SUCCESS", message);
    }

    public void LogWarning(string message)
    {
        WriteLog("WARNING", message);
    }

    public void LogError(string message)
    {
        WriteLog("ERROR", message);
    }

    public void LogVerbose(string message)
    {
        if (_verbose)
        {
            WriteLog("VERBOSE", message);
        }
    }

    public void LogAction(string action, string assetId, string details)
    {
        WriteLog("ACTION", $"{action} | Asset: {assetId} | {details}");
    }

    public void LogDuplicate(int index, string duplicateId, string category, List<string> assetIds, string action)
    {
        lock (_lock)
        {
            _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] DUPLICATE #{index}: {duplicateId}");
            _writer.WriteLine($"  Index: {index}");
            _writer.WriteLine($"  Category: {category}");
            _writer.WriteLine($"  Assets: {string.Join(", ", assetIds)}");
            _writer.WriteLine($"  Action: {action}");
            _writer.WriteLine();
        }
    }

    public void LogUnchanged(int index, string duplicateId, List<(string Id, string FileName, string Checksum, string Extension, DateTime CreatedAt, long Size)> assets)
    {
        lock (_lock)
        {
            _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] UNCHANGED DUPLICATE #{index}: {duplicateId}");
            _writer.WriteLine($"  Index: {index}");
            _writer.WriteLine("  Assets:");
            foreach (var asset in assets)
            {
                _writer.WriteLine($"    - ID: {asset.Id}");
                _writer.WriteLine($"      File: {asset.FileName}");
                _writer.WriteLine($"      Checksum: {asset.Checksum}");
                _writer.WriteLine($"      Extension: {asset.Extension}");
                _writer.WriteLine($"      Created: {asset.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                _writer.WriteLine($"      Size: {asset.Size:N0} bytes");
            }
            _writer.WriteLine("  Reason: Does not match any processing criteria");
            _writer.WriteLine();
        }
    }

    public void LogSummary(int processed, int deleted, int stacked, int albumsUpdated, int unchanged, bool isDryRun)
    {
        lock (_lock)
        {
            _writer.WriteLine();
            _writer.WriteLine("=".PadRight(80, '='));
            _writer.WriteLine("SUMMARY");
            _writer.WriteLine("=".PadRight(80, '='));
            _writer.WriteLine($"Mode: {(isDryRun ? "DRY RUN" : "LIVE")}");
            _writer.WriteLine($"Duplicates Processed: {processed}");
            _writer.WriteLine($"Assets Deleted: {deleted}");
            _writer.WriteLine($"Stacks Created: {stacked}");
            _writer.WriteLine($"Album Assignments Updated: {albumsUpdated}");
            _writer.WriteLine($"Unchanged: {unchanged}");
            _writer.WriteLine($"Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _writer.WriteLine("=".PadRight(80, '='));
        }
    }

    private void WriteLog(string level, string message)
    {
        lock (_lock)
        {
            _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
