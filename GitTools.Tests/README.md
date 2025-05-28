# GitTools.Tests

This project contains the unit tests for the GitTools application.

## Test Structure

### Services

- **GitServiceTests**: Tests for the service responsible for Git operations (tags, commands)
- **GitRepositoryScannerTests**: Tests for the Git repository scanner

## Technologies Used

- **xUnit**: Testing framework
- **Shouldly**: Fluent assertion library
- **NSubstitute**: Mocking framework
- **System.IO.Abstractions.TestingHelpers**: Helpers for file system testing

## Running Tests

To run all tests:

```bash
dotnet test
```

To run tests for a specific project only:

```bash
dotnet test GitTools.Tests
```

To run with code coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Conventions

### Naming

- Test classes end with `Tests`
- Test methods follow the pattern: `Method_Condition_ExpectedResult`

### Test Pattern

All tests follow the **AAA (Arrange, Act, Assert)** pattern:

```csharp
[Fact]
public void Method_Condition_ExpectedResult()
{
    // Arrange
    // Setup data and mocks

    // Act  
    // Execute the method being tested

    // Assert
    // Verify the results
}
```

### Mocking

- Dependencies are declared as `readonly fields` and initialized inline
- Uses `NSubstitute` for creating mocks
- Non-virtual classes are tested with real instances or extracted interfaces

### Code Coverage

- All test classes have the `[ExcludeFromCodeCoverage]` attribute
- Focus is on production code coverage, not test code

## Specific Tests

### GitServiceTests

Tests the main functionalities of `GitService`:

- Tag existence verification
- Local and remote tag removal
- Git command execution
- Worktree handling
- Error handling

### GitRepositoryScannerTests

Tests Git repository scanning:

- Discovery of single and multiple repositories
- Submodule handling
- Repositories with `.git` files (worktrees)
- Access error handling
- Non-Git directory filtering
