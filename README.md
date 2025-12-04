# Immich Deduplicator

A command-line tool to identify and remove duplicate images and videos in an [Immich](https://immich.app/) media library. It leverages Immich's built-in duplicate detection and processes duplicates automatically using the Immich API.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green)

## Features

- 🔍 **Smart Detection** - Categorizes duplicates by checksum, filename, timestamp, and file type
- 📦 **Stacking Support** - Automatically stacks RAW+JPG pairs and burst photos
- 📁 **Album Preservation** - Transfers album assignments before removing duplicates
- 🧪 **Dry Run Mode** - Preview changes without modifying your library
- 📝 **Detailed Logging** - Comprehensive log file for audit and review
- ⚡ **Flexible Filtering** - Skip specific categories or limit processing count

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- Access to an Immich server with an API key that has permissions to:
  - Read and manage duplicates
  - Read and delete assets
  - Read and modify albums
  - Create stacks

## Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/immich-deduplicator.git
   cd immich-deduplicator
   ```

2. Build the project:
   ```bash
   cd ImmichDeduplicator
   dotnet build
   ```

## Usage

```bash
dotnet run -- --server <url> --api-key <key> [options]
```

### Command-Line Options

| Option | Description |
|--------|-------------|
| `--server <url>` | **(Required)** Immich server URL |
| `--api-key <key>` | **(Required)** Immich API key |
| `--dry-run` | Run without making any actual changes |
| `--limit <count>` | Limit the number of duplicates to process |
| `--verbose` | Enable detailed logging output |
| `--log-path <path>` | Specify custom path for the log file |
| `--skip-checksum` | Skip processing duplicates with same checksum & date |
| `--skip-extension` | Skip processing duplicates with different extensions |
| `--skip-name-date` | Skip processing duplicates with same name & date |
| `--skip-burst` | Skip processing burst photos |
| `--skip-same-time` | Skip processing duplicates with same time but different names |

### Examples

```bash
# Dry run with verbose output (preview changes without applying)
dotnet run -- --server "https://immich.example.com" --api-key "your-api-key" --dry-run --verbose

# Process only the first 10 duplicates (useful for testing)
dotnet run -- --server "https://immich.example.com" --api-key "your-api-key" --dry-run --limit 10

# Apply changes (live mode)
dotnet run -- --server "https://immich.example.com" --api-key "your-api-key"

# Only process identical duplicates (skip stacking and other categories)
dotnet run -- --server "https://immich.example.com" --api-key "your-api-key" --skip-extension --skip-name-date --skip-burst --skip-same-time
```

### Cancellation

Press `Ctrl+C` or `Escape` at any time to safely cancel the operation. The tool can be re-run after cancellation.

## Deduplication Rules

The tool processes duplicates based on the following criteria:

| Category | Condition | Action |
|----------|-----------|--------|
| **Same Checksum & Date** | Identical checksum and creation date | Keep the one in most albums, delete others |
| **Different Extension** | Same name, different extensions (e.g., JPG + RAW) | Create a stack |
| **Same Name & Date** | Identical filename and date, same extension | Keep largest file, delete others |
| **Burst Photos** | Sequential names, within 5 seconds, same extension | Create a stack |
| **Same Time, Different Name** | Same timestamp (within 1 sec), different names | Keep largest file, delete others |
| **Unchanged** | Doesn't match any criteria | Left for manual review |

### Asset Selection Priority

When choosing which asset to keep:
1. Most album assignments
2. Marked as favorite
3. Largest file size

### Album Handling

Before deleting duplicates, the tool:
1. Identifies all albums containing the assets to be deleted
2. Adds the keeper asset to those albums
3. Then removes the duplicate assets

## Output

### Console Display

The tool displays a colorful summary table showing:
- Duplicate categories and counts
- Actions to be performed
- Progress during processing

### Log File

A detailed log file (`deduplication.log` by default) is generated containing:
- All duplicates processed with their category
- Assets involved and actions taken
- Unchanged duplicates for manual review
- Summary statistics

## Exit Codes

| Code | Description |
|------|-------------|
| `0` | Success - all operations completed |
| `1` | Failure - an error occurred during execution |

## ⚠️ Important Notes

- **Always back up your Immich library** before running in live mode
- **Use `--dry-run` first** to preview what changes will be made
- **Review the log file** to understand what actions were performed
- Deleted assets go to Immich's trash (soft delete) and can be restored
- Unchanged duplicates are logged for manual review in the Immich UI

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Immich](https://immich.app/) - The amazing self-hosted photo and video backup solution
- [Spectre.Console](https://spectreconsole.net/) - Beautiful console applications for .NET
