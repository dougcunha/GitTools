using GitTools.Services;

namespace GitTools.Tests.Services;

/// <summary>
/// Tests for <see cref="TagValidationService"/>.
/// </summary>
public sealed class TagValidationServiceTests
{
    private static readonly TagValidationService Service = new();

    [Fact]
    public void ParseAndValidateTags_ValidSingleTag_ReturnsArray()
    {
        // Arrange
        const string input = "v1.0.0";

        // Act
        var result = Service.ParseAndValidateTags(input);

        // Assert
        result.ShouldBe(["v1.0.0"]);
    }

    [Fact]
    public void ParseAndValidateTags_ValidMultipleTags_ReturnsArray()
    {
        // Arrange
        const string input = "v1.0.0,v2.0.0,v3.0.0";

        // Act
        var result = Service.ParseAndValidateTags(input);

        // Assert
        result.ShouldBe(["v1.0.0", "v2.0.0", "v3.0.0"]);
    }

    [Fact]
    public void ParseAndValidateTags_TagsWithSpaces_TrimsAndReturnsArray()
    {
        // Arrange
        const string input = " v1.0.0 , v2.0.0 , v3.0.0 ";

        // Act
        var result = Service.ParseAndValidateTags(input);

        // Assert
        result.ShouldBe(["v1.0.0", "v2.0.0", "v3.0.0"]);
    }

    [Fact]
    public void ParseAndValidateTags_EmptyEntries_FiltersOutEmptyEntries()
    {
        // Arrange
        const string input = "v1.0.0,,v2.0.0, ,v3.0.0";

        // Act
        var result = Service.ParseAndValidateTags(input);

        // Assert
        result.ShouldBe(["v1.0.0", "v2.0.0", "v3.0.0"]);
    }

    [Fact]
    public void ParseAndValidateTags_NullInput_ReturnsEmptyArray()
    {
        // Act
        var result = Service.ParseAndValidateTags(null!);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAndValidateTags_EmptyInput_ReturnsEmptyArray()
    {
        // Act
        var result = Service.ParseAndValidateTags(string.Empty);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAndValidateTags_WhitespaceInput_ReturnsEmptyArray()
    {
        // Act
        var result = Service.ParseAndValidateTags("   ");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAndValidateTags_OnlyCommas_ReturnsEmptyArray()
    {
        // Act
        var result = Service.ParseAndValidateTags(",,,");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseAndValidateTags_WildcardPatterns_ReturnsArray()
    {
        // Arrange
        const string input = "v*.0.0,release-*,*-beta";

        // Act
        var result = Service.ParseAndValidateTags(input);

        // Assert
        result.ShouldBe(["v*.0.0", "release-*", "*-beta"]);
    }

    [Fact]
    public void IsValidTagsInput_ValidInput_ReturnsTrue()
    {
        // Act & Assert
        Service.IsValidTagsInput("v1.0.0").ShouldBeTrue();
        Service.IsValidTagsInput("v1.0.0,v2.0.0").ShouldBeTrue();
        Service.IsValidTagsInput("tag").ShouldBeTrue();
        Service.IsValidTagsInput("*").ShouldBeTrue();
    }

    [Fact]
    public void IsValidTagsInput_NullInput_ReturnsFalse()
    {
        // Act & Assert
        Service.IsValidTagsInput(null!).ShouldBeFalse();
    }

    [Fact]
    public void IsValidTagsInput_EmptyInput_ReturnsFalse()
    {
        // Act & Assert
        Service.IsValidTagsInput(string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void IsValidTagsInput_WhitespaceInput_ReturnsFalse()
    {
        // Act & Assert
        Service.IsValidTagsInput("   ").ShouldBeFalse();
        Service.IsValidTagsInput("\t").ShouldBeFalse();
        Service.IsValidTagsInput("\n").ShouldBeFalse();
    }
}
