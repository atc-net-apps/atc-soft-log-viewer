namespace Atc.LogFiles.Collector.Tests.Tail;

[Collection(nameof(TestCollection))]
public sealed class TailFileTests : IDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("atc-tailfile-tests");
    private readonly List<TailFile> tailFiles = [];

    [Fact]
    public async Task AppendedLines_AreRaisedOnceAndInOrder()
    {
        // Arrange
        var file = CreateEmptyFile("app.log");
        var received = new ConcurrentQueue<TailLine>();
        StartTail(file, received.Enqueue);

        // Act
        for (var burst = 0; burst < 5; burst++)
        {
            await File.AppendAllLinesAsync(file.FullName, Enumerable.Range(burst * 20, 20).Select(i => $"line {i}"), CancellationToken);
            await Task.Delay(20, CancellationToken);
        }

        // Assert
        (await WaitUntil(() => received.Count >= 100)).Should().BeTrue();
        await Task.Delay(300, CancellationToken);
        received.Select(x => x.Line).Should().Equal(Enumerable.Range(0, 100).Select(i => $"line {i}"));
        received.Select(x => x.LineNumber).Should().Equal(Enumerable.Range(1, 100).Select(i => (long)i));
    }

    [Fact]
    public async Task TailingManyFiles_DoesNotStarveTheThreadPool()
    {
        // Arrange
        // More files than the thread pool starts threads for without delay (the test host may raise that minimum).
        ThreadPool.GetMinThreads(out var minWorkerThreads, out _);
        var fileCount = minWorkerThreads + 8;
        var received = 0;
        var files = new List<FileInfo>();
        for (var i = 0; i < fileCount; i++)
        {
            files.Add(CreateEmptyFile($"app{i}.log"));
        }

        foreach (var file in files)
        {
            StartTail(file, _ => Interlocked.Increment(ref received));
        }

        // Act
        foreach (var file in files)
        {
            await File.AppendAllTextAsync(file.FullName, "hello" + Environment.NewLine, CancellationToken);
        }

        // Assert
        (await WaitUntil(() => Volatile.Read(ref received) >= fileCount)).Should().BeTrue();
        await Task.Delay(500, CancellationToken);
        ThreadPool.ThreadCount.Should().BeLessThan(
            fileCount,
            "an idle tailed file must not keep a thread-pool thread busy");
    }

    [Fact]
    public async Task PartialLine_IsRaisedWhenCompleted()
    {
        // Arrange
        var file = CreateEmptyFile("app.log");
        var received = new ConcurrentQueue<TailLine>();
        StartTail(file, received.Enqueue);

        // Act
        await File.AppendAllTextAsync(file.FullName, "first half", CancellationToken);
        await Task.Delay(500, CancellationToken);
        var receivedBeforeNewLine = received.Count;
        await File.AppendAllTextAsync(file.FullName, " second half" + Environment.NewLine, CancellationToken);

        // Assert
        receivedBeforeNewLine.Should().Be(0);
        (await WaitUntil(() => !received.IsEmpty)).Should().BeTrue();
        received.Select(x => x.Line).Should().Equal("first half second half");
    }

    [Fact]
    public async Task TruncatedFile_IsReadFromTheStart()
    {
        // Arrange
        var file = CreateEmptyFile("app.log");
        var received = new ConcurrentQueue<TailLine>();
        StartTail(file, received.Enqueue);
        await File.AppendAllLinesAsync(file.FullName, ["old 1", "old 2", "old 3"], CancellationToken);
        (await WaitUntil(() => received.Count >= 3)).Should().BeTrue();
        received.Clear();

        // Act
        await File.WriteAllLinesAsync(file.FullName, ["new 1"], CancellationToken);

        // Assert
        (await WaitUntil(() => !received.IsEmpty)).Should().BeTrue();
        await Task.Delay(300, CancellationToken);
        received.Select(x => (x.Line, x.LineNumber)).Should().Equal(("new 1", 1L));
    }

    [Fact]
    public async Task Start_WithStartOffset_RaisesTheLinesAfterIt()
    {
        // Arrange - the collector read the first line, and the logger wrote the second before tailing started
        var file = CreateEmptyFile("app.log");
        await File.WriteAllTextAsync(file.FullName, "line 1\nline 2\n", CancellationToken);
        var received = new ConcurrentQueue<TailLine>();
        var tailFile = new TailFile(file, lastLineNumber: 1, startOffset: "line 1\n".Length);
        tailFile.LineAdded += (_, e) => received.Enqueue(e.Line);
        tailFiles.Add(tailFile);

        // Act
        tailFile.Start();

        // Assert
        (await WaitUntil(() => !received.IsEmpty)).Should().BeTrue();
        received.Select(x => (x.Line, x.LineNumber)).Should().Equal(("line 2", 2L));
    }

    [Fact]
    public async Task RenamedFile_ItsLastLinesAreReadBeforeTheNewFile()
    {
        // Arrange - pause, so the last lines and the rename happen before any read
        var file = CreateEmptyFile("app.log");
        var received = new ConcurrentQueue<TailLine>();
        var tailFile = StartTail(file, received.Enqueue);
        tailFile.Pause();
        await File.AppendAllLinesAsync(file.FullName, ["last before roll"], CancellationToken);
        File.Move(file.FullName, Path.Combine(directory.FullName, "app.1.log"));
        await File.WriteAllLinesAsync(file.FullName, ["first after roll"], CancellationToken);
        await Task.Delay(300, CancellationToken);

        // Act
        tailFile.Resume();

        // Assert
        (await WaitUntil(() => received.Count >= 2)).Should().BeTrue();
        await Task.Delay(300, CancellationToken);
        received.Select(x => (x.Line, x.LineNumber)).Should().Equal(("last before roll", 1L), ("first after roll", 1L));
    }

    [Fact]
    public async Task Stop_StopsRaisingLines()
    {
        // Arrange
        var file = CreateEmptyFile("app.log");
        var received = new ConcurrentQueue<TailLine>();
        var tailFile = StartTail(file, received.Enqueue);

        // Act
        tailFile.Stop();
        await File.AppendAllLinesAsync(file.FullName, ["after stop"], CancellationToken);
        await Task.Delay(1_500, CancellationToken);

        // Assert
        received.Should().BeEmpty();
    }

    public void Dispose()
    {
        foreach (var tailFile in tailFiles)
        {
            tailFile.Dispose();
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

    private FileInfo CreateEmptyFile(string name)
    {
        var file = new FileInfo(Path.Combine(directory.FullName, name));
        using (File.Create(file.FullName))
        {
            // Creates an empty file
        }

        return file;
    }

    private TailFile StartTail(
        FileInfo file,
        Action<TailLine> onLine)
    {
        var tailFile = new TailFile(file);
        tailFile.LineAdded += (_, e) => onLine(e.Line);
        tailFile.Start();
        tailFiles.Add(tailFile);
        return tailFile;
    }
}