namespace Atc.LogFiles.Simulator.Tests;

public sealed class LogSimulatorTests : IDisposable
{
    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("atc-log-simulator-tests");
    private readonly LogSimulator sut;

    public LogSimulatorTests()
        => sut = new LogSimulator(
            Options.Create(new SimulatorOptions { Folder = folder.FullName }),
            TimeProvider.System);

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(LogFormat.Serilog)]
    [InlineData(LogFormat.NLog)]
    [InlineData(LogFormat.Log4Net)]
    public async Task Burst_WritesEntriesTheCollectorReadsBack(LogFormat format)
    {
        // Arrange
        sut.UpdateSettings(new SimulatorSettings(Format: format, FileCount: 1, ErrorPercent: 30, StackTracePercent: 100));

        // Act
        sut.Burst(500);

        // Assert
        var entries = await ReadEntries(format, "orders.log");
        entries.Should().HaveCount(500);
        entries.Should().Contain(x => x.LogLevel == LogLevel.Error && x.MessageFull.Contains("   at Contoso.", StringComparison.Ordinal));
    }

    [Fact]
    public void Burst_SpreadsEntriesOverTheFiles()
    {
        // Arrange
        sut.UpdateSettings(new SimulatorSettings(FileCount: 3));

        // Act
        sut.Burst(30);

        // Assert
        FileNames().Should().BeEquivalentTo("orders.log", "payments.log", "shipping.log");
    }

    [Fact]
    public void RollNow_Rename_ShiftsTheOlderFiles()
    {
        // Arrange
        sut.UpdateSettings(new SimulatorSettings(FileCount: 1, RollMode: RollMode.Rename));
        sut.Burst(10);
        sut.RollNow();
        sut.Burst(10);

        // Act
        sut.RollNow();
        sut.Burst(10);

        // Assert
        FileNames().Should().BeEquivalentTo("orders.log", "orders.log.1", "orders.log.2");
    }

    [Fact]
    public void RollNow_NewFile_WritesToAFileWithATimeStampInItsName()
    {
        // Arrange
        sut.UpdateSettings(new SimulatorSettings(FileCount: 1, RollMode: RollMode.NewFile));
        sut.Burst(10);

        // Act
        sut.RollNow();
        sut.Burst(10);

        // Assert
        var names = FileNames();
        names.Should().HaveCount(2).And.Contain("orders.log");
        names.Should().ContainSingle(x => x.StartsWith("orders-", StringComparison.Ordinal) && x.EndsWith(".log", StringComparison.Ordinal));
    }

    [Fact]
    public void RollNow_CopyTruncate_EmptiesTheFileAndKeepsACopy()
    {
        // Arrange
        sut.UpdateSettings(new SimulatorSettings(FileCount: 1, RollMode: RollMode.CopyTruncate));
        sut.Burst(10);
        var size = new FileInfo(Path.Combine(folder.FullName, "orders.log")).Length;

        // Act
        sut.RollNow();

        // Assert
        new FileInfo(Path.Combine(folder.FullName, "orders.log")).Length.Should().Be(0);
        folder.GetFiles().Where(x => x.Name.StartsWith("orders.log.", StringComparison.Ordinal)).Should().ContainSingle().Which.Length.Should().Be(size);
    }

    [Fact]
    public void RollSize_RollsTheFileWhenItIsFull()
    {
        // Arrange
        sut.UpdateSettings(new SimulatorSettings(FileCount: 1, RollMode: RollMode.Rename, RollSizeKb: 1));

        // Act
        sut.Burst(100);

        // Assert
        FileNames().Should().Contain("orders.log.1");
    }

    [Fact]
    public async Task UpdateSettings_NewFormat_StartsNewFilesAndKeepsTheOldOnesInTheOldFormat()
    {
        // Arrange
        sut.UpdateSettings(new SimulatorSettings(Format: LogFormat.Serilog, FileCount: 1));
        sut.Burst(10);

        // Act
        sut.UpdateSettings(new SimulatorSettings(Format: LogFormat.Log4Net, FileCount: 1));
        sut.Burst(20);

        // Assert
        var newFile = FileNames().Should().ContainSingle(x => x.StartsWith("orders-", StringComparison.Ordinal)).Which;
        (await ReadEntries(LogFormat.Serilog, "orders.log")).Should().HaveCount(10, "the old file keeps only the old format");
        (await ReadEntries(LogFormat.Log4Net, newFile)).Should().HaveCount(20, "the new format goes to a new file");
    }

    [Fact]
    public void DeleteFile_OnlyAcceptsAFileNameInTheFolder()
    {
        // Arrange
        var outside = new FileInfo(Path.Combine(folder.Parent!.FullName, $"{folder.Name}-outside.log"));
        File.WriteAllText(outside.FullName, "keep me");

        try
        {
            // Act
            sut.DeleteFile($"..{Path.DirectorySeparatorChar}{outside.Name}");

            // Assert
            outside.Exists.Should().BeTrue();
            sut.GetState().Events[0].IsError.Should().BeTrue();
        }
        finally
        {
            outside.Delete();
        }
    }

    public void Dispose()
    {
        sut.Dispose();

        try
        {
            folder.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp directory
        }
    }

    private string[] FileNames()
        => folder.GetFiles().Select(x => x.Name).ToArray();

    private async Task<List<AtcLogEntry>> ReadEntries(
        LogFormat format,
        string fileName)
    {
        IFileCollector collector = format switch
        {
            LogFormat.Serilog => new SerilogFileCollector(new SerilogFileExtractor()),
            LogFormat.NLog => new NLogFileCollector(new NLogFileExtractor()),
            _ => new Log4NetFileCollector(new Log4NetFileExtractor()),
        };

        var entries = new List<AtcLogEntry>();
        collector.CollectedEntry += (_, e) => entries.Add(e.Entry);
        await collector.CollectFile(
            new FileInfo(Path.Combine(folder.FullName, fileName)),
            new LogFileCollectorConfiguration(),
            useMonitoring: false,
            CancellationToken);

        return entries;
    }
}