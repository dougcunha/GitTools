# GitTools

[![CI Tests](https://github.com/dougcunha/GitTools/actions/workflows/ci.yml/badge.svg)](https://github.com/dougcunha/GitTools/actions/workflows/ci.yml)
[![Release](https://github.com/dougcunha/GitTools/actions/workflows/release.yml/badge.svg)](https://github.com/dougcunha/GitTools/actions/workflows/release.yml)
[![codecov](https://codecov.io/gh/dougcunha/GitTools/graph/badge.svg?token=HC2PBRX67N)](https://codecov.io/gh/dougcunha/GitTools)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Index

- [Features](#features)
- [Usage](#usage)
- [Parameters](#parameters)
- [Example](#example)
- [Build](#build)
- [Code Coverage](#code-coverage)
- [Publish as Single File](#publish-as-single-file)
- [Contributing](#contributing)
- [Third-party Libraries](#third-party-libraries)
- [License](#license)

GitTools is a command-line tool for searching and removing tags in multiple Git repositories and their submodules. It is designed to help you manage tags across large codebases with ease.

## Features

- Recursively scans a root directory for Git repositories and submodules
- Lists all repositories containing specified tags
- Allows interactive selection of repositories to remove tags from
- Removes only the tags that actually exist in each repository
- Supports multiple tags (comma-separated)
- **Supports wildcard patterns** using `*` and `?` characters for flexible tag matching
- Optionally removes tags from the remote repository as well
- Modern, user-friendly terminal UI using [Spectre.Console](https://spectreconsole.net/)
- Modern CLI parsing with [System.CommandLine](https://github.com/dotnet/command-line-api)

## Usage

```sh
GitTools rm <root-directory> <tag1,tag2,...> [--remote]
```

### Wildcard Support

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

### Parameters

- `root-directory` (required): Root directory to scan for Git repositories (e.g., `C:\Projects`)
- `tags` (required): Comma-separated list of tags or wildcard patterns to search and remove (e.g., `NET8,NET7` or `v1.*,beta-?`)
- `--remote`, `-r`, `/remote` (optional): Also remove the tag from the remote repository (origin)
- `--help` or `-h`: Show help and usage information

### Example

**Remove specific tags:**

```sh
GitTools rm C:/Projects NET8,NET7 --remote
```

**Remove tags using wildcards:**

```sh
GitTools rm C:/Projects "v1.*,beta-*" --remote
```

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
