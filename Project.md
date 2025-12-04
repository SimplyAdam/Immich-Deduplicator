# Immich Deduplicator Tool
This tool helps identify and remove duplicate images and videos in an Immich media library. It uses the Immich duplication detection algorithm and processes them using the immich API.

## Prerequisites
- This is a C# .NET 10 Console Application
- You need to have access to an Immich server with an API key that has permissions to:
    - Read and delete duplicates
    - Read and delete assets
    - Read albums
    - Create stacks
    - Add and delete album assets.
- The system will require your Immich server URL and API key to function


## Usage
1. Build the project using .NET CLI or your preferred IDE:
   ```
   cd ImmichDeduplicator
   dotnet build
   ```
2. Run the application from the command line:
   ```
   dotnet run -- [options]
   ```

### Command-Line Options
| Option | Description |
|--------|-------------|
| `--server <url>` | **(Required)** Immich server URL |
| `--api-key <key>` | **(Required)** Immich API key |
| `--dry-run` | Run without making any actual changes. Omit this flag to apply changes. |
| `--limit <count>` | Limit the number of duplicates to process (useful for testing) |
| `--verbose` | Enable detailed logging output |
| `--log-path <path>` | Specify custom path for the log file |
| `--skip-checksum` | Skip processing duplicates with same checksum & date |
| `--skip-extension` | Skip processing duplicates with different extensions (no stacking) |
| `--skip-name-date` | Skip processing duplicates with same name & date |
| `--skip-burst` | Skip processing burst photos |
| `--skip-same-time` | Skip processing duplicates with same time but different names |

### Examples
```
# Dry run with verbose output (no changes made)
dotnet run -- --server "https://your-immich-server.com" --api-key "your-api-key" --dry-run --verbose

# Process only the first 10 duplicates (useful for testing)
dotnet run -- --server "https://your-immich-server.com" --api-key "your-api-key" --dry-run --limit 10

# Apply changes
dotnet run -- --server "https://your-immich-server.com" --api-key "your-api-key"

# Only process same checksum duplicates (skip stacking and same name/date)
dotnet run -- --server "https://your-immich-server.com" --api-key "your-api-key" --skip-extension --skip-name-date
```

### Cancellation
You can cancel the operation at any time by pressing `Ctrl+C` or `Escape`. The tool can be safely re-run after cancellation.

## Deduplication Rules
The tool will go through all the duplicates found in the Immich server and for each set of duplicates:
- If all the assets have the same checksum and same creation date which would mean they are identical in type, format and size, it will keep the one that has been assigned to the most albums adding it to any other albums the others were assigned to.
- If the duplicate has two assets:
    - If the assets have different file extensions but the same creation date, it will create a stack of the files.
    - If the assets have the same file names and same creation dates, it will keep the one that has the largest file size and will add it to any albums the other was assigned to.
    - If the assets have the same extension but slightly different file names (most likely sequential) and same creation date but with a creation time that is only slightly different (within 5 seconds), it will stack the files together. (likely burst photos)
    - If the assets have different filenames but identical file extension, creation date and creation time, it will keep the one that has the largest file size and will add it to any albums the other was assigned to.
- **Unchanged**: Duplicates that don't match any of the above criteria will be left unchanged and logged for manual review. Additional handling criteria may be added in future versions.

## Important Notes
- Always back up your Immich media library before running the deduplication tool, especially if not in dry run mode.
- Review the duplicates identified by the tool before confirming deletion to avoid accidental loss of important media files
    - These will be added to a log file name 'deduplication.log' in the application directory.
    - The log will be overwritten each time the tool is run.

## Output
The tool will generate a log file named `deduplication.log` in the application directory (or custom path if specified), detailing the actions taken, including which duplicates were identified and what actions were performed on them. The log is plain text with streamed output.

On screen it will show you the numbers of duplicates that fit each criteria before confirming you wish to proceed with the deletions. If you run in dry run mode, it will pretend it is deleting the files and moving files into stacks and albums but will not actually do so.

## Exit Codes
| Code | Description |
|------|-------------|
| `0` | Success - all operations completed |
| `1` | Failure - an error occurred during execution |