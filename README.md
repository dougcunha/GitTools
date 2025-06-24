# GitTools

[![CI Tests](https://github.com/dougcunha/GitTools/actions/workflows/ci.yml/badge.svg)](https://github.com/dougcunha/GitTools/actions/workflows/ci.yml)
[![Release](https://github.com/dougcunha/GitTools/actions/workflows/release.yml/badge.svg)](https://github.com/dougcunha/GitTools/actions/workflows/release.yml)
[![codecov](https://codecov.io/gh/dougcunha/GitTools/graph/badge.svg?token=HC2PBRX67N)](https://codecov.io/gh/dougcunha/GitTools)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Index

- [Features](#features)
- [Commands](#commands)
  - [Remove Tags (rm)](#remove-tags-rm)
  - [List Tags (ls)](#list-tags-ls)
  - [Reclone Repository (reclone)](#reclone-repository-reclone)
  - [Bulk Backup (bkp)](#bulk-backup-bkp)
  - [Bulk Restore (restore)](#bulk-restore-restore)
  - [Synchronize (sync)](#synchronize-sync)
  - [Build](#build)
- [Code Coverage](#code-coverage)
- [Publish as Single File](#publish-as-single-file)
- [Contributing](#contributing)
- [Third-party Libraries](#third-party-libraries)
- [License](#license)

GitTools is a command-line tool for managing Git repositories, including searching and removing tags across multiple repositories and recloning repositories with backup support.

## Features

- **Tag Management**: Recursively scans directories for Git repositories and manages tags
- **Repository Recloning**: Clean reclone of repositories with automatic backup support
- **Submodule Support**: Works with Git repositories and their submodules
- **Interactive Selection**: User-friendly interface for selecting repositories
- **Wildcard Pattern Support**: Use `*` and `?` characters for flexible tag matching
- **Remote Tag Management**: Optionally remove tags from remote repositories
- **Tag Listing with wildcard filtering**
- **Bulk Backup/Restore**: Generate a JSON configuration file and restore repositories from it
- **Synchonize**: Identify repositories behind a remote branch and update them
- **Backup Creation**: Automatic ZIP backup creation before destructive operations
- **Modern Terminal UI**: Built with [Spectre.Console](https://spectreconsole.net/) for beautiful interfaces
- **Modern CLI Parsing**: Uses [System.CommandLine](https://github.com/dotnet/command-line-api) for extensible command-line parsing
- **Global Options**: Configure output behavior with global options available across all commands

## Commands

GitTools provides several commands for repository management, all supporting global options for customizing output and behavior.

### Global Options

These options are available for all commands:

- `--log-all-git-commands`, `-lg`: Log all git commands executed to the console (useful for debugging)
- `--disable-ansi`, `-da`: Disable ANSI color codes in console output (useful for plain text output or incompatible terminals)
- `--quiet`, `-q`: Suppress all console output (useful for automated scripts or silent operation)

**Examples:**

```sh
# Enable git command logging for any command
GitTools ls C:/Projects "v1.*" --log-all-git-commands

# Disable colors for plain text output
GitTools rm C:/Projects NET8 --disable-ansi

# Run silently without any output
GitTools sync C:/Projects --quiet

# Combine multiple global options
GitTools reclone ./my-project --log-all-git-commands --disable-ansi
```

### Remove Tags (rm)

Search and remove tags from multiple Git repositories with wildcard pattern support.

```sh
GitTools rm <root-directory> <tag1,tag2,...> [--remote]
```

#### Wildcard Support

GitTools supports wildcard patterns for flexible tag matching:

- `*` (asterisk): Matches zero or more characters
- `?` (question mark): Matches exactly one character

#### Wildcard Examples

**Exact tag matching** (traditional behavior):

```sh
GitTools rm C:/Projects NET8,NET7 --remote
```

**Wildcard pattern matching:**

```sh
# Remove all tags starting with "v1."
GitTools rm C:/Projects "v1.*"

# Remove all tags like "TAG1.0", "TAG1.1", etc.
GitTools rm C:/Projects "TAG1.?"

# Remove multiple patterns
GitTools rm C:/Projects "v1.*,v2.*,beta-*"

# Mix exact tags and patterns
GitTools rm C:/Projects "NET8,v1.*,beta-?"
```

**Note:** When using wildcards on Windows Command Prompt or PowerShell, wrap patterns in quotes to prevent shell expansion.

#### Parameters

- `root-directory` (required): Root directory to scan for Git repositories (e.g., `C:\Projects`)
- `tags` (required): Comma-separated list of tags or wildcard patterns to search and remove (e.g., `NET8,NET7` or `v1.*,beta-?`)
- `--remote`, `-r`, `/remote` (optional): Also remove the tag from the remote repository (origin)
- `--help` or `-h`: Show help and usage information

### List Tags (ls)

List repositories containing specific tags. Supports the same wildcard patterns as tag removal.

```sh
GitTools ls <root-directory> <tag1,tag2,...>
```

Example output:

```text
Repo1: v1.0, v2.0
```

### Reclone Repository (reclone)

Reclone a Git repository with automatic backup and cleanup support. This command is useful for cleaning up repository state, removing local changes, or fixing corrupted repositories.

```sh
GitTools reclone <repository-path> [--no-backup] [--force]
```

#### Reclone Features

- **Automatic Backup**: Creates a ZIP backup of the repository before recloning (unless `--no-backup` is specified)
- **Uncommitted Changes Detection**: Checks for uncommitted changes and requires `--force` to proceed
- **Temporary Directory Management**: Safely renames the original directory during the process
- **Cleanup**: Automatically removes the old repository directory after successful recloning
- **Remote URL Preservation**: Uses the existing remote URL for recloning

#### Reclone Parameters

- `repository-path` (required): Path to the Git repository (absolute or relative to the current directory)
- `--no-backup` (optional): Skip creating a backup ZIP file before recloning
- `--force` (optional): Ignore uncommitted changes and proceed with recloning
- `--help` or `-h`: Show help and usage information

#### Examples

**Basic reclone with backup:**

```sh
GitTools reclone ./my-project
GitTools reclone C:/Projects/my-project
```

**Reclone without backup:**

```sh
GitTools reclone ./my-project --no-backup
```

**Force reclone ignoring uncommitted changes:**

```sh
GitTools reclone ./my-project --force
```

**Reclone without backup and ignoring changes:**

```sh
GitTools reclone ./my-project --no-backup --force
```

### Bulk Backup (bkp)

Generate a JSON file with the remote URL for each repository under a directory. This can be used later for bulk restoration.

```sh
GitTools bkp <root-directory> [output-file]
```

### Bulk Restore (restore)

Clone repositories from a configuration file created by the bulk backup command.

```json
[
  {"name": "repo1", "remote_url": "https://example.com/repo1.git"},
  {"name": "repo2", "remote_url": "https://example.com/repo2.git"}
]
```

```sh
GitTools restore repos.json <target-directory>
```

Use `--force-ssh` to convert all repository URLs in the JSON file to SSH before cloning.

```sh
GitTools restore repos.json <target-directory> --force-ssh
```

### Synchronize (sync)

Detect repositories that are behind the specified branch and optionally update them.

```sh
GitTools sync <root-directory> [--show-only] [--with-uncommitted] [--no-fetch]
```

By default repositories with uncommitted changes are skipped. Use `--with-uncommitted` to include them; any changes are stashed before updating.

Use `--show-only` if you want to see what is new without changing anything on local repository.

Use `--no-fetch` to skip fetching updates from the remote server before checking each repository.

## Build

This project requires [.NET 9 SDK](https://dotnet.microsoft.com/) to build and run.

```sh
dotnet build
```

## Code Coverage

To generate and view code coverage reports:

### Generate Coverage Report

```sh
# Using the provided PowerShell script (recommended)
.\generate-coverage.ps1

# Or manually
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
reportgenerator -reports:"TestResults\**\coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"Html"
```

### View Coverage Report

```sh
# Generate and open the report automatically
.\generate-coverage.ps1 -OpenReport

# Or open manually
start CoverageReport\index.html
```

The coverage report includes:

- **Line Coverage**: Percentage of executable lines covered by tests
- **Branch Coverage**: Percentage of decision branches covered by tests  
- **Method Coverage**: Percentage of methods covered by tests
- **Per-file Analysis**: Detailed coverage breakdown for each source file

## Publish as Single File

```sh
dotnet publish -c Release -r <RID>
```

Replace `<RID>` with your target runtime identifier, for example `win-x64` for
Windows or `linux-x64` for Linux.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to contribute to this project.

## Third-party Libraries

This project uses the following open source libraries:

- [Spectre.Console](https://spectreconsole.net/) — for beautiful, interactive console UIs in .NET
- [System.CommandLine](https://github.com/dotnet/command-line-api) — for modern, extensible command-line parsing

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
