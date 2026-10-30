namespace Atc.LogFiles.Analyzer.Tests;

public sealed class LogAnalyzerTests : IDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
    private static readonly DateTime BaseTime = new(2024, 2, 16, 9, 15, 0, DateTimeKind.Unspecified);

    private readonly ILog4NetFileCollector log4NetFileCollector = Substitute.For<ILog4NetFileCollector>();
    private readonly INLogFileCollector nlogFileCollector = Substitute.For<INLogFileCollector>();
    private readonly ISerilogFileCollector serilogFileCollector = Substitute.For<ISerilogFileCollector>();
    private readonly LogAnalyzer sut;

    public LogAnalyzerTests()
        => sut = new LogAnalyzer(
            log4NetFileCollector,
            nlogFileCollector,
            serilogFileCollector);

    [Fact]
    public void Constructor_ThrowsOnNullCollector()
    {
        // Act
        var act = () => new LogAnalyzer(
            null!,
            nlogFileCollector,
            serilogFileCollector);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetLogStatistics_CountsEntriesPerLogLevel()
    {
        // Arrange
        RaiseDefaultEntries();

        // Act
        var actual = sut.GetLogStatistics();

        // Assert
        actual.Count.Should().Be(7);
        actual.TraceCount.Should().Be(1);
        actual.DebugCount.Should().Be(1);
        actual.InformationCount.Should().Be(2);
        actual.WarningCount.Should().Be(1);
        actual.ErrorCount.Should().Be(1);
        actual.CriticalCount.Should().Be(1);
    }

    [Fact]
    public void GetFilteredLogEntries_WithDefaultFilter_ReturnsAllEntriesOrderedByTimeStamp()
    {
        // Arrange
        RaiseDefaultEntries();

        // Act
        var actual = sut.GetFilteredLogEntries();

        // Assert
        actual.Should().HaveCount(7);
        actual.Should().BeInAscendingOrder(x => x.TimeStamp);
    }

    [Fact]
    public void GetFilteredLogEntries_ExcludesDisabledLogLevels()
    {
        // Arrange
        RaiseDefaultEntries();
        sut.SetFilter(CreateFilter() with
        {
            LogLevelTrace = false,
            LogLevelDebug = false,
            LogLevelInfo = false,
        });

        // Act
        var actual = sut.GetFilteredLogEntries();

        // Assert
        actual
            .Select(x => x.LogLevel)
            .Should()
            .Equal(LogLevel.Warning, LogLevel.Error, LogLevel.Critical);
    }

    [Fact]
    public void GetFilteredLogEntries_MatchesIncludeTextIgnoringCase()
    {
        // Arrange
        RaiseDefaultEntries();
        sut.SetFilter(CreateFilter() with { IncludeText = "DATABASE" });

        // Act
        var actual = sut.GetFilteredLogEntries();

        // Assert
        actual
            .Should()
            .ContainSingle()
            .Which.MessageFull.Should().Be("Database connection failed");
    }

    [Fact]
    public void GetFilteredLogEntries_RespectsDateTimeRange()
    {
        // Arrange
        RaiseDefaultEntries();
        sut.SetFilter(CreateFilter() with
        {
            DateTimeFrom = BaseTime.AddSeconds(2),
            DateTimeTo = BaseTime.AddSeconds(4),
        });

        // Act
        var actual = sut.GetFilteredLogEntries();

        // Assert
        actual
            .Select(x => x.TimeStamp)
            .Should()
            .Equal(BaseTime.AddSeconds(2), BaseTime.AddSeconds(3), BaseTime.AddSeconds(4));
    }

    [Fact]
    public void GetFilteredLogEntries_MatchesSourceSystem()
    {
        // Arrange
        RaiseDefaultEntries();
        sut.SetFilter(CreateFilter() with { SourceSystem = "Worker" });

        // Act
        var actual = sut.GetFilteredLogEntries();

        // Assert
        actual
            .Should()
            .ContainSingle()
            .Which.SourceSystem.Should().Be("Worker");
    }

    [Fact]
    public void GetSourceSystems_ReturnsBlankFirstItemAndDistinctSystems()
    {
        // Arrange
        RaiseDefaultEntries();

        // Act
        var actual = sut.GetSourceSystems();

        // Assert
        actual.Keys.First().Should().BeEmpty();
        actual.Keys.Should().BeEquivalentTo(string.Empty, "Api", "Worker");
    }

    [Fact]
    public void ApplyFilter_EntriesThatArriveOutOfOrderAfterAFilter_AreInOrderInTheNextFilter()
    {
        // Arrange - files read in parallel deliver their entries interleaved
        RaiseDefaultEntries();
        sut.ApplyFilter(CreateFilter());
        RaiseEntry(CreateEntry(10, LogLevel.Information, "late file, later"));
        RaiseEntry(CreateEntry(-5, LogLevel.Information, "late file, earlier"));

        // Act
        var actual = sut.ApplyFilter(CreateFilter());

        // Assert
        actual.Entries.Should().HaveCount(9);
        actual.Entries.Should().BeInAscendingOrder(x => x.TimeStamp);
        actual.Entries[0].MessageFull.Should().Be("late file, earlier");
    }

    [Fact]
    public void SourceSystemsVersion_ChangesOnlyWhenANewSourceSystemAppears()
    {
        // Arrange
        RaiseEntry(CreateEntry(0, LogLevel.Information, "first"));
        var afterFirst = sut.SourceSystemsVersion;

        // Act
        RaiseEntry(CreateEntry(1, LogLevel.Information, "same system"));
        var afterSameSystem = sut.SourceSystemsVersion;
        RaiseEntry(CreateEntry(2, LogLevel.Information, "new system", sourceSystem: "Worker"));
        var afterNewSystem = sut.SourceSystemsVersion;

        // Assert
        afterSameSystem.Should().Be(afterFirst);
        afterNewSystem.Should().NotBe(afterSameSystem);
    }

    [Fact]
    public void SourceSystemsVersion_ChangesWhenTheEntriesAreCleared()
    {
        // Arrange
        RaiseDefaultEntries();
        var before = sut.SourceSystemsVersion;

        // Act
        sut.ClearLogEntries();

        // Assert
        sut.SourceSystemsVersion.Should().NotBe(before);
    }

    [Fact]
    public void ClearLogEntries_RemovesAllEntries()
    {
        // Arrange
        RaiseDefaultEntries();

        // Act
        sut.ClearLogEntries();

        // Assert
        sut.GetLogStatistics().Count.Should().Be(0);
        sut.GetFilteredLogEntries().Should().BeEmpty();
    }

    [Fact]
    public void StopMonitoringAndClearLogEntries_StopsAllCollectors()
    {
        // Arrange
        RaiseDefaultEntries();

        // Act
        sut.StopMonitoringAndClearLogEntries();

        // Assert
        log4NetFileCollector.Received(1).StopMonitoringAllFiles();
        nlogFileCollector.Received(1).StopMonitoringAllFiles();
        serilogFileCollector.Received(1).StopMonitoringAllFiles();
        sut.GetLogStatistics().Count.Should().Be(0);
    }

    [Fact]
    public async Task CollectedEntries_IsRaisedForEntriesMatchingTheFilter()
    {
        // Arrange
        var raised = new ConcurrentQueue<AtcLogEntry>();
        sut.CollectedEntries += (_, e) => e.Batch.Entries.ToList().ForEach(raised.Enqueue);
        sut.SetFilter(CreateFilter() with { LogLevelTrace = false });

        // Act
        RaiseDefaultEntries();

        // Assert
        (await WaitUntil(() => raised.Count >= 6)).Should().BeTrue();
        raised.Should().HaveCount(6);
        raised.Should().NotContain(x => x.LogLevel == LogLevel.Trace);
    }

    [Fact]
    public async Task CollectedEntries_DeliversLiveEntriesInBatches()
    {
        // Arrange
        var notifications = 0;
        var delivered = 0;
        sut.CollectedEntries += (_, e) =>
        {
            Interlocked.Increment(ref notifications);
            Interlocked.Add(ref delivered, e.Batch.Entries.Count);
        };

        // Act
        for (var i = 0; i < 500; i++)
        {
            RaiseEntry(CreateEntry(i, LogLevel.Information, $"Live entry {i}"));
        }

        // Assert
        (await WaitUntil(() => Volatile.Read(ref delivered) >= 500)).Should().BeTrue();
        notifications.Should().BeLessThan(50);
    }

    [Fact]
    public async Task CollectedEntries_DeliversAllEntriesRaisedConcurrently()
    {
        // Arrange
        const int threadCount = 8;
        const int entriesPerThread = 5_000;
        var delivered = 0;
        sut.CollectedEntries += (_, e) => Interlocked.Add(ref delivered, e.Batch.Entries.Count);

        // Act
        await Task.WhenAll(
            Enumerable.Range(0, threadCount).Select(thread => Task.Run(
                () =>
                {
                    for (var i = 0; i < entriesPerThread; i++)
                    {
                        RaiseEntry(CreateEntry(i, LogLevel.Information, $"Entry {thread}-{i}"));
                    }
                },
                CancellationToken)));

        // Assert
        (await WaitUntil(() => Volatile.Read(ref delivered) >= threadCount * entriesPerThread)).Should().BeTrue();
        await Task.Delay(300, CancellationToken);
        delivered.Should().Be(threadCount * entriesPerThread);
        sut.GetLogStatistics().Count.Should().Be(threadCount * entriesPerThread);
    }

    [Fact]
    public void ApplyFilter_ReturnsMatchingEntriesWithANewFilterVersion()
    {
        // Arrange
        RaiseDefaultEntries();
        var before = sut.ApplyFilter(CreateFilter());

        // Act
        var actual = sut.ApplyFilter(CreateFilter() with { LogLevelInfo = false });

        // Assert
        actual.FilterVersion.Should().BeGreaterThan(before.FilterVersion);
        actual.Entries.Should().HaveCount(5);
        actual.Entries.Should().BeInAscendingOrder(x => x.TimeStamp);
    }

    [Fact]
    public async Task CollectedEntries_IsRaisedWithTheAnalyzerAsSender()
    {
        // Arrange
        var senders = new ConcurrentQueue<object?>();
        sut.CollectedEntries += (sender, _) => senders.Enqueue(sender);

        // Act
        RaiseDefaultEntries();
        await Task.Delay(400, CancellationToken);

        // Assert
        senders.Should().NotBeEmpty();
        senders.Should().AllSatisfy(x => x.Should().BeSameAs(sut));
    }

    [Fact]
    public async Task ApplyFilter_DoesNotDeliverEntriesThatAreInItsResultAgain()
    {
        // Arrange
        var batches = new ConcurrentQueue<LogEntryBatch>();
        sut.CollectedEntries += (_, e) => batches.Enqueue(e.Batch);
        RaiseDefaultEntries();

        // Act
        var actual = sut.ApplyFilter(CreateFilter());
        await Task.Delay(400, CancellationToken);

        // Assert
        actual.Entries.Should().HaveCount(7);
        batches
            .Where(x => x.FilterVersion == actual.FilterVersion)
            .SelectMany(x => x.Entries)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task CollectedEntries_AfterApplyFilter_CarryItsFilterVersion()
    {
        // Arrange
        var batches = new ConcurrentQueue<LogEntryBatch>();
        sut.CollectedEntries += (_, e) => batches.Enqueue(e.Batch);
        var applied = sut.ApplyFilter(CreateFilter() with { LogLevelTrace = false });

        // Act
        RaiseEntry(CreateEntry(10, LogLevel.Trace, "Filtered out"));
        RaiseEntry(CreateEntry(11, LogLevel.Information, "Live entry"));

        // Assert
        (await WaitUntil(() => !batches.IsEmpty)).Should().BeTrue();
        var batch = batches.Single();
        batch.FilterVersion.Should().Be(applied.FilterVersion);
        batch.Entries.Select(x => x.MessageFull).Should().Equal("Live entry");
    }

    [Theory]
    [InlineData(true, false, false, LogFileCollectorType.Serilog)]
    [InlineData(false, true, false, LogFileCollectorType.NLog)]
    [InlineData(false, false, true, LogFileCollectorType.Log4Net)]
    [InlineData(false, false, false, LogFileCollectorType.None)]
    public void DetermineCollectorType_UsesFirstCollectorThatCanParseTheFile(
        bool serilogCanParse,
        bool nlogCanParse,
        bool log4NetCanParse,
        LogFileCollectorType expected)
    {
        // Arrange
        var file = new FileInfo("test.log");
        serilogFileCollector.CanParseFileFormat(file).Returns(serilogCanParse);
        nlogFileCollector.CanParseFileFormat(file).Returns(nlogCanParse);
        log4NetFileCollector.CanParseFileFormat(file).Returns(log4NetCanParse);

        // Act
        var actual = sut.DetermineCollectorType(file);

        // Assert
        actual.Should().Be(expected);
    }

    public void Dispose()
        => sut.Dispose();

    private static LogFilter CreateFilter()
        => new(
            LogLevelTrace: true,
            LogLevelDebug: true,
            LogLevelInfo: true,
            LogLevelWarning: true,
            LogLevelError: true,
            LogLevelCritical: true,
            IncludeText: string.Empty,
            DateTimeFrom: null,
            DateTimeTo: null,
            SourceSystem: string.Empty);

    private static AtcLogEntry CreateEntry(
        int secondsOffset,
        LogLevel logLevel,
        string message,
        string sourceSystem = "Api")
        => new(
            SourceIdentifier: "test.log",
            SourceSystem: sourceSystem,
            LineNumber: secondsOffset + 1,
            TimeStamp: BaseTime.AddSeconds(secondsOffset),
            LogLevel: logLevel,
            MessageShort: message,
            MessageFull: message);

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

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

    private void RaiseEntry(AtcLogEntry entry)
        => serilogFileCollector.CollectedEntry += Raise.EventWith(serilogFileCollector, new LogEntryEventArgs(entry));

    private void RaiseDefaultEntries()
    {
        // Raised out of order on purpose, to verify that the analyzer sorts by time stamp.
        AtcLogEntry[] entries =
        [
            CreateEntry(3, LogLevel.Warning, "Module C is deprecated"),
            CreateEntry(0, LogLevel.Information, "Application Start"),
            CreateEntry(1, LogLevel.Trace, "Entering module loader"),
            CreateEntry(2, LogLevel.Debug, "Loading modules"),
            CreateEntry(4, LogLevel.Error, "Database connection failed"),
            CreateEntry(5, LogLevel.Critical, "Out of disk space"),
            CreateEntry(6, LogLevel.Information, "Job done", sourceSystem: "Worker"),
        ];

        foreach (var entry in entries)
        {
            RaiseEntry(entry);
        }
    }
}