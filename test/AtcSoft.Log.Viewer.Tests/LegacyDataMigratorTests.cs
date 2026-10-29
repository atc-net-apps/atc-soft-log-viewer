namespace AtcSoft.Log.Viewer.Tests;

public sealed class LegacyDataMigratorTests : IDisposable
{
    private readonly DirectoryInfo testRoot = Directory.CreateTempSubdirectory("atc-log-viewer-migration-tests");
    private readonly DirectoryInfo legacyRoot;
    private readonly DirectoryInfo newRoot;

    public LegacyDataMigratorTests()
    {
        legacyRoot = new DirectoryInfo(Path.Combine(testRoot.FullName, "ProgramData", "ATC", "atc-logviewer"));
        newRoot = new DirectoryInfo(Path.Combine(testRoot.FullName, "LocalAppData", "AtcSoft", "AtcSoft.Log.Viewer"));
    }

    [Fact]
    public void CopyIfNeeded_CopiesTheLegacyFolderWhenTheNewFolderIsMissing()
    {
        // Arrange
        WriteLegacyFile(Path.Combine("Profiles", "orders.json"), "{\"Name\":\"orders\"}");
        WriteLegacyFile("appsettings.custom.json", "{\"Application\":{}}");

        // Act
        var copied = LegacyDataMigrator.CopyIfNeeded(legacyRoot, newRoot);

        // Assert
        copied.Should().BeTrue();
        File.ReadAllText(Path.Combine(newRoot.FullName, "Profiles", "orders.json")).Should().Be("{\"Name\":\"orders\"}");
        File.ReadAllText(Path.Combine(newRoot.FullName, "appsettings.custom.json")).Should().Be("{\"Application\":{}}");
    }

    [Fact]
    public void CopyIfNeeded_LeavesTheLegacyFolderInPlace()
    {
        // Arrange
        WriteLegacyFile(Path.Combine("Profiles", "orders.json"), "{}");

        // Act
        LegacyDataMigrator.CopyIfNeeded(legacyRoot, newRoot);

        // Assert
        File.Exists(Path.Combine(legacyRoot.FullName, "Profiles", "orders.json")).Should().BeTrue();
    }

    [Fact]
    public void CopyIfNeeded_DoesNothingWhenTheNewFolderExists()
    {
        // Arrange
        WriteLegacyFile(Path.Combine("Profiles", "orders.json"), "{\"Name\":\"legacy\"}");
        Directory.CreateDirectory(Path.Combine(newRoot.FullName, "Profiles"));
        File.WriteAllText(Path.Combine(newRoot.FullName, "Profiles", "orders.json"), "{\"Name\":\"new\"}");

        // Act
        var copied = LegacyDataMigrator.CopyIfNeeded(legacyRoot, newRoot);

        // Assert
        copied.Should().BeFalse();
        File.ReadAllText(Path.Combine(newRoot.FullName, "Profiles", "orders.json")).Should().Be("{\"Name\":\"new\"}");
    }

    [Fact]
    public void CopyIfNeeded_DoesNothingWithoutALegacyFolder()
    {
        // Act
        var copied = LegacyDataMigrator.CopyIfNeeded(legacyRoot, newRoot);

        // Assert
        copied.Should().BeFalse();
        newRoot.Refresh();
        newRoot.Exists.Should().BeFalse();
    }

    [Fact]
    public void CopyIfNeeded_PointsRecentFilesInTheLegacyFolderToTheNewFolder()
    {
        // Arrange
        // The folder part in other casing: Windows paths are case-insensitive
        var legacyProfile = Path.Combine(legacyRoot.FullName.ToUpperInvariant(), "Profiles", "orders.json");
        var otherProfile = Path.Combine(testRoot.FullName, "elsewhere", "shared.json");
        WriteLegacyFile(Path.Combine("Profiles", "orders.json"), "{}");
        var recentOpenFiles = $$"""
          {
            "RecentOpenFiles": [
              { "TimeStamp": "2026-10-29T10:00:00", "FilePath": {{JsonSerializer.Serialize(legacyProfile)}} },
              { "TimeStamp": "2026-10-28T10:00:00", "FilePath": {{JsonSerializer.Serialize(otherProfile)}} }
            ]
          }
          """;
        WriteLegacyFile("RecentOpenFiles.json", recentOpenFiles);

        // Act
        LegacyDataMigrator.CopyIfNeeded(legacyRoot, newRoot);

        // Assert
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(newRoot.FullName, "RecentOpenFiles.json")))!;
        var filePaths = json["RecentOpenFiles"]!.AsArray().Select(x => (string)x!["FilePath"]!).ToList();
        filePaths.Should().Equal(
            Path.Combine(newRoot.FullName, "Profiles", "orders.json"),
            otherProfile);
        ((string)json["RecentOpenFiles"]![0]!["TimeStamp"]!).Should().Be("2026-10-29T10:00:00");
    }

    public void Dispose()
        => testRoot.Delete(recursive: true);

    private void WriteLegacyFile(
        string relativePath,
        string content)
    {
        var path = Path.Combine(legacyRoot.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}