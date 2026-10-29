namespace Atc.LogFiles.Collector.Tests;

/// <summary>
/// An entry is a root line (with time stamp and level) followed by the lines up to the next root line,
/// for example a stack trace or a JSON payload.
/// </summary>
[Collection(nameof(TestCollection))]
public sealed class MultiLineEntryTests : IDisposable
{
    private static readonly string NewLine = Environment.NewLine;
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("atc-multiline-tests");
    private readonly List<LogFileCollectorBase> collectors = [];

    public static TheoryData<string> TestDataFiles
        => new()
        {
            "multiline-serilog.log",
            "multiline-no-trailing-newline-serilog.log",
            "multiline-nlog.log",
            "multiline-no-trailing-newline-nlog.log",
            "multiline-log4net.log",
            "multiline-no-trailing-newline-log4net.log",
        };

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(TestDataFiles))]
    public async Task ReadAndParseLines_GroupsFollowingLinesIntoTheEntry(
        string fileName)
    {
        // Arrange
        var file = new FileInfo(Path.Combine(AppContext.BaseDirectory, "TestData", fileName));
        var entries = new List<AtcLogEntry>();

        // Act
        var (isSuccessFul, lastLineNumber) = await ReadAndParseLines(file, entries.Add);

        // Assert
        isSuccessFul.Should().BeTrue();
        lastLineNumber.Should().Be(18);
        entries.Select(x => x.LineNumber).Should().Equal(1, 2, 7, 11, 13, 16);
        entries.Select(x => x.MessageFull).Should().Equal(
            "Application started successfully",
            string.Join(
                NewLine,
                "Order processing failed for order 42",
                "System.InvalidOperationException: boom",
                "   at Contoso.Orders.OrderService.Process()",
                string.Empty,
                "   at Contoso.Orders.OrderWorker.Execute()"),
            string.Join(
                NewLine,
                "Received payload from upstream system",
                "{",
                "  \"orderId\": 42",
                "}"),
            string.Join(
                NewLine,
                "Short sub line follows this entry",
                "abc"),
            "Processing a very long message " + new string('x', 200),
            string.Join(
                NewLine,
                "Last entry with a stack trace attached",
                "System.Exception: end",
                "   at Contoso.Orders.Final()"));
        entries[4].MessageShort.Should().Be(string.Concat("Processing a very long message ", new string('x', 129), "..."));
    }

    [Theory]
    [InlineData("serilog")]
    [InlineData("nlog")]
    [InlineData("log4net")]
    public async Task Monitoring_RaisesAppendedEntryWithItsFollowingLines(
        string format)
    {
        // Arrange
        var (collector, rootLine) = CreateCollector(format);
        var file = new FileInfo(Path.Combine(directory.FullName, $"live-{format}.log"));
        await File.WriteAllLinesAsync(file.FullName, [rootLine("INFO", "Application started successfully")], CancellationToken);

        var entries = new ConcurrentQueue<AtcLogEntry>();
        ((IFileCollector)collector).CollectedEntry += entries.Enqueue;
        await ((IFileCollector)collector).CollectFile(file, new LogFileCollectorConfiguration(), useMonitoring: true, CancellationToken);
        entries.Clear();

        // Act
        await File.AppendAllLinesAsync(
            file.FullName,
            [
                rootLine("ERROR", "Order processing failed for order 42"),
                "System.InvalidOperationException: boom",
                "   at Contoso.Orders.OrderService.Process()",
            ],
            CancellationToken);

        // Assert
        (await WaitUntil(() => !entries.IsEmpty)).Should().BeTrue();
        await Task.Delay(500, CancellationToken);
        entries.Should().ContainSingle();
        var entry = entries.Single();
        entry.LineNumber.Should().Be(2);
        entry.LogLevel.Should().Be(LogLevel.Error);
        entry.MessageFull.Should().Be(
            string.Join(
                NewLine,
                "Order processing failed for order 42",
                "System.InvalidOperationException: boom",
                "   at Contoso.Orders.OrderService.Process()"));
    }

    [Fact]
    public async Task Monitoring_ShortensLongMessages()
    {
        // Arrange
        var (collector, rootLine) = CreateCollector("serilog");
        var file = new FileInfo(Path.Combine(directory.FullName, "live-long.log"));
        await File.WriteAllLinesAsync(file.FullName, [rootLine("INFO", "Application started successfully")], CancellationToken);

        var entries = new ConcurrentQueue<AtcLogEntry>();
        ((IFileCollector)collector).CollectedEntry += entries.Enqueue;
        await ((IFileCollector)collector).CollectFile(file, new LogFileCollectorConfiguration(), useMonitoring: true, CancellationToken);
        entries.Clear();

        // Act
        await File.AppendAllLinesAsync(file.FullName, [rootLine("INFO", new string('x', 300))], CancellationToken);

        // Assert
        (await WaitUntil(() => !entries.IsEmpty)).Should().BeTrue();
        entries.Single().MessageShort.Should().Be(string.Concat(new string('x', 160), "..."));
        entries.Single().MessageFull.Should().HaveLength(300);
    }

    public void Dispose()
    {
        foreach (var collector in collectors)
        {
            collector.StopMonitoringAllFiles();
        }

        try
        {
            directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp directory
        }
    }

    private static Task<(bool IsSuccessFul, long LastLineNumber)> ReadAndParseLines(
        FileInfo file,
        Action<AtcLogEntry> onEntry)
    {
        if (file.Name.Contains("serilog", StringComparison.Ordinal))
        {
            var collector = new SerilogFileCollector(new SerilogFileExtractor());
            collector.CollectedEntry += onEntry;
            return collector.ReadAndParseLines(file, CancellationToken);
        }

        if (file.Name.Contains("nlog", StringComparison.Ordinal))
        {
            var collector = new NLogFileCollector(new NLogFileExtractor());
            collector.CollectedEntry += onEntry;
            return collector.ReadAndParseLines(file, CancellationToken);
        }

        var log4NetCollector = new Log4NetFileCollector(new Log4NetFileExtractor());
        log4NetCollector.CollectedEntry += onEntry;
        return log4NetCollector.ReadAndParseLines(file, CancellationToken);
    }

    private static async Task<bool> WaitUntil(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + WaitTimeout;
        while (DateTime.UtcNow < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25, CancellationToken);
        }

        return condition();
    }

    private (LogFileCollectorBase Collector, Func<string, string, string> RootLine) CreateCollector(
        string format)
    {
        var timeStamp = DateTime.Now;
        (LogFileCollectorBase Collector, Func<string, string, string> RootLine) result = format switch
        {
            "serilog" => (
                new SerilogFileCollector(new SerilogFileExtractor()),
                (level, message) => $"{timeStamp:yyyy-MM-dd HH:mm:ss.fff} [{ToSerilogLevel(level)}] {message}"),
            "nlog" => (
                new NLogFileCollector(new NLogFileExtractor()),
                (level, message) => $"{timeStamp:yyyy-MM-dd HH:mm:ss.ffff} {level} {message}"),
            _ => (
                new Log4NetFileCollector(new Log4NetFileExtractor()),
                (level, message) => $"{timeStamp:yyyy-MM-dd HH:mm:ss,fff} [8] {level,-5} Worker - {message}"),
        };

        collectors.Add(result.Collector);
        return result;
    }

    private static string ToSerilogLevel(string level)
        => level switch
        {
            "ERROR" => "ERR",
            "WARN" => "WRN",
            _ => "INF",
        };
}