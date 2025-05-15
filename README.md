# GitTools

- [Features](#features)
- [Usage](#usage)
- [Parameters](#parameters)
- [Example](#example)
- [Build](#build)
- [Publish as Single File (Windows x64)](#publish-as-single-file-windows-x64)
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
- Modern, user-friendly terminal UI using [Spectre.Console](https://spectreconsole.net/)

## Usage

```sh
GitTools --dir <root-directory> --tags <tag1,tag2,...>
```

### Parameters

- `--dir` (required): Root directory to scan for Git repositories (e.g., `C:\Projects`)
- `--tags` (required): Comma-separated list of tags to search and remove (e.g., `NET8,NET7`)
- `--help` or `-h`: Show help and usage information

### Example

```sh
GitTools --dir C:\Projects --tags NET8,NET7
```

## Build

This project requires [.NET 9 SDK](https://dotnet.microsoft.com/) to build and run.

```sh
dotnet build
```

## Publish as Single File (Windows x64)

```sh
dotnet publish -c Release -r win-x64 --self-contained true
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to contribute to this project.

## Third-party Libraries

This project uses the following open source libraries:

- [Spectre.Console](https://spectreconsole.net/) — for beautiful, interactive console UIs in .NET
- [FluentArgs](https://github.com/kutoga/FluentArgs) — for modern, fluent command-line argument parsing

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
