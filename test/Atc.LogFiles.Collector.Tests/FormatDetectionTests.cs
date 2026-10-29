namespace Atc.LogFiles.Collector.Tests;

/// <summary>
/// The format of a folder is found by trying the parsers in turn, so each parser must accept only its own
/// format's lines. A log4net line was read as NLog, so log4net folders opened as NLog.
/// </summary>
public sealed class FormatDetectionTests
{
    private const string SerilogLine = "2026-10-30 00:08:20.678 +01:00 [DBG] Order 61336 received from customer 994";
    private const string NLogLine = "2026-10-30 00:08:20.6781 DEBUG Order 61336 received from customer 994";
    private const string Log4NetLine = "2026-10-30 00:08:20,678 [16] DEBUG Contoso.Orders.Worker - Order 61336 received from customer 994";
    private const string Log4NetInfoLine = "2026-10-30 00:08:21,068 [16] INFO  Contoso.Orders.Worker - User 1500 signed in";

    public static TheoryData<string, string, bool> LinesAndParsers
        => new()
        {
            { "serilog", SerilogLine, true },
            { "serilog", NLogLine, false },
            { "serilog", Log4NetLine, false },
            { "nlog", SerilogLine, false },
            { "nlog", NLogLine, true },
            { "nlog", Log4NetLine, false },
            { "nlog", Log4NetInfoLine, false },
            { "log4net", SerilogLine, false },
            { "log4net", NLogLine, false },
            { "log4net", Log4NetLine, true },
            { "log4net", Log4NetInfoLine, true },
        };

    [Theory]
    [MemberData(nameof(LinesAndParsers))]
    public void ParseRootLine_AcceptsOnlyItsOwnFormat(
        string parser,
        string line,
        bool expected)
    {
        // Act
        var actual = parser switch
        {
            "serilog" => new SerilogFileExtractor().ParseRootLine("app", "app", 1, line),
            "nlog" => new NLogFileExtractor().ParseRootLine("app", "app", 1, line),
            _ => new Log4NetFileExtractor().ParseRootLine("app", "app", 1, line),
        };

        // Assert
        (actual is not null).Should().Be(expected, line);
    }

    [Fact]
    public void ParseRootLine_NLog_ReadsTheLevelAndMessage()
    {
        // Act
        var actual = new NLogFileExtractor().ParseRootLine("app", "app", 1, NLogLine);

        // Assert
        actual!.LogLevel.Should().Be(LogLevel.Debug);
        actual.MessageFull.Should().Be("Order 61336 received from customer 994");
        actual.TimeStamp.Should().Be(new DateTime(2026, 10, 30, 0, 8, 20, 678, DateTimeKind.Unspecified).AddTicks(1_000));
    }
}