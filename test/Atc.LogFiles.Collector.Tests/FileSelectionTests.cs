namespace Atc.LogFiles.Collector.Tests;

/// <summary>
/// Which files of a folder are read: by extension, by file name terms, and by the days back.
/// </summary>
public sealed class FileSelectionTests : IDisposable
{
    private static readonly string[] SerilogExtensions = ["log", "txt"];

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("atc-file-selection-tests");

    [Fact]
    public void WithoutTerms_EveryFileWithALogExtensionIsSelected()
    {
        // Arrange
        CreateFiles("orders.log", "billing.txt", "readme.md", "orders.log.bak");

        // Act
        var actual = SelectedNames(new LogFileCollectorConfiguration());

        // Assert
        actual.Should().BeEquivalentTo("orders.log", "billing.txt");
    }

    [Fact]
    public void WithOneTerm_OnlyFilesWhoseNameContainsItAreSelected()
    {
        // Arrange
        CreateFiles("orders-20261030.log", "billing-20261030.log", "ORDERS-20261029.txt");

        // Act
        var actual = SelectedNames(new LogFileCollectorConfiguration { FileNameTerms = ["orders"] });

        // Assert - the term is not case sensitive
        actual.Should().BeEquivalentTo("orders-20261030.log", "ORDERS-20261029.txt");
    }

    [Fact]
    public void WithSeveralTerms_AFileIsSelectedOnce_EvenWhenItMatchesMoreThanOne()
    {
        // Arrange
        CreateFiles("orders-api.log", "billing-api.log", "auth.log");

        // Act
        var actual = SelectedNames(new LogFileCollectorConfiguration { FileNameTerms = ["orders", "api"] });

        // Assert
        actual.Should().BeEquivalentTo("orders-api.log", "billing-api.log");
    }

    [Fact]
    public void WithTermsNoFileMatches_NothingIsSelected()
    {
        // Arrange
        CreateFiles("orders.log");

        // Act
        var actual = SelectedNames(new LogFileCollectorConfiguration { FileNameTerms = ["billing"] });

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void FilesNotWrittenWithinTheDaysBack_AreNotSelected()
    {
        // Arrange
        CreateFiles("recent.log", "old.log");
        File.SetLastWriteTime(Path.Combine(directory.FullName, "old.log"), DateTime.Now.AddDays(-8));

        // Act
        var actual = SelectedNames(new LogFileCollectorConfiguration { MaxDaysBack = 7 });

        // Assert
        actual.Should().BeEquivalentTo("recent.log");
    }

    [Fact]
    public void ZeroDaysBack_SelectsTheLastYear()
    {
        // Arrange
        CreateFiles("months-ago.log", "years-ago.log");
        File.SetLastWriteTime(Path.Combine(directory.FullName, "months-ago.log"), DateTime.Now.AddDays(-300));
        File.SetLastWriteTime(Path.Combine(directory.FullName, "years-ago.log"), DateTime.Now.AddDays(-400));

        // Act
        var actual = SelectedNames(new LogFileCollectorConfiguration { MaxDaysBack = 0 });

        // Assert
        actual.Should().BeEquivalentTo("months-ago.log");
    }

    public void Dispose()
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp directory
        }
    }

    private void CreateFiles(params string[] names)
    {
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(directory.FullName, name), "2026-10-30 00:08:20.678 +01:00 [INF] Started");
        }
    }

    private string[] SelectedNames(LogFileCollectorConfiguration config)
        => LogFileCollectorBase
            .GetFiles(directory, config, SerilogExtensions)
            .Select(x => x.Name)
            .ToArray();
}