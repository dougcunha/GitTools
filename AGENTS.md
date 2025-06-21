# GitTools - AI Agents Guide

This Agents.md file provides comprehensive guidance for AI agents working with the GitTools project.

## Project Structure for AI Agent Navigation

- `/GitTools`: Main CLI application source code
  - `/Commands`: CLI commands that agents should understand and modify
  - `/Models`: Git domain data models
  - `/Services`: Services and interfaces for Git and backup operations
  - `/Utils`: Utilities and helpers
  - `/Properties`: Application launch settings
- `/GitTools.Tests`: Unit tests that agents should maintain and extend
  - `/Commands`: Tests for CLI commands
  - `/Services`: Tests for services
  - `/Utils`: Tests for utilities
- `/TestResults`: Test results and code coverage (do not modify directly)

## Coding Conventions for AI Agents

### General Conventions

- Use modern C# (.NET 9.0) for all new code
- Agents should follow the existing code style in each file
- Use meaningful names for variables, methods, and classes
- Add XML documentation for public methods and classes
- Use `var` when the type is obvious
- Use modern C# collections (spans, ranges, etc.)
- Utilize async/await for I/O operations

### CLI Commands Guidelines

- Agents should use System.CommandLine for new commands
- Keep commands focused with single responsibility
- Implement proper parameter validation
- Use Spectre.Console for rich user interfaces
- Follow naming convention: `{Action}Command.cs`

### Service Patterns

- Agents should implement interfaces for all services
- Use dependency injection with Microsoft.Extensions.DependencyInjection
- Keep services testable and loosely coupled
- Use System.IO.Abstractions for testable file system operations

## Testing Requirements for AI Agents

Agents should run tests with the following commands:

```bash
# Run all tests
dotnet test

# Run tests for a specific project
dotnet test GitTools.Tests/GitTools.Tests.csproj

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report using PowerShell script
./generate-coverage.ps1
```

## Pull Request Guidelines for AI Agents

When agents help create a PR, ensure it:

1. Includes a clear description of changes as guided by CONTRIBUTING.md
2. References any related issues being addressed
3. Ensures all tests pass for generated code
4. Keeps PRs focused on a single responsibility
5. Follows project code style guidelines
6. Adds or updates tests as appropriate

## Programmatic Checks for AI Agents

Before submitting changes, run:

```bash
# Build the project
dotnet build

# Run all tests
dotnet test

# Check code coverage
./generate-coverage.ps1

# Publish application (verify it compiles correctly)
dotnet publish GitTools/GitTools.csproj -c Release
```

All checks must pass before agent-generated code can be merged.