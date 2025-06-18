using GitTools.Services;
using NSubstitute.ExceptionExtensions;

namespace GitTools.Tests.Services;

/// <summary>
/// Tests for <see cref="TagSearchService"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TagSearchServiceTests
{
    private readonly IGitRepositoryScanner _gitScanner = Substitute.For<IGitRepositoryScanner>();
    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly TagSearchService _service;

    public TagSearchServiceTests()
    {
        _service = new TagSearchService(_gitScanner, _gitService);
    }

    [Fact]
    public async Task SearchRepositoriesWithTagsAsync_WithMatchingTags_ReturnsRepositoriesWithTags()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tagsToSearch = new[] { "v1.0.0", "v2.0.0" };
        var repositories = new List<string> { @"C:\repos\repo1", @"C:\repos\repo2" };
        var repo1Tags = new List<string> { "v1.0.0", "v1.1.0" };
        var repo2Tags = new List<string> { "v2.0.0", "v2.1.0" };

        _gitScanner.Scan(BASE_FOLDER).Returns(repositories);
        _gitService.GetAllTagsAsync(@"C:\repos\repo1").Returns(repo1Tags);
        _gitService.GetAllTagsAsync(@"C:\repos\repo2").Returns(repo2Tags);

        // Act
        var result = await _service.SearchRepositoriesWithTagsAsync(BASE_FOLDER, tagsToSearch);

        // Assert
        result.RepositoriesWithTags.Count.ShouldBe(2);
        result.RepositoriesWithTags.ShouldContain(@"C:\repos\repo1");
        result.RepositoriesWithTags.ShouldContain(@"C:\repos\repo2");
        result.RepositoryTagsMap[@"C:\repos\repo1"].ShouldBe(["v1.0.0"]);
        result.RepositoryTagsMap[@"C:\repos\repo2"].ShouldBe(["v2.0.0"]);
        result.ScanErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchRepositoriesWithTagsAsync_WithNoMatchingTags_ReturnsEmptyResult()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tagsToSearch = new[] { "v3.0.0" };
        var repositories = new List<string> { @"C:\repos\repo1", @"C:\repos\repo2" };
        var repo1Tags = new List<string> { "v1.0.0", "v1.1.0" };
        var repo2Tags = new List<string> { "v2.0.0", "v2.1.0" };

        _gitScanner.Scan(BASE_FOLDER).Returns(repositories);
        _gitService.GetAllTagsAsync(@"C:\repos\repo1").Returns(repo1Tags);
        _gitService.GetAllTagsAsync(@"C:\repos\repo2").Returns(repo2Tags);

        // Act
        var result = await _service.SearchRepositoriesWithTagsAsync(BASE_FOLDER, tagsToSearch);

        // Assert
        result.RepositoriesWithTags.ShouldBeEmpty();
        result.RepositoryTagsMap.ShouldBeEmpty();
        result.ScanErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchRepositoriesWithTagsAsync_WithWildcardTags_ReturnsMatchingRepositories()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tagsToSearch = new[] { "v1.*" };
        var repositories = new List<string> { @"C:\repos\repo1" };
        var repo1Tags = new List<string> { "v1.0.0", "v1.1.0", "v2.0.0" };

        _gitScanner.Scan(BASE_FOLDER).Returns(repositories);
        _gitService.GetAllTagsAsync(@"C:\repos\repo1").Returns(repo1Tags);

        // Act
        var result = await _service.SearchRepositoriesWithTagsAsync(BASE_FOLDER, tagsToSearch);

        // Assert
        result.RepositoriesWithTags.ShouldHaveSingleItem();
        result.RepositoriesWithTags.ShouldContain(@"C:\repos\repo1");
        result.RepositoryTagsMap[@"C:\repos\repo1"].ShouldBe(["v1.0.0", "v1.1.0"]);
        result.ScanErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchRepositoriesWithTagsAsync_WithGitServiceException_RecordsScanError()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tagsToSearch = new[] { "v1.0.0" };
        var repositories = new List<string> { @"C:\repos\repo1" };
        var exception = new InvalidOperationException("Git error");

        _gitScanner.Scan(BASE_FOLDER).Returns(repositories);
        _gitService.GetAllTagsAsync(@"C:\repos\repo1").ThrowsAsync(exception);

        // Act
        var result = await _service.SearchRepositoriesWithTagsAsync(BASE_FOLDER, tagsToSearch);

        // Assert
        result.RepositoriesWithTags.ShouldBeEmpty();
        result.RepositoryTagsMap.ShouldBeEmpty();
        result.ScanErrors.ShouldHaveSingleItem();
        result.ScanErrors[@"C:\repos\repo1"].ShouldBe(exception);
    }

    [Fact]
    public async Task SearchRepositoriesWithTagsAsync_WithProgressCallback_CallsProgressCallback()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tagsToSearch = new[] { "v1.0.0" };
        var repositories = new List<string> { @"C:\repos\repo1", @"C:\repos\repo2" };
        var progressCallbacks = new List<string>();

        _gitScanner.Scan(BASE_FOLDER).Returns(repositories);
        _gitService.GetAllTagsAsync(Arg.Any<string>()).Returns([]);

        void ProgressCallback(string repoName) => progressCallbacks.Add(repoName);

        // Act
        await _service.SearchRepositoriesWithTagsAsync(BASE_FOLDER, tagsToSearch, ProgressCallback);

        // Assert
        progressCallbacks.ShouldBe(["repo1", "repo2"]);
    }

    [Fact]
    public async Task SearchRepositoriesWithTagsAsync_WithEmptyTagsArray_ProcessesAllRepositories()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tagsToSearch = Array.Empty<string>();
        var repositories = new List<string> { @"C:\repos\repo1" };

        _gitScanner.Scan(BASE_FOLDER).Returns(repositories);
        _gitService.GetAllTagsAsync(@"C:\repos\repo1").Returns(["v1.0.0", "v2.0.0"]);

        // Act
        var result = await _service.SearchRepositoriesWithTagsAsync(BASE_FOLDER, tagsToSearch);

        // Assert
        result.RepositoriesWithTags.ShouldBeEmpty();
        result.RepositoryTagsMap.ShouldBeEmpty();
        result.ScanErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchTagsInRepositoryAsync_WithMatchingTags_ReturnsMatchingTags()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\repos\repo1";
        var tagsToSearch = new[] { "v1.0.0", "v2.0.0" };
        var allTags = new List<string> { "v1.0.0", "v1.1.0", "v2.0.0", "v3.0.0" };

        _gitService.GetAllTagsAsync(REPOSITORY_PATH).Returns(allTags);

        // Act
        var result = await _service.SearchTagsInRepositoryAsync(REPOSITORY_PATH, tagsToSearch);

        // Assert
        result.ShouldBe(["v1.0.0", "v2.0.0"]);
    }

    [Fact]
    public async Task SearchTagsInRepositoryAsync_WithWildcardPattern_ReturnsMatchingTags()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\repos\repo1";
        var tagsToSearch = new[] { "v1.*" };
        var allTags = new List<string> { "v1.0.0", "v1.1.0", "v2.0.0" };

        _gitService.GetAllTagsAsync(REPOSITORY_PATH).Returns(allTags);

        // Act
        var result = await _service.SearchTagsInRepositoryAsync(REPOSITORY_PATH, tagsToSearch);

        // Assert
        result.ShouldBe(["v1.0.0", "v1.1.0"]);
    }

    [Fact]
    public async Task SearchTagsInRepositoryAsync_WithNoMatchingTags_ReturnsEmptyList()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\repos\repo1";
        var tagsToSearch = new[] { "v5.0.0" };
        var allTags = new List<string> { "v1.0.0", "v1.1.0", "v2.0.0" };

        _gitService.GetAllTagsAsync(REPOSITORY_PATH).Returns(allTags);

        // Act
        var result = await _service.SearchTagsInRepositoryAsync(REPOSITORY_PATH, tagsToSearch);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchTagsInRepositoryAsync_WithDuplicateMatches_ReturnsDistinctTags()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\repos\repo1";
        var tagsToSearch = new[] { "v1.*", "v1.0.0" }; // Both patterns match v1.0.0
        var allTags = new List<string> { "v1.0.0", "v1.1.0", "v2.0.0" };

        _gitService.GetAllTagsAsync(REPOSITORY_PATH).Returns(allTags);

        // Act
        var result = await _service.SearchTagsInRepositoryAsync(REPOSITORY_PATH, tagsToSearch);

        // Assert
        result.ShouldBe(["v1.0.0", "v1.1.0"]);
        result.Count.ShouldBe(2); // Ensure no duplicates
    }

    [Fact]
    public async Task SearchTagsInRepositoryAsync_WithEmptyTagsArray_ReturnsEmptyList()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\repos\repo1";
        var tagsToSearch = Array.Empty<string>();
        var allTags = new List<string> { "v1.0.0", "v1.1.0", "v2.0.0" };

        _gitService.GetAllTagsAsync(REPOSITORY_PATH).Returns(allTags);

        // Act
        var result = await _service.SearchTagsInRepositoryAsync(REPOSITORY_PATH, tagsToSearch);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchTagsInRepositoryAsync_WithEmptyRepositoryTags_ReturnsEmptyList()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\repos\repo1";
        var tagsToSearch = new[] { "v1.0.0" };
        List<string> allTags = [];

        _gitService.GetAllTagsAsync(REPOSITORY_PATH).Returns(allTags);

        // Act
        var result = await _service.SearchTagsInRepositoryAsync(REPOSITORY_PATH, tagsToSearch);

        // Assert
        result.ShouldBeEmpty();
    }
}
