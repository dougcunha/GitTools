using GitTools.Utils;

namespace GitTools.Tests.Utils;

/// <summary>
/// Unit tests for the WildcardMatcher class.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class WildcardMatcherTests
{
    [Theory]
    [InlineData("test*", true)]
    [InlineData("test?", true)]
    [InlineData("*test", true)]
    [InlineData("te*st", true)]
    [InlineData("test", false)]
    [InlineData("", false)]
    public void IsWildcardPattern_ShouldReturnExpectedResult(string pattern, bool expected)
    {
        // Act
        var result = WildcardMatcher.IsWildcardPattern(pattern);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("v1.*", new[] { "v1.0", "v1.1", "v1.2.3", "v2.0" }, new[] { "v1.0", "v1.1", "v1.2.3" })]
    [InlineData("v?.0", new[] { "v1.0", "v2.0", "v10.0", "v1.1" }, new[] { "v1.0", "v2.0" })]
    [InlineData("exact", new[] { "exact", "inexact", "exactly" }, new[] { "exact" })]
    [InlineData("*beta*", new[] { "v1.0-beta", "beta-test", "release", "alpha-beta-gamma" }, new[] { "v1.0-beta", "beta-test", "alpha-beta-gamma" })]
    [InlineData("release-?", new[] { "release-1", "release-a", "release-10", "release" }, new[] { "release-1", "release-a" })]
    public void MatchItems_ShouldReturnMatchingItems(string pattern, string[] items, string[] expected)
    {
        // Act
        var result = WildcardMatcher.MatchItems(items, pattern);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void MatchItems_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var items = Array.Empty<string>();
        const string PATTERN = "test*";

        // Act
        var result = WildcardMatcher.MatchItems(items, PATTERN);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void MatchItems_WithNonMatchingPattern_ShouldReturnEmptyList()
    {
        // Arrange
        var items = new[] { "v1.0", "v2.0", "beta" };
        const string PATTERN = "release-*";

        // Act
        var result = WildcardMatcher.MatchItems(items, PATTERN);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ConvertToRegex_ShouldCreateValidRegex()
    {
        // Arrange
        const string PATTERN = "v1.*";

        // Act
        var regex = WildcardMatcher.ConvertToRegex(PATTERN);

        // Assert
        regex.ShouldNotBeNull();
        regex.IsMatch("v1.0").ShouldBeTrue();
        regex.IsMatch("v1.2.3").ShouldBeTrue();
        regex.IsMatch("v2.0").ShouldBeFalse();
    }
}
