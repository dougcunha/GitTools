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
- 4-space indentation.
- Always use a blank line between code blocks (methods, properties, constructors, etc).
- Always add a blank line before if, for, foreach, and return.
- Always add a blank line after opening or before closing braces.
- Use expression-bodied members and start a new line before =>
- Constant names in UPPER_CASE.
- Use var when the type is obvious.
- XMLDoc is mandatory for public classes and methods.
- Non-inheritable classes must be sealed.
- DRY: avoid code repetition.
- Write code always in english.
- Use modern collections and initializers ([] and [..] when possible).
- Add `<inherited />` for inherited members in XMLDoc.
- Always put using before namespace declaration and sort them alphabetically.
- Use `nameof` operator instead of hardcoded strings for member names.
- Make anonymous function static when possible.
- Keep always one class per file.
- Use `string.Equals` with `StringComparison.OrdinalIgnoreCase` for case-insensitive comparisons.
- Use `StringBuilder` for string concatenation in loops or large concatenations.
- Use .ConfigureAwait(false) for async methods to avoid deadlocks except in Windows Forms applications.
- Use `CancellationToken` for long-running operations to allow cancellation.

### Writing Test Convetions

- Use xUnit, Shouldly and NSubstitute.
- Place ExcludeFromCodeCoverage attribute on all test classes.
- All test classes must be public sealed.
- Use AAA (Arrange, Act, Assert) pattern.
- Use Method_Condition_ExpectedResult naming convention for test methods.
- Declare all dependencies as readonly fields and initialize it inline.
- Declare string const as const and use UPPER_CASE for names
- Use raw string to declare multi-line strings
- Avoid using magic strings and numbers, declare them as constants.
- Never try to mock non virtual classes
- If you can't mock a class, use a real instance of it or extract it to an interface.
- Use verbatim strings for file paths and other strings that require escaping.

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