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
        => new JsonObject
        {
            ["tag_name"] = tag,
            ["html_url"] = ReleasePage,
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
}