using GitTools.Services;

namespace GitTools.Tests.Services;

/// <summary>
/// Tests for <see cref="TagValidationService"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TagValidationServiceTests
{
    private static readonly TagValidationService _service = new();

    [Fact]
    public void ParseAndValidateTags_ValidSingleTag_ReturnsArray()
    {
        // Arrange
        const string INPUT = "v1.0.0";

        // Act
        var result = _service.ParseAndValidateTags(INPUT);

        // Assert
        result.ShouldBe(["v1.0.0"]);
    }

    [Fact]
    public void ParseAndValidateTags_ValidMultipleTags_ReturnsArray()
    {
        // Arrange
        const string INPUT = "v1.0.0,v2.0.0,v3.0.0";

        // Act
        var result = _service.ParseAndValidateTags(INPUT);

        // Assert
        result.ShouldBe(["v1.0.0", "v2.0.0", "v3.0.0"]);
    }

    [Fact]
    public void ParseAndValidateTags_TagsWithSpaces_TrimsAndReturnsArray()
    {
        // Arrange
        const string INPUT = " v1.0.0 , v2.0.0 , v3.0.0 ";

        // Act
        var result = _service.ParseAndValidateTags(INPUT);

        // Assert
        result.ShouldBe(["v1.0.0", "v2.0.0", "v3.0.0"]);
    }

    [Fact]
    public void ParseAndValidateTags_EmptyEntries_FiltersOutEmptyEntries()
    {
        // Arrange
        const string INPUT = "v1.0.0,,v2.0.0, ,v3.0.0";

        // Act
        var result = _service.ParseAndValidateTags(INPUT);

        // Assert
        result.ShouldBe(["v1.0.0", "v2.0.0", "v3.0.0"]);
    }

    [Fact]
    public void ParseAndValidateTags_NullInput_ReturnsEmptyArray()
    {
        // Act
        var result = _service.ParseAndValidateTags(null!);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAndValidateTags_EmptyInput_ReturnsEmptyArray()
    {
        // Act
        var result = _service.ParseAndValidateTags(string.Empty);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAndValidateTags_WhitespaceInput_ReturnsEmptyArray()
    {
        // Act
        var result = _service.ParseAndValidateTags("   ");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAndValidateTags_OnlyCommas_ReturnsEmptyArray()
    {
        // Act
        var result = _service.ParseAndValidateTags(",,,");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAndValidateTags_WildcardPatterns_ReturnsArray()
    {
        // Arrange
        const string INPUT = "v*.0.0,release-*,*-beta";

        // Act
        var result = _service.ParseAndValidateTags(INPUT);

        // Assert
        result.ShouldBe(["v*.0.0", "release-*", "*-beta"]);
    }

    [Fact]
    public void IsValidTagsInput_ValidInput_ReturnsTrue()
    {
        // Act & Assert
        _service.IsValidTagsInput("v1.0.0").ShouldBeTrue();
        _service.IsValidTagsInput("v1.0.0,v2.0.0").ShouldBeTrue();
        _service.IsValidTagsInput("tag").ShouldBeTrue();
        _service.IsValidTagsInput("*").ShouldBeTrue();
    }

    [Fact]
    public void IsValidTagsInput_NullInput_ReturnsFalse()
    {
        // Act & Assert
        _service.IsValidTagsInput(null!).ShouldBeFalse();
    }

    [Fact]
    public void IsValidTagsInput_EmptyInput_ReturnsFalse()
    {
        // Act & Assert
        _service.IsValidTagsInput(string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void IsValidTagsInput_WhitespaceInput_ReturnsFalse()
    {
        // Act & Assert
        _service.IsValidTagsInput("   ").ShouldBeFalse();
        _service.IsValidTagsInput("\t").ShouldBeFalse();
        _service.IsValidTagsInput("\n").ShouldBeFalse();
    }
}

