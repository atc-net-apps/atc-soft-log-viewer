namespace AtcSoft.Log.Viewer.Tests.Updates;

public sealed class GitHubReleaseTests
{
    private const string ReleasePage = "https://github.com/atc-net-apps/atc-soft-log-viewer/releases/tag/v1.2.0";
    private const string InstallerDownload = "https://github.com/atc-net-apps/atc-soft-log-viewer/releases/download/v1.2.0/AtcSoft.Log.Viewer.Installer.msi";

    [Fact]
    public void LatestReleaseApiUrl_IsThisRepositorysLatestRelease()
        => UpdateCheck.LatestReleaseApiUrl.Should().Be(
            new Uri("https://api.github.com/repos/atc-net-apps/atc-soft-log-viewer/releases/latest"));

    [Fact]
    public void InstallerAssetName_IsTheNameOfTheInstallerProject()
    {
        // Arrange - the release workflow attaches the MSI the installer project builds, named after the project
        var setupFolder = Path.Combine(FindRepositoryRoot().FullName, "setup");

        // Act
        var installerProjects = Directory
            .GetFiles(setupFolder, "*.wixproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension);

        // Assert
        installerProjects.Should().Equal(Path.GetFileNameWithoutExtension(UpdateCheck.InstallerAssetName));
    }

    [Fact]
    public void Parse_AReleaseWithTheInstaller_GivesTheVersionAndTheInstallerDownload()
    {
        // Arrange
        var json = ReleaseJson(
            "v1.2.0",
            ("AtcSoft.Log.Viewer.Portable.zip", "https://example.com/portable.zip"),
            ("AtcSoft.Log.Viewer.Installer.msi", InstallerDownload));

        // Act
        var actual = GitHubRelease.Parse(json, UpdateCheck.InstallerAssetName);

        // Assert
        actual.Should().NotBeNull();
        actual!.Version.Should().Be(new Version(1, 2, 0));
        actual.ReleaseUrl.Should().Be(new Uri(ReleasePage));
        actual.DownloadUrl.Should().Be(new Uri(InstallerDownload));
    }

    [Fact]
    public void Parse_AReleaseWithOtherInstallersOnly_DownloadsFromTheReleasePage()
    {
        // Arrange - another app's MSI must never be offered
        var json = ReleaseJson("v1.2.0", ("Some.Other.Installer.msi", "https://example.com/other.msi"));

        // Act
        var actual = GitHubRelease.Parse(json, UpdateCheck.InstallerAssetName);

        // Assert
        actual!.DownloadUrl.Should().Be(new Uri(ReleasePage));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"message\":\"Not Found\"}")]
    [InlineData("{\"tag_name\":\"latest\",\"html_url\":\"https://github.com/x\"}")]
    public void Parse_SomethingThatIsNotARelease_IsNull(string json)
        => GitHubRelease.Parse(json, UpdateCheck.InstallerAssetName).Should().BeNull();

    [Theory]
    [InlineData("http://github.com/atc-net-apps/atc-soft-log-viewer/releases/download/v1.2.0/AtcSoft.Log.Viewer.Installer.msi")]
    [InlineData("file:///C:/Temp/AtcSoft.Log.Viewer.Installer.msi")]
    [InlineData("https://example.com/AtcSoft.Log.Viewer.Installer.msi")]
    [InlineData("https://github.com.example.com/AtcSoft.Log.Viewer.Installer.msi")]
    [InlineData("https://github.com@example.com/AtcSoft.Log.Viewer.Installer.msi")]
    [InlineData("https://example.com/github.com/AtcSoft.Log.Viewer.Installer.msi")]
    public void Parse_AnInstallerOutsideGitHub_DownloadsFromTheReleasePage(
        string installerLink)
    {
        // Arrange - the download is started by the shell, so only https links on github.com are trusted
        var json = ReleaseJson("v1.2.0", ("AtcSoft.Log.Viewer.Installer.msi", installerLink));

        // Act
        var actual = GitHubRelease.Parse(json, UpdateCheck.InstallerAssetName);

        // Assert
        actual!.DownloadUrl.Should().Be(new Uri(ReleasePage));
    }

    [Theory]
    [InlineData("http://github.com/atc-net-apps/atc-soft-log-viewer/releases/tag/v1.2.0")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("https://example.com/releases/tag/v1.2.0")]
    [InlineData("https://github.com.example.com/releases/tag/v1.2.0")]
    public void Parse_AReleasePageOutsideGitHub_IsNull(string releasePageLink)
    {
        // Arrange
        var json = ReleaseJson("v1.2.0", releasePageLink);

        // Act & Assert
        GitHubRelease.Parse(json, UpdateCheck.InstallerAssetName).Should().BeNull();
    }

    [Theory]
    [InlineData("1.2.0", "1.1.9", true)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.2.0", "1.3.0", false)]
    public void IsNewerThan_ComparesTheVersions(
        string latest,
        string current,
        bool expected)
    {
        // Arrange
        var release = new GitHubRelease(Version.Parse(latest), new Uri(ReleasePage), InstallerUrl: null);

        // Act & Assert
        release.IsNewerThan(Version.Parse(current)).Should().Be(expected);
    }

    [Fact]
    public void IsNewerThan_TheSameVersionWithARevision_IsFalse()
    {
        // Arrange - the exe reports 1.2.0.0, the tag says 1.2.0
        var release = new GitHubRelease(new Version(1, 2, 0), new Uri(ReleasePage), InstallerUrl: null);

        // Act & Assert
        release.IsNewerThan(new Version(1, 2, 0, 0)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/release.json")]
    public void GetLatestReleaseUrl_WithoutAValidOverride_IsGitHub(
        string? value)
        => UpdateCheck.GetLatestReleaseUrl(value).Should().Be(UpdateCheck.LatestReleaseApiUrl);

    [Fact]
    public void GetLatestReleaseUrl_WithAFile_IsTheFile()
    {
        // Arrange - the UI tests point the check at a release they wrote
        var file = Path.Combine(Path.GetTempPath(), "release.json");

        // Act
        var actual = UpdateCheck.GetLatestReleaseUrl(file);

        // Assert
        actual.Should().Be(new Uri(file));
    }

    [Fact]
    public async Task CheckAsync_FromAFile_ReadsTheRelease()
    {
        // Arrange
        var file = Path.Combine(Path.GetTempPath(), $"release-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, ReleaseJson("v9.0.0", ("AtcSoft.Log.Viewer.Installer.msi", InstallerDownload)), TestContext.Current.CancellationToken);
        using var sut = new GitHubReleaseService(new Uri(file));

        try
        {
            // Act
            var actual = await sut.CheckAsync(TestContext.Current.CancellationToken);

            // Assert
            actual.Status.Should().Be(ReleaseCheckStatus.Found);
            actual.Release!.Version.Should().Be(new Version(9, 0, 0));
            actual.Release.DownloadUrl.Should().Be(new Uri(InstallerDownload));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task CheckAsync_FromAMissingFile_IsUnavailable()
    {
        // Arrange
        using var sut = new GitHubReleaseService(new Uri(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json")));

        // Act
        var actual = await sut.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.Status.Should().Be(ReleaseCheckStatus.Unavailable);
        actual.Release.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ReleaseCheckStatus.NoRelease)]
    [InlineData(HttpStatusCode.Forbidden, ReleaseCheckStatus.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, ReleaseCheckStatus.Unavailable)]
    public async Task CheckAsync_TellsNoReleaseYetFromNoAnswer(
        HttpStatusCode statusCode,
        ReleaseCheckStatus expected)
    {
        // Arrange - GitHub answers 404 for releases/latest until the first release is published
        using var handler = new FixedAnswerHandler(statusCode);
        using var sut = new GitHubReleaseService(UpdateCheck.LatestReleaseApiUrl, handler);

        // Act
        var actual = await sut.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.Status.Should().Be(expected);
    }

    [Fact]
    public async Task CheckAsync_Offline_IsUnavailable()
    {
        // Arrange
        using var handler = new FixedAnswerHandler(error: new HttpRequestException("No network"));
        using var sut = new GitHubReleaseService(UpdateCheck.LatestReleaseApiUrl, handler);

        // Act
        var actual = await sut.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.Status.Should().Be(ReleaseCheckStatus.Unavailable);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    public void IsSkipped_ReadsTheSkipVariable(
        string? value,
        bool expected)
        => UpdateCheck.IsSkipped(value).Should().Be(expected);

    private static string ReleaseJson(
        string tag,
        params (string Name, string Url)[] assets)
        => ReleaseJson(tag, ReleasePage, assets);

    private static string ReleaseJson(
        string tag,
        string releasePage,
        params (string Name, string Url)[] assets)
        => new JsonObject
        {
            ["tag_name"] = tag,
            ["html_url"] = releasePage,
            ["assets"] = new JsonArray(assets
                .Select(a => (JsonNode)new JsonObject
                {
                    ["name"] = a.Name,
                    ["browser_download_url"] = a.Url,
                })
                .ToArray()),
        }.ToJsonString();

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AtcSoft.Log.Viewer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private sealed class FixedAnswerHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        HttpRequestException? error = null)
        : HttpMessageHandler
    {
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The caller disposes the response.")]
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => error is null
                ? Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent("{}") })
                : Task.FromException<HttpResponseMessage>(error);
    }
}