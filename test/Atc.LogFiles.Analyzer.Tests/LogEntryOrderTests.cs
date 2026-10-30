namespace Atc.LogFiles.Analyzer.Tests;

public sealed class LogEntryOrderTests
{
    private static readonly DateTime BaseTime = new(2026, 10, 30, 9, 0, 0, DateTimeKind.Unspecified);

    private static readonly AtcLogEntry[] Shown =
    [
        Entry(0, "a.log", 1),
        Entry(1, "a.log", 2),
        Entry(1, "b.log", 1),
        Entry(3, "a.log", 3),
    ];

    [Fact]
    public void FindInsertIndex_ANewerEntry_GoesAtTheEnd()
        => LogEntryOrder.FindInsertIndex(Shown, Entry(4, "b.log", 2), x => x).Should().Be(4);

    [Fact]
    public void FindInsertIndex_AnOlderEntryFromASlowerFile_GoesAtItsTime()
        => LogEntryOrder.FindInsertIndex(Shown, Entry(2, "c.log", 1), x => x).Should().Be(3);

    [Fact]
    public void FindInsertIndex_TheOldestEntry_GoesFirst()
        => LogEntryOrder.FindInsertIndex(Shown, Entry(-1, "c.log", 1), x => x).Should().Be(0);

    [Fact]
    public void FindInsertIndex_TheSameTimeStamp_IsOrderedByFileAndLine()
        => LogEntryOrder.FindInsertIndex(Shown, Entry(1, "a.log", 5), x => x).Should().Be(2);

    [Fact]
    public void FindInsertIndex_ReadsTheEntryOfEachRow()
    {
        // Arrange - the list shows rows that wrap the entries
        var rows = Shown.Select(x => new { Entry = x }).ToArray();

        // Act & Assert
        LogEntryOrder.FindInsertIndex(rows, Entry(2, "c.log", 1), x => x.Entry).Should().Be(3);
    }

    private static AtcLogEntry Entry(
        int seconds,
        string file,
        long line)
        => new(file, "app", line, BaseTime.AddSeconds(seconds), LogLevel.Information, "m", "m");
}