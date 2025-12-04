using Spectre.Console;
using ImmichDeduplicator.Models;

namespace ImmichDeduplicator.Services;

/// <summary>
/// Handles console UI rendering using Spectre.Console
/// </summary>
public class ConsoleUI
{
    private readonly bool _isDryRun;
    private readonly bool _isVerbose;
    private readonly string _serverUrl;

    public ConsoleUI(string serverUrl, bool isDryRun, bool isVerbose)
    {
        _serverUrl = serverUrl;
        _isDryRun = isDryRun;
        _isVerbose = isVerbose;
    }

    /// <summary>
    /// Renders the application header
    /// </summary>
    public void RenderHeader()
    {
        AnsiConsole.Clear();
        
        // Title
        var titlePanel = new Panel(
            new FigletText("Immich Deduplicator")
                .Centered()
                .Color(Color.Cyan1))
            .Border(BoxBorder.Double)
            .BorderColor(Color.Cyan1);
        
        AnsiConsole.Write(titlePanel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the mode indicator (dry-run or live)
    /// </summary>
    public void RenderModeIndicator()
    {
        var modeText = _isDryRun 
            ? "[bold yellow on darkgoldenrod]  DRY RUN MODE  [/]" 
            : "[bold white on red]  LIVE MODE  [/]";
        
        var rule = new Rule(modeText)
        {
            Justification = Justify.Right,
            Style = _isDryRun ? Style.Parse("yellow") : Style.Parse("red")
        };
        
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the connection status box
    /// </summary>
    public void RenderConnectionStatus(bool isConnected, string? serverVersion, string? error)
    {
        var borderColor = isConnected ? Color.Green : Color.Red;
        var statusIcon = isConnected ? "✓" : "✗";
        var statusColor = isConnected ? "green" : "red";
        
        var content = new Markup($"""
            [{statusColor}]{statusIcon}[/] Server: [bold]{_serverUrl}[/]
            [{statusColor}]{statusIcon}[/] Status: [{statusColor}]{(isConnected ? "Connected" : "Failed")}[/]
            """);

        if (serverVersion != null)
        {
            content = new Markup($"""
                [{statusColor}]{statusIcon}[/] Server: [bold]{_serverUrl}[/]
                [{statusColor}]{statusIcon}[/] Version: [bold]{serverVersion}[/]
                [{statusColor}]{statusIcon}[/] Status: [{statusColor}]{(isConnected ? "Connected" : "Failed")}[/]
                """);
        }

        if (!isConnected && error != null)
        {
            content = new Markup($"""
                [{statusColor}]{statusIcon}[/] Server: [bold]{_serverUrl}[/]
                [{statusColor}]{statusIcon}[/] Status: [{statusColor}]Failed[/]
                [{statusColor}]{statusIcon}[/] Error: [red]{error}[/]
                """);
        }

        var panel = new Panel(content)
        {
            Header = new PanelHeader(" Connection Status "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the duplicates statistics table
    /// </summary>
    public void RenderDuplicatesTable(CategorizedDuplicates categories, bool showProcessed = false, 
        int processedSameChecksum = 0, int processedDiffExt = 0, int processedSameName = 0, 
        int processedBurst = 0, int processedSameTime = 0)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Title("[bold blue] Duplicate Categories [/]")
            .AddColumn(new TableColumn("[bold]Category[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Count[/]").Centered())
            .AddColumn(new TableColumn("[bold]Action[/]").LeftAligned());

        if (showProcessed)
        {
            table.AddColumn(new TableColumn("[bold]Processed[/]").Centered());
        }

        // Same Checksum Same Date
        var row1 = new List<string>
        {
            "[cyan]Same Checksum & Date[/]",
            $"[white]{categories.SameChecksumSameDate.Count}[/]",
            "[dim]Keep best, delete others[/]"
        };
        if (showProcessed) row1.Add($"[green]{processedSameChecksum}[/]");
        table.AddRow(row1.ToArray());

        // Different Extension (Same Date)
        var row2 = new List<string>
        {
            "[magenta]Different Extension (Same Date)[/]",
            $"[white]{categories.DifferentExtension.Count}[/]",
            "[dim]Create stack[/]"
        };
        if (showProcessed) row2.Add($"[green]{processedDiffExt}[/]");
        table.AddRow(row2.ToArray());

        // Same Name Same Date
        var row3 = new List<string>
        {
            "[yellow]Same Name & Date[/]",
            $"[white]{categories.SameNameSameDate.Count}[/]",
            "[dim]Keep largest, delete others[/]"
        };
        if (showProcessed) row3.Add($"[green]{processedSameName}[/]");
        table.AddRow(row3.ToArray());

        // Burst Photos
        var row4 = new List<string>
        {
            "[green]Burst Photos[/]",
            $"[white]{categories.BurstPhotos.Count}[/]",
            "[dim]Create stack[/]"
        };
        if (showProcessed) row4.Add($"[green]{processedBurst}[/]");
        table.AddRow(row4.ToArray());

        // Same Time Different Name
        var row5 = new List<string>
        {
            "[blue]Same Time, Different Name[/]",
            $"[white]{categories.SameTimeDifferentName.Count}[/]",
            "[dim]Keep largest, delete others[/]"
        };
        if (showProcessed) row5.Add($"[green]{processedSameTime}[/]");
        table.AddRow(row5.ToArray());

        // Unchanged
        var row6 = new List<string>
        {
            "[grey]Unchanged[/]",
            $"[white]{categories.Unchanged.Count}[/]",
            "[dim]No action (logged)[/]"
        };
        if (showProcessed) row6.Add("[grey]-[/]");
        table.AddRow(row6.ToArray());

        // Total row
        table.AddEmptyRow();
        var totalRow = new List<string>
        {
            "[bold]Total[/]",
            $"[bold white]{categories.TotalCount}[/]",
            ""
        };
        if (showProcessed) totalRow.Add($"[bold green]{processedSameChecksum + processedDiffExt + processedSameName + processedBurst + processedSameTime}[/]");
        table.AddRow(totalRow.ToArray());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Prompts user to confirm proceeding with the operation
    /// </summary>
    public bool ConfirmProceed()
    {
        var actionText = _isDryRun 
            ? "[yellow]simulate processing[/]" 
            : "[red]process and modify[/]";

        return AnsiConsole.Confirm($"Do you want to {actionText} these duplicates?");
    }

    /// <summary>
    /// Updates the status line (overwrites the current line)
    /// </summary>
    public void UpdateStatus(string message)
    {
        // Move cursor up one line and clear it
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Console.CursorTop);
        AnsiConsole.MarkupLine(message);
    }

    /// <summary>
    /// Writes a blank line for status updates to overwrite
    /// </summary>
    public void StartStatusLine()
    {
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Clears the status line
    /// </summary>
    public void ClearStatusLine()
    {
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Console.CursorTop);
    }

    /// <summary>
    /// Logs an info message
    /// </summary>
    public void LogInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {message}");
    }

    /// <summary>
    /// Logs a success message
    /// </summary>
    public void LogSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {message}");
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    public void LogWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {message}");
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    public void LogError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {message}");
    }

    /// <summary>
    /// Logs a verbose message (only shown if verbose mode is enabled)
    /// </summary>
    public void LogVerbose(string message)
    {
        if (_isVerbose)
        {
            AnsiConsole.MarkupLine($"[grey]  → {message}[/]");
        }
    }

    /// <summary>
    /// Renders a summary panel
    /// </summary>
    public void RenderSummary(int processed, int deleted, int stacked, int albumsUpdated, int unchanged)
    {
        var modeLabel = _isDryRun ? "[yellow](DRY RUN)[/]" : "";
        
        var summaryContent = new Markup($"""
            [bold]Duplicates Processed:[/] [cyan]{processed}[/] {modeLabel}
            [bold]Assets Deleted:[/] [red]{deleted}[/]
            [bold]Stacks Created:[/] [magenta]{stacked}[/]
            [bold]Album Assignments:[/] [blue]{albumsUpdated}[/]
            [bold]Unchanged:[/] [grey]{unchanged}[/]
            """);

        var panel = new Panel(summaryContent)
        {
            Header = new PanelHeader(" Summary "),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Shows completion message
    /// </summary>
    public void RenderComplete()
    {
        AnsiConsole.WriteLine();
        if (_isDryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry run complete. No changes were made.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Deduplication complete![/]");
        }
    }

    /// <summary>
    /// Shows cancellation message
    /// </summary>
    public void RenderCancelled()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Operation cancelled by user.[/]");
    }
}
