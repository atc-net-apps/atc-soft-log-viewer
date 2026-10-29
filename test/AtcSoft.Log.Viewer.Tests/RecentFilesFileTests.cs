namespace AtcSoft.Log.Viewer.Tests;

public sealed class RecentFilesFileTests : IDisposable
{
    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("recent-files-tests");

    private FileInfo RecentFiles
        => new(Path.Combine(folder.FullName, "RecentOpenFiles.json"));

    [Fact]
    public void Load_NoFile_IsEmpty()
        => RecentFilesFile.Load(RecentFiles).Should().BeEmpty();

    [Fact]
    public void Load_InvalidJson_IsEmpty()
    {
        // Arrange
        File.WriteAllText(RecentFiles.FullName, "{ not json");

        // Act
        var actual = RecentFilesFile.Load(RecentFiles);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Load_TheLayoutOfEarlierVersions_NewestFirstAndOnlyFilesThatExist()
    {
        // Arrange
        var older = CreateProfile("older.json");
        var newer = CreateProfile("newer.json");
        var missing = Path.Combine(folder.FullName, "missing.json");
        var json = $$"""
            {
              "RecentOpenFiles": [
                { "TimeStamp": "2026-10-28T10:00:00", "FilePath": {{JsonSerializer.Serialize(older)}} },
                { "TimeStamp": "2026-10-30T10:00:00", "FilePath": {{JsonSerializer.Serialize(missing)}} },
                { "TimeStamp": "2026-10-29T10:00:00", "FilePath": {{JsonSerializer.Serialize(newer)}} }
              ]
            }
            """;
        File.WriteAllText(RecentFiles.FullName, json);

        // Act
        var actual = RecentFilesFile.Load(RecentFiles);

        // Assert
        actual.Select(x => x.FilePath).Should().Equal(newer, older);
        actual[0].TimeStamp.Should().Be(new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void Add_PutsTheFileFirstAndKeepsOneEntryPerFile()
    {
        // Arrange
        var first = CreateProfile("first.json");
        var second = CreateProfile("second.json");
        RecentFilesFile.Add(RecentFiles, first, new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Local));
        RecentFilesFile.Add(RecentFiles, second, new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Local));

        // Act
        var actual = RecentFilesFile.Add(RecentFiles, first.ToUpperInvariant(), new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Local));

        // Assert
        actual.Select(x => x.FilePath).Should().Equal(first.ToUpperInvariant(), second);
        RecentFilesFile.Load(RecentFiles).Should().Equal(actual, "Add saves what it returns");
    }

    [Fact]
    public void Add_WritesTheLayoutEarlierVersionsRead()
    {
        // Arrange
        var profile = CreateProfile("profile.json");

        // Act
        RecentFilesFile.Add(RecentFiles, profile, new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Local));

        // Assert
        var json = JsonNode.Parse(File.ReadAllText(RecentFiles.FullName))!;
        var item = json["RecentOpenFiles"]!.AsArray().Should().ContainSingle().Which!;
        ((string)item["FilePath"]!).Should().Be(profile);
        ((DateTime)item["TimeStamp"]!).Should().Be(new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Local));
    }

    [Fact]
    public void Add_CreatesTheFolder()
    {
        // Arrange
        var profile = CreateProfile("profile.json");
        var file = new FileInfo(Path.Combine(folder.FullName, "new-root", "RecentOpenFiles.json"));

        // Act
        RecentFilesFile.Add(file, profile, DateTime.Now);

        // Assert
        File.Exists(file.FullName).Should().BeTrue();
    }

    public void Dispose()
    {
        try
        {
            folder.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp directory
        }
    }

    private string CreateProfile(string name)
    {
        var path = Path.Combine(folder.FullName, name);
        File.WriteAllText(path, "{}");
        return path;
    }
}