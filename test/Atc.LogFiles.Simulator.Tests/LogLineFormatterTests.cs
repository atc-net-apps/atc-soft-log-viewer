namespace Atc.LogFiles.Simulator.Tests;

/// <summary>
/// The simulator's lines must be read correctly by the collectors, or the sample would report false problems.
/// </summary>
public sealed class LogLineFormatterTests
{
    private static readonly DateTimeOffset TimeStamp = new(2026, 7, 15, 9, 30, 12, 345, TimeSpan.FromHours(2));

    public static TheoryData<LogFormat, LogLevel> FormatsAndLevels
    {
        get
        {
            var data = new TheoryData<LogFormat, LogLevel>();
            foreach (var format in Enum.GetValues<LogFormat>())
            {
                foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
                {
                    data.Add(format, level);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(FormatsAndLevels))]
    public void Format_IsReadByTheCollectorOfTheFormat(
        LogFormat format,
        LogLevel level)
    {
        // Act
        var line = LogLineFormatter.Format(format, TimeStamp, level, "Contoso.Orders.Worker", 8, "Order 42 received");
        var actual = Parse(format, line);

        // Assert
        actual.Should().NotBeNull(line);
        actual!.LogLevel.Should().Be(level);
        actual.MessageShort.Should().Be("Order 42 received");
        actual.TimeStamp.Should().BeCloseTo(ExpectedTimeStamp(format), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Format_Serilog_WritesTheOffsetOfTheTimeStamp()
    {
        // Act
        var actual = LogLineFormatter.Format(LogFormat.Serilog, TimeStamp, LogLevel.Information, "Contoso.Orders.Worker", 8, "Order 42 received");

        // Assert
        actual.Should().Be("2026-07-15 09:30:12.345 +02:00 [INF] Order 42 received");
    }

    private static DateTime ExpectedTimeStamp(LogFormat format)
        => format == LogFormat.Serilog
            ? TimeStamp.LocalDateTime
            : TimeStamp.DateTime;

    private static AtcLogEntry? Parse(
        LogFormat format,
        string line)
        => format switch
        {
            LogFormat.Serilog => new SerilogFileExtractor().ParseRootLine("orders", "orders", 1, line),
            LogFormat.NLog => new NLogFileExtractor().ParseRootLine("orders", "orders", 1, line),
            _ => new Log4NetFileExtractor().ParseRootLine("orders", "orders", 1, line),
        };
}