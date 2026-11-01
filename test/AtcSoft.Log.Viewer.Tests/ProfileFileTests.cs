namespace AtcSoft.Log.Viewer.Tests;

public sealed class ProfileFileTests : IDisposable
{
    private static readonly FileInfo OrdersProfile = new(Path.Combine(AppContext.BaseDirectory, "TestData", "profile-orders.json"));

    private readonly DirectoryInfo tempDirectory = Directory.CreateTempSubdirectory("atcsoft-log-viewer-tests-");

    public void Dispose()
        => tempDirectory.Delete(recursive: true);

    [Fact]
    public async Task LoadAsync_ReadsAllSettings()
    {
        // Act
        var actual = await ProfileFile.LoadAsync(OrdersProfile, TestContext.Current.CancellationToken);

        // Assert
        actual.Name.Should().Be("Orders");
        actual.LogFolder.Should().Be(@"C:\logs\orders");
        actual.CollectorType.Should().Be(LogFileCollectorType.Serilog);
        actual.Highlights.Should().HaveCount(2);
        actual.Highlights[0].Text.Should().Be("timeout");
        actual.Highlights[0].IgnoreCasing.Should().BeTrue();
        actual.Highlights[0].Foreground.Should().Be("#FFFF0000");
        actual.Highlights[0].Background.Should().Be("#00FFFFFF");
        actual.Highlights[1].Text.Should().Be("NullReferenceException");
        actual.CollectorConfiguration.MaxDaysBack.Should().Be(7);
        actual.CollectorConfiguration.MonitorFiles.Should().BeFalse();
        actual.CollectorConfiguration.FileNameTerms.Should().Equal("orders", "api");
    }

    [Fact]
    public async Task SaveAsync_WritesTheSameFormatAsEarlierVersions()
    {
        // Arrange - profile files of earlier versions must stay readable, and a saved profile must look the same
        var profile = await ProfileFile.LoadAsync(OrdersProfile, TestContext.Current.CancellationToken);
        var file = new FileInfo(Path.Combine(tempDirectory.FullName, "orders.json"));

        // Act
        await ProfileFile.SaveAsync(file, profile, TestContext.Current.CancellationToken);

        // Assert
        var expected = await File.ReadAllTextAsync(OrdersProfile.FullName, TestContext.Current.CancellationToken);
        var actual = await File.ReadAllTextAsync(file.FullName, TestContext.Current.CancellationToken);
        NormalizeLineEndings(actual).Should().Be(NormalizeLineEndings(expected));
    }

    [Fact]
    public async Task LoadAsync_FileWithoutProfile_Throws()
    {
        // Arrange
        var file = new FileInfo(Path.Combine(tempDirectory.FullName, "empty.json"));
        await File.WriteAllTextAsync(file.FullName, "null", TestContext.Current.CancellationToken);

        // Act
        var act = () => ProfileFile.LoadAsync(file, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    private static string NormalizeLineEndings(string text)
        => text.ReplaceLineEndings("\n").TrimEnd('\n');
}