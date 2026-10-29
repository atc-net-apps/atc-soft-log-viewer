namespace AtcSoft.Log.Viewer.Tests;

public sealed class AppDataPathsTests
{
    [Fact]
    public void ForCurrentUser_UsesTheVendorAndAppFolderInLocalAppData()
    {
        // Act
        var actual = AppDataPaths.ForCurrentUser();

        // Assert
        actual.Root.FullName.Should().Be(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtcSoft",
                "AtcSoft.Log.Viewer"));
    }

    [Fact]
    public void Files_AreInTheRootFolder()
    {
        // Arrange
        var root = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "app-root"));

        // Act
        var actual = new AppDataPaths(root);

        // Assert
        actual.ProfilesDirectory.FullName.Should().Be(Path.Combine(root.FullName, "Profiles"));
        actual.RecentOpenFilesFile.FullName.Should().Be(Path.Combine(root.FullName, "RecentOpenFiles.json"));
        actual.CustomAppSettingsFile.FullName.Should().Be(Path.Combine(root.FullName, "appsettings.custom.json"));
    }

    [Fact]
    public void LegacyRoot_IsTheOldProgramDataFolder()
        => AppDataPaths.LegacyRoot.FullName.Should().Be(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ATC",
                "atc-logviewer"));
}