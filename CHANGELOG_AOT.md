# AOT Compatibility Changes

## Overview

This update implements Ahead-of-Time (AOT) compilation compatibility for GitTools, enabling faster startup times and smaller deployments.

## Changes Made

### Project Configuration

- **GitTools.csproj**: Added `PublishAot=true`, `InvariantGlobalization=true`, `TrimMode=full`
- **GitTools.csproj**: Changed `PublishSingleFile=false` (AOT is incompatible with single file)

### JSON Serialization

- **NEW**: `GitToolsJsonContext.cs` - Source-generated JSON serializer context for AOT
- **NEW**: `GitRepositoryBackup.cs` - Dedicated model for backup operations
- **UPDATED**: `GitRepository.cs` - Added JSON property attributes for AOT compatibility
- **UPDATED**: `GitToolsOptions.cs` - Added JSON property attributes with proper naming
- **UPDATED**: `BulkBackupCommand.cs` - Uses AOT-compatible JSON serialization
- **UPDATED**: `BulkRestoreCommand.cs` - Uses AOT-compatible JSON deserialization with backward compatibility

### Console Display

- **UPDATED**: `ConsoleDisplayService.cs` - Replaced `WriteException` with AOT-compatible error display

### CI/CD

- **UPDATED**: `release.yml` - Workflow now uses `-p:PublishAot=true` for both Windows and Linux builds

## Benefits

- **Faster Startup**: AOT compilation eliminates JIT compilation overhead
- **Smaller Deployments**: Trimming removes unused code
- **Better Performance**: Native code execution
- **Self-Contained**: No runtime dependencies required

## Backward Compatibility

- JSON files created with the old format are still supported
- Existing configurations will continue to work seamlessly

## Technical Notes

- Source generators are used for both JSON serialization and Regex patterns
- All reflection-based operations have been eliminated
- The application is now fully compatible with trimming and AOT compilation
