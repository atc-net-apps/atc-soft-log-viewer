namespace Atc.LogFiles.Analyzer.Tests;

/// <summary>
/// Simulates a logging system that writes to a watched folder: it appends, adds new files,
/// rolls files by renaming them, and deletes them. Uses real files, real collectors and real file notifications.
/// </summary>
[Collection(nameof(FolderMonitoringCollection))]
public sealed class FolderMonitoringTests : IDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("atc-folder-monitoring-tests");
    private readonly LogAnalyzer sut;
    private readonly ConcurrentQueue<AtcLogEntry> received = new();
    private readonly ConcurrentQueue<int> batchSizes = new();

    public FolderMonitoringTests()
    {
        sut = new LogAnalyzer(
            new Log4NetFileCollector(new Log4NetFileExtractor()),
            new NLogFileCollector(new NLogFileExtractor()),
            new SerilogFileCollector(new SerilogFileExtractor()));

        sut.CollectedEntries += (_, e) =>
        {
            batchSizes.Enqueue(e.Batch.Entries.Count);
            foreach (var entry in e.Batch.Entries)
            {
                received.Enqueue(entry);
            }
        };
    }

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AppendedLines_AreDelivered()
    {
        // Arrange
        var file = await WriteLines("app.log", "started");
        await StartWatching();

        // Act
        await AppendLines(file, "appended 1", "appended 2");

        // Assert
        (await WaitForMessages("appended 1", "appended 2")).Should().BeTrue();
    }

    [Fact]
    public async Task NewFileWithContent_IsPickedUpAndTailed()
    {
        // Arrange
        await WriteLines("app-1.log", "first file");
        await StartWatching();

        // Act
        var newFile = await WriteLines("app-2.log", "second file");
        (await WaitForMessages("second file")).Should().BeTrue();
        await AppendLines(newFile, "second file appended");

        // Assert
        (await WaitForMessages("second file appended")).Should().BeTrue();
    }

    [Fact]
    public async Task NewFileCreatedEmpty_IsPickedUpWhenTheLoggerWritesToIt()
    {
        // Arrange - loggers often create the file first and write the first line a moment later
        await WriteLines("app-1.log", "first file");
        await StartWatching();

        // Act
        var newFile = new FileInfo(Path.Combine(directory.FullName, "app-2.log"));
        await File.WriteAllTextAsync(newFile.FullName, string.Empty, CancellationToken);
        await Task.Delay(300, CancellationToken);
        await AppendLines(newFile, "written after create");

        // Assert
        (await WaitForMessages("written after create")).Should().BeTrue();
    }

    [Fact]
    public async Task RollByRename_TheNewFileIsReadFromTheStart_AndNothingIsDuplicated()
    {
        // Arrange
        var file = await WriteLines("app.log", "before roll");
        await StartWatching();

        // Act - the logger renames the current file to an archive name and starts a new one with the same name
        File.Move(file.FullName, Path.Combine(directory.FullName, "app.1.log"));
        await WriteLines("app.log", "after roll 1");
        await AppendLines(file, "after roll 2");

        // Assert
        (await WaitForMessages("after roll 1", "after roll 2")).Should().BeTrue();
        await Task.Delay(1_500, CancellationToken);
        received.Select(x => x.MessageFull).Should().OnlyHaveUniqueItems();
        received.Should().ContainSingle(x => x.MessageFull == "before roll");
    }

    [Fact]
    public async Task FolderWatcherError_NewFilesItMissed_AreFoundAndFollowed()
    {
        // Arrange - the folder watcher's buffer overflows in a burst of file events, so it misses a new file
        await WriteLines("app-1.log", "first file");
        await StartWatching();
        sut.FolderWatcher!.EnableRaisingEvents = false;
        var newFile = await WriteLines("app-2.log", "missed file");

        // Act
        sut.OnFolderWatcherError(sut.FolderWatcher, new ErrorEventArgs(new InternalBufferOverflowException()));
        (await WaitForMessages("missed file")).Should().BeTrue();
        await AppendLines(newFile, "missed file appended");

        // Assert
        (await WaitForMessages("missed file appended")).Should().BeTrue();
        await Task.Delay(500, CancellationToken);
        received.Select(x => x.MessageFull).Should().OnlyHaveUniqueItems();
        received.Should().ContainSingle(x => x.MessageFull == "first file");
    }

    [Fact]
    public async Task FileCreatedBeforeToday_IsFollowed()
    {
        // Arrange - a service that writes to the same file across midnight
        var file = await WriteLines("app.log", "written yesterday");
        File.SetCreationTime(file.FullName, DateTime.Now.AddDays(-1));
        await StartWatching();

        // Act
        await AppendLines(file, "written today");

        // Assert
        (await WaitForMessages("written yesterday", "written today")).Should().BeTrue();
    }

    [Fact]
    public async Task FileCreatedBeforeTheDaysBack_ButStillWrittenTo_IsReadAndFollowed()
    {
        // Arrange - the file is older than the days back, but its lines are recent
        var file = await WriteLines("app.log", "written recently");
        File.SetCreationTime(file.FullName, DateTime.Now.AddDays(-30));
        await StartWatching();

        // Act
        await AppendLines(file, "written now");

        // Assert
        (await WaitForMessages("written recently", "written now")).Should().BeTrue();
    }

    [Fact]
    public async Task FileNotWrittenWithinTheDaysBack_IsNotRead()
    {
        // Arrange
        var file = await WriteLines("app.log", "old");
        File.SetCreationTime(file.FullName, DateTime.Now.AddDays(-30));
        File.SetLastWriteTime(file.FullName, DateTime.Now.AddDays(-30));
        await WriteLines("app-2.log", "recent");

        // Act
        await StartWatching();

        // Assert
        received.Select(x => x.MessageFull).Should().Equal("recent");
    }

    [Fact]
    public async Task RollByRename_OfAFileCreatedBeforeToday_TheNewFileIsFollowed()
    {
        // Arrange - Windows may give a file created soon after a rename the old file's creation time (NTFS tunneling)
        var file = await WriteLines("app.log", "before roll");
        File.SetCreationTime(file.FullName, DateTime.Now.AddDays(-1));
        await StartWatching();

        // Act
        File.Move(file.FullName, Path.Combine(directory.FullName, "app.1.log"));
        await WriteLines("app.log", "after roll 1");
        await AppendLines(file, "after roll 2");

        // Assert
        (await WaitForMessages("after roll 1", "after roll 2")).Should().BeTrue();
    }

    [Fact]
    public async Task EntriesWithTheSameTimeStamp_KeepTheirLineOrder()
    {
        // Arrange - a burst where many entries get the same time stamp
        var timeStamp = DateTime.Now;
        var messages = Enumerable.Range(1, 50).Select(i => $"burst {i:00}").ToArray();
        await File.WriteAllLinesAsync(
            Path.Combine(directory.FullName, "app.log"),
            messages.Select(m => Line(m, timeStamp)),
            CancellationToken);

        // Act
        await StartWatching();
        var refiltered = sut.ApplyFilter(new LogFilter(
            LogLevelTrace: true,
            LogLevelDebug: true,
            LogLevelInfo: true,
            LogLevelWarning: true,
            LogLevelError: true,
            LogLevelCritical: true,
            IncludeText: string.Empty,
            DateTimeFrom: null,
            DateTimeTo: null,
            SourceSystem: string.Empty));

        // Assert
        received.Select(x => x.MessageFull).Should().Equal(messages);
        refiltered.Entries.Select(x => x.MessageFull).Should().Equal(messages);
        sut.GetFilteredLogEntries().Select(x => x.MessageFull).Should().Equal(messages);
    }

    [Fact]
    public async Task LinesWrittenJustBeforeARoll_AreNotLost()
    {
        // Arrange
        var file = await WriteLines("app.log", "started");
        await StartWatching();

        // Act - the last lines and the rename come so close together that no read happens in between
        await File.AppendAllLinesAsync(file.FullName, [Line("last before roll")], CancellationToken);
        File.Move(file.FullName, Path.Combine(directory.FullName, "app.1.log"));
        await WriteLines("app.log", "first after roll");

        // Assert
        (await WaitForMessages("last before roll", "first after roll")).Should().BeTrue();
        await Task.Delay(500, CancellationToken);
        received.Select(x => x.MessageFull).Should().Equal("started", "last before roll", "first after roll");
    }

    [Fact]
    public async Task RollByCopyAndTruncate_NothingIsLostOrDuplicated()
    {
        // Arrange
        var file = await WriteLines("app.log", "started");
        await StartWatching();
        await AppendLines(file, "last before roll");
        (await WaitForMessages("last before roll")).Should().BeTrue();

        // Act - the logger copies the file to an archive name, empties it and goes on writing
        File.Copy(file.FullName, Path.Combine(directory.FullName, "app.1.log"));
        await File.WriteAllTextAsync(file.FullName, string.Empty, CancellationToken);
        await AppendLines(file, "first after roll");

        // Assert
        (await WaitForMessages("first after roll")).Should().BeTrue();
        await Task.Delay(1_500, CancellationToken);
        received.Select(x => x.MessageFull).Should().Equal("started", "last before roll", "first after roll");
    }

    [Fact]
    public async Task TwoRollsByCopyAndTruncate_NothingIsDuplicated()
    {
        // Arrange
        var file = await WriteLines("app.log", "started");
        await StartWatching();
        File.Copy(file.FullName, Path.Combine(directory.FullName, "app.1.log"));
        await File.WriteAllTextAsync(file.FullName, string.Empty, CancellationToken);
        await AppendLines(file, "after roll 1");
        (await WaitForMessages("after roll 1")).Should().BeTrue();
        await Task.Delay(500, CancellationToken);

        // Act - the second archive starts with the first line the tail read after the first roll
        File.Copy(file.FullName, Path.Combine(directory.FullName, "app.2.log"));
        await File.WriteAllTextAsync(file.FullName, string.Empty, CancellationToken);
        await AppendLines(file, "after roll 2");

        // Assert
        (await WaitForMessages("after roll 2")).Should().BeTrue();
        await Task.Delay(1_500, CancellationToken);
        received.Select(x => x.MessageFull).Should().Equal("started", "after roll 1", "after roll 2");
    }

    [Fact]
    public async Task DeletedAndRecreatedFile_IsReadFromTheStart()
    {
        // Arrange
        var file = await WriteLines("app.log", "before delete");
        await StartWatching();

        // Act
        file.Delete();
        await Task.Delay(300, CancellationToken);
        await WriteLines("app.log", "after recreate");

        // Assert
        (await WaitForMessages("after recreate")).Should().BeTrue();
    }

    [Fact]
    public async Task DeletingOneFile_DoesNotStopTheOtherFiles()
    {
        // Arrange
        var deleted = await WriteLines("app-1.log", "first file");
        var kept = await WriteLines("app-2.log", "second file");
        await StartWatching();

        // Act
        deleted.Delete();
        await AppendLines(kept, "still tailed");

        // Assert
        (await WaitForMessages("still tailed")).Should().BeTrue();
    }

    [Fact]
    public async Task Burst_IsDeliveredCompletely_InFewBatches()
    {
        // Arrange
        var file = await WriteLines("app.log", "started");
        await StartWatching();

        // Act - 5,000 entries over about a second, in many small writes like a busy service
        for (var i = 0; i < 50; i++)
        {
            await File.AppendAllLinesAsync(
                file.FullName,
                Enumerable.Range(i * 100, 100).Select(n => Line($"burst {n}")),
                CancellationToken);
            await Task.Delay(20, CancellationToken);
        }

        // Assert - the front end gets a few batches, not one notification per entry
        (await WaitUntil(() => received.Count(x => x.MessageFull.StartsWith("burst ", StringComparison.Ordinal)) == 5_000)).Should().BeTrue();
        batchSizes.Count.Should().BeLessThan(60);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S2925:Do not use 'Thread.Sleep()' in a test", Justification = "Simulates a slow, synchronous event handler.")]
    public async Task SlowSubscriber_DoesNotLoseEntries()
    {
        // Arrange - a front end that needs 200 ms per batch
        var slowReceived = new ConcurrentQueue<AtcLogEntry>();
        sut.CollectedEntries += (_, e) =>
        {
            Thread.Sleep(200);
            foreach (var entry in e.Batch.Entries)
            {
                slowReceived.Enqueue(entry);
            }
        };

        var file = await WriteLines("app.log", "started");
        await StartWatching();

        // Act
        for (var i = 0; i < 20; i++)
        {
            await AppendLines(file, $"slow {i}");
            await Task.Delay(50, CancellationToken);
        }

        // Assert
        (await WaitUntil(() => slowReceived.Count(x => x.MessageFull.StartsWith("slow ", StringComparison.Ordinal)) == 20)).Should().BeTrue();
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S2925:Do not use 'Thread.Sleep()' in a test", Justification = "Simulates a slow, synchronous event handler.")]
    public async Task Batches_AreNeverDeliveredAtTheSameTime()
    {
        // Arrange - a front end may assume that batches come one at a time
        var active = 0;
        var overlaps = 0;
        sut.CollectedEntries += (_, _) =>
        {
            if (Interlocked.Increment(ref active) > 1)
            {
                Interlocked.Increment(ref overlaps);
            }

            Thread.Sleep(30);
            Interlocked.Decrement(ref active);
        };

        var file = await WriteLines("app-1.log", "started");
        await StartWatching();

        // Act - live lines and new files at the same time
        for (var i = 0; i < 20; i++)
        {
            await AppendLines(file, $"live {i}");
            await WriteLines($"app-new-{i}.log", $"new file {i}");
            await Task.Delay(20, CancellationToken);
        }

        // Assert
        (await WaitForMessages("live 19", "new file 19")).Should().BeTrue();
        overlaps.Should().Be(0);
    }

    [Fact]
    public async Task LinesInAnotherFormat_AreCountedAsNotRead()
    {
        // Arrange - the logger's layout was changed while it kept writing to the same file
        var file = await WriteLines("app.log", "started");
        await File.AppendAllLinesAsync(file.FullName, [Log4NetLine("first in log4net"), Log4NetLine("second in log4net")], CancellationToken);
        var changes = 0;
        sut.NotReadChanged += (_, _) => Interlocked.Increment(ref changes);

        // Act
        await StartWatching();

        // Assert
        (await WaitUntil(() => Volatile.Read(ref changes) > 0)).Should().BeTrue();
        var lines = sut.GetNotRead().Lines.Should().ContainSingle().Which;
        lines.FilePath.Should().Be(file.FullName);
        lines.Count.Should().Be(2);
        lines.FirstLineNumber.Should().Be(2);
        lines.FirstLine.Should().EndWith("first in log4net");
        lines.Format.Should().Be(LogFileCollectorType.Log4Net);
        sut.GetLogStatistics().LinesNotReadCount.Should().Be(2);
    }

    [Fact]
    public async Task LinesNoFormatCanRead_AreNotCountedAsNotRead()
    {
        // Arrange - a message line that starts with a digit, and a line the logger is still writing
        var file = await WriteLines("app.log", "started");
        await File.AppendAllTextAsync(
            file.FullName,
            "42 items failed validation" + Environment.NewLine + Line("next entry") + Environment.NewLine + "2026-10-30 00:0",
            CancellationToken);

        // Act
        await StartWatching();
        await Task.Delay(500, CancellationToken);

        // Assert
        (await WaitForMessages("next entry")).Should().BeTrue();
        sut.GetLogStatistics().LinesNotReadCount.Should().Be(0);
        sut.GetNotRead().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task AppendedLinesInAnotherFormat_AreCountedAsNotRead()
    {
        // Arrange
        var file = await WriteLines("app.log", "started");
        await StartWatching();

        // Act
        await File.AppendAllLinesAsync(file.FullName, [Log4NetLine("appended in log4net")], CancellationToken);

        // Assert
        (await WaitUntil(() => sut.GetLogStatistics().LinesNotReadCount == 1)).Should().BeTrue();
    }

    [Fact]
    public async Task NewFileInAnotherFormat_IsReportedAsNotRead()
    {
        // Arrange
        await WriteLines("app.log", "started");
        await StartWatching();
        var changes = 0;
        sut.NotReadChanged += (_, _) => Interlocked.Increment(ref changes);

        // Act
        var newFile = Path.Combine(directory.FullName, "app-log4net.log");
        await File.WriteAllLinesAsync(newFile, [Log4NetLine("a file in another format")], CancellationToken);

        // Assert
        (await WaitUntil(() => Volatile.Read(ref changes) > 0)).Should().BeTrue();
        var notRead = sut.GetNotRead().Files.Should().ContainSingle().Which;
        notRead.FilePath.Should().Be(newFile);
        notRead.Format.Should().Be(LogFileCollectorType.Log4Net);
        sut.GetLogStatistics().FilesNotReadCount.Should().Be(1);
    }

    [Fact]
    public async Task ClearLogEntries_ForgetsWhatWasNotRead()
    {
        // Arrange
        var file = await WriteLines("app.log", "started");
        await File.AppendAllLinesAsync(file.FullName, [Log4NetLine("in log4net")], CancellationToken);
        await StartWatching();
        (await WaitUntil(() => sut.GetLogStatistics().LinesNotReadCount == 1)).Should().BeTrue();

        // Act
        sut.ClearLogEntries();

        // Assert
        sut.GetNotRead().Lines.Should().BeEmpty();
        sut.GetLogStatistics().LinesNotReadCount.Should().Be(0);
    }

    [Fact]
    public async Task HalfWrittenLastLine_WhenTheFolderIsOpened_IsReadOnceComplete()
    {
        // Arrange - the logger is in the middle of a line when the folder is opened
        var file = await WriteLines("app.log", "started");
        var line = Line("Order 42 done");
        await File.AppendAllTextAsync(file.FullName, line[..^3], CancellationToken);
        await StartWatching();

        // Act
        await File.AppendAllTextAsync(file.FullName, line[^3..] + Environment.NewLine, CancellationToken);

        // Assert
        (await WaitForMessages("Order 42 done")).Should().BeTrue();
        await Task.Delay(500, CancellationToken);
        received.Select(x => x.MessageFull).Should().Equal("started", "Order 42 done");
    }

    [Fact]
    public async Task FileWithOnlyAHalfWrittenLine_WhenTheFolderIsOpened_IsReadOnceComplete()
    {
        // Arrange - a second file whose first line is still being written
        await WriteLines("app.log", "started");
        var file = new FileInfo(Path.Combine(directory.FullName, "worker.log"));
        var line = Line("worker started");
        await File.WriteAllTextAsync(file.FullName, line[..^4], CancellationToken);
        await StartWatching();

        // Act
        await File.AppendAllTextAsync(file.FullName, line[^4..] + Environment.NewLine, CancellationToken);
        await AppendLines(file, "worker next");

        // Assert
        (await WaitForMessages("worker started", "worker next")).Should().BeTrue();
    }

    [Fact]
    public async Task LastLineWithoutLineBreak_InAFollowedFile_IsRead()
    {
        // Arrange - a file whose last line has no line break, and nothing more is written
        var file = await WriteLines("app.log", "started");
        await File.AppendAllTextAsync(file.FullName, Line("no line break at the end"), CancellationToken);

        // Act
        await StartWatching();

        // Assert
        (await WaitForMessages("started", "no line break at the end")).Should().BeTrue();
    }

    private static string Log4NetLine(string message)
        => $"{DateTime.Now:yyyy-MM-dd HH:mm:ss,fff} [8] INFO  Worker - {message}"; // log4net's default layout

    public void Dispose()
    {
        sut.StopMonitoringAndClearLogEntries();
        sut.Dispose();

        try
        {
            directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp directory
        }
    }

    private static string Line(string message)
        => Line(message, DateTime.Now);

    private static string Line(
        string message,
        DateTime timeStamp)
        => $"{timeStamp:yyyy-MM-dd HH:mm:ss.fff zzz} [INF] {message}"; // Serilog's default file layout

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

    private Task<bool> WaitForMessages(params string[] messages)
        => WaitUntil(() => messages.All(m => received.Any(x => x.MessageFull == m)));

    private Task StartWatching()
        => sut.CollectFolder(
            LogFileCollectorType.Serilog,
            directory,
            new LogFileCollectorConfiguration(),
            useMonitoring: true,
            CancellationToken);

    private async Task<FileInfo> WriteLines(
        string fileName,
        params string[] messages)
    {
        var file = new FileInfo(Path.Combine(directory.FullName, fileName));
        await File.WriteAllLinesAsync(file.FullName, messages.Select(Line), CancellationToken);
        return file;
    }

    private static Task AppendLines(
        FileInfo file,
        params string[] messages)
        => File.AppendAllLinesAsync(file.FullName, messages.Select(Line), CancellationToken);
}