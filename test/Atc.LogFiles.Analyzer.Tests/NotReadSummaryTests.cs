namespace Atc.LogFiles.Analyzer.Tests;

public sealed class NotReadSummaryTests
{
    private static readonly string NewLine = Environment.NewLine;

    [Fact]
    public void ToShortText_Nothing_IsEmpty()
        => new NotReadSummary([], []).ToShortText().Should().BeEmpty();

    [Fact]
    public void ToShortText_LinesAndFiles_CountsBoth()
    {
        // Arrange
        var sut = new NotReadSummary(
            [new LinesNotRead(@"C:\logs\orders.log", 5_810, 31, "x", LogFileCollectorType.Log4Net), new LinesNotRead(@"C:\logs\payments.log", 1, 9, "y", LogFileCollectorType.Log4Net)],
            [new FileNotRead(@"C:\logs\orders-1.log", LogFileCollectorType.Log4Net)]);

        // Act
        var actual = sut.ToShortText();

        // Assert
        actual.Should().Be("Not read: 5,811 lines, 1 file");
    }

    [Theory]
    [InlineData(1, 0, "Not read: 1 line")]
    [InlineData(0, 2, "Not read: 2 files")]
    public void ToShortText_OneKind_SaysOnlyThat(
        int lineCount,
        int fileCount,
        string expected)
    {
        // Arrange
        var sut = new NotReadSummary(
            lineCount == 0 ? [] : [new LinesNotRead(@"C:\logs\orders.log", lineCount, 1, "x", LogFileCollectorType.Log4Net)],
            [.. Enumerable.Range(1, fileCount).Select(i => new FileNotRead($@"C:\logs\other-{i}.log", LogFileCollectorType.NLog))]);

        // Act
        var actual = sut.ToShortText();

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void ToDetails_NamesTheFilesAndShowsAnExampleLine()
    {
        // Arrange
        var sut = new NotReadSummary(
            [new LinesNotRead(@"C:\logs\orders.log", 5_810, 31, "2026-10-30 00:08:20,678 [16] DEBUG Worker - Order 1", LogFileCollectorType.Log4Net)],
            [new FileNotRead(@"C:\logs\orders-1.log", LogFileCollectorType.Log4Net)]);

        // Act
        var actual = sut.ToDetails();

        // Assert
        actual.Should().Be(
            "These lines are in another format:" + NewLine +
            "• orders.log: 5,810 lines (Log4Net), the first is line 31:" + NewLine +
            "    2026-10-30 00:08:20,678 [16] DEBUG Worker - Order 1" + NewLine +
            NewLine +
            "These files are in another format:" + NewLine +
            "• orders-1.log (Log4Net)" + NewLine +
            NewLine +
            "The viewer reads one format per folder. Open a folder with files in one format.");
    }

    [Fact]
    public void ToDetails_ManyFiles_ListsTheFirstOnes()
    {
        // Arrange
        var sut = new NotReadSummary(
            [],
            [.. Enumerable.Range(1, 12).Select(i => new FileNotRead($@"C:\logs\other-{i:00}.log", LogFileCollectorType.NLog))]);

        // Act
        var actual = sut.ToDetails(maxItems: 10);

        // Assert
        actual.Should().Contain("• other-10.log (NLog)").And.NotContain("other-11.log").And.Contain("… and 2 more");
    }

    [Fact]
    public void ToDetails_LongLine_IsShortened()
    {
        // Arrange
        var sut = new NotReadSummary([new LinesNotRead(@"C:\logs\orders.log", 1, 1, new string('x', 300), LogFileCollectorType.Log4Net)], []);

        // Act
        var actual = sut.ToDetails();

        // Assert
        actual.Should().Contain(new string('x', 120) + "…").And.NotContain(new string('x', 121));
    }
}