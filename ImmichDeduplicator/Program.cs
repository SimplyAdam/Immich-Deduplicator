using System.CommandLine;
using Spectre.Console;
using ImmichDeduplicator.Services;

namespace ImmichDeduplicator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Define command-line options
        var serverOption = new Option<string>(
            name: "--server",
            description: "Immich server URL (e.g., https://your-immich-server.com)")
        {
            IsRequired = true
        };

        var apiKeyOption = new Option<string>(
            name: "--api-key",
            description: "Immich API key")
        {
            IsRequired = true
        };

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Run without making any actual changes",
            getDefaultValue: () => false);

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Enable detailed logging output",
            getDefaultValue: () => false);

        var logPathOption = new Option<string>(
            name: "--log-path",
            description: "Specify custom path for the log file",
            getDefaultValue: () => "deduplication.log");

        var limitOption = new Option<int?>(
            name: "--limit",
            description: "Limit the number of duplicates to process (useful for testing)",
            getDefaultValue: () => null);

        var skipChecksumOption = new Option<bool>(
            name: "--skip-checksum",
            description: "Skip processing duplicates with same checksum & date",
            getDefaultValue: () => false);

        var skipExtensionOption = new Option<bool>(
            name: "--skip-extension",
            description: "Skip processing duplicates with different extensions (no stacking)",
            getDefaultValue: () => false);

        var skipNameDateOption = new Option<bool>(
            name: "--skip-name-date",
            description: "Skip processing duplicates with same name & date",
            getDefaultValue: () => false);

        var skipBurstOption = new Option<bool>(
            name: "--skip-burst",
            description: "Skip processing burst photos (sequential names, close timestamps)",
            getDefaultValue: () => false);

        var skipSameTimeOption = new Option<bool>(
            name: "--skip-same-time",
            description: "Skip processing duplicates with same time but different names",
            getDefaultValue: () => false);

        var rootCommand = new RootCommand("Immich Deduplicator - Identify and remove duplicate media files")
        {
            serverOption,
            apiKeyOption,
            dryRunOption,
            verboseOption,
            logPathOption,
            limitOption,
            skipChecksumOption,
            skipExtensionOption,
            skipNameDateOption,
            skipBurstOption,
            skipSameTimeOption
        };

        rootCommand.SetHandler(async (context) =>
        {
            var server = context.ParseResult.GetValueForOption(serverOption)!;
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption)!;
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var logPath = context.ParseResult.GetValueForOption(logPathOption)!;
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var skipChecksum = context.ParseResult.GetValueForOption(skipChecksumOption);
            var skipExtension = context.ParseResult.GetValueForOption(skipExtensionOption);
            var skipNameDate = context.ParseResult.GetValueForOption(skipNameDateOption);
            var skipBurst = context.ParseResult.GetValueForOption(skipBurstOption);
            var skipSameTime = context.ParseResult.GetValueForOption(skipSameTimeOption);

            await RunDeduplicationAsync(server, apiKey, dryRun, verbose, logPath, limit, skipChecksum, skipExtension, skipNameDate, skipBurst, skipSameTime);
        });

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunDeduplicationAsync(string server, string apiKey, bool dryRun, bool verbose, string logPath, int? limit, bool skipChecksum, bool skipExtension, bool skipNameDate, bool skipBurst, bool skipSameTime)
    {
        // Set up cancellation handling
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Also handle Escape key
        _ = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        cts.Cancel();
                        break;
                    }
                }
                Thread.Sleep(100);
            }
        });

        var ui = new ConsoleUI(server, dryRun, verbose);
        
        try
        {
            // Render header and mode indicator
            ui.RenderHeader();
            ui.RenderModeIndicator();

            // Initialize logger
            using var logger = new FileLogger(logPath, verbose);
            logger.LogInfo($"Starting Immich Deduplicator");
            logger.LogInfo($"Server: {server}");
            logger.LogInfo($"Mode: {(dryRun ? "DRY RUN" : "LIVE")}");
            logger.LogInfo($"Limit: {(limit.HasValue ? limit.Value.ToString() : "None")}");
            if (skipChecksum) logger.LogInfo("Skipping: Same Checksum & Date");
            if (skipExtension) logger.LogInfo("Skipping: Different Extension");
            if (skipNameDate) logger.LogInfo("Skipping: Same Name & Date");
            if (skipBurst) logger.LogInfo("Skipping: Burst Photos");
            if (skipSameTime) logger.LogInfo("Skipping: Same Time Different Name");
            logger.LogInfo($"Log Path: {logPath}");

            // Connect to Immich
            using var apiClient = new ImmichApiClient(server, apiKey);
            
            ui.LogInfo("Connecting to Immich server...");
            var (isConnected, serverVersion, error) = await apiClient.ValidateConnectionAsync();
            
            ui.RenderConnectionStatus(isConnected, serverVersion, error);
            
            if (!isConnected)
            {
                logger.LogError($"Connection failed: {error}");
                return;
            }

            logger.LogSuccess($"Connected to Immich {serverVersion}");

            // Fetch duplicates
            ui.LogInfo("Fetching duplicates from server...");
            
            var duplicates = await apiClient.GetDuplicatesAsync();
            
            if (duplicates.Count == 0)
            {
                ui.LogSuccess("No duplicates found. Your library is clean!");
                logger.LogInfo("No duplicates found");
                return;
            }

            ui.LogSuccess($"Found {duplicates.Count} duplicate group(s)");
            logger.LogInfo($"Found {duplicates.Count} duplicate groups");

            // Apply limit if specified
            if (limit.HasValue && limit.Value > 0)
            {
                duplicates = duplicates.Take(limit.Value).ToList();
                ui.LogInfo($"Limiting to first {limit.Value} duplicate group(s)");
                logger.LogInfo($"Limited to {limit.Value} duplicate groups");
            }

            // Categorize duplicates
            var skipOptions = new SkipOptions(skipChecksum, skipExtension, skipNameDate, skipBurst, skipSameTime);
            var deduplicationService = new DeduplicationService(apiClient, ui, logger, dryRun, skipOptions);
            var categories = deduplicationService.CategorizeDuplicates(duplicates);

            // Display the duplicates table
            ui.RenderDuplicatesTable(categories);

            // Confirm before proceeding
            if (!ui.ConfirmProceed())
            {
                ui.RenderCancelled();
                logger.LogInfo("Operation cancelled by user");
                return;
            }

            AnsiConsole.WriteLine();

            // Process duplicates
            ui.LogInfo("Processing duplicates...");
            logger.LogInfo("Starting duplicate processing");

            var (processed, deleted, stacked, albumsUpdated, unchanged) = 
                await deduplicationService.ProcessDuplicatesAsync(categories, cts.Token);

            // Log summary
            logger.LogSummary(processed, deleted, stacked, albumsUpdated, unchanged, dryRun);

            // Display summary
            ui.RenderSummary(processed, deleted, stacked, albumsUpdated, unchanged);
            ui.RenderComplete();

            ui.LogInfo($"Log file saved to: [blue]{Path.GetFullPath(logPath)}[/]");
        }
        catch (OperationCanceledException)
        {
            ui.RenderCancelled();
        }
        catch (Exception ex)
        {
            ui.LogError($"An error occurred: {ex.Message}");
            if (verbose)
            {
                AnsiConsole.WriteException(ex);
            }
            Environment.ExitCode = 1;
        }
    }
}
