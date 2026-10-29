namespace Atc.LogFiles.SampleGenerator.Tests;

public sealed class SampleLogGeneratorTests : IDisposable
{
    private const int LineCount = 5_000;

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("atc-sample-generator-tests");

    public static TheoryData<LogFormat> Formats
        => new() { LogFormat.NLog, LogFormat.Log4Net, LogFormat.Serilog };

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task Generate_WritesTheRequestedNumberOfLines(LogFormat format)
    {
        // Arrange
        var options = CreateOptions(format);

        // Act
        var result = SampleLogGenerator.Generate(options);

        // Assert
        result.LineCount.Should().Be(LineCount);
        (await File.ReadAllLinesAsync(options.OutputPath, CancellationToken)).Should().HaveCount(LineCount);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task Generate_WritesEntriesTheMatchingCollectorCanRead(
        LogFormat format)
    {
        // Arrange
        var options = CreateOptions(format);
        var collector = CreateCollector(format);
        var entries = new List<AtcLogEntry>();
        collector.CollectedEntry += entries.Add;

        // Act
        var result = SampleLogGenerator.Generate(options);
        var file = new FileInfo(options.OutputPath);
        await collector.CollectFile(file, new LogFileCollectorConfiguration(), useMonitoring: false, CancellationToken);

        // Assert
        collector.CanParseFileFormat(file).Should().BeTrue();
        entries.Should().HaveCount((int)result.EntryCount);
        entries.Should().Contain(x => x.LogLevel == Microsoft.Extensions.Logging.LogLevel.Error && x.MessageFull.Contains("   at ", StringComparison.Ordinal));
        entries.Should().Contain(x => x.MessageFull.Contains("\"orderId\"", StringComparison.Ordinal));
        entries.Select(x => x.LogLevel).Distinct().Should().HaveCount(6);
    }

    [Fact]
    public async Task Generate_WithTheSameSeed_WritesTheSameContent()
    {
        // Arrange
        var first = CreateOptions(LogFormat.Serilog) with { OutputPath = Path.Combine(directory.FullName, "first.log") };
        var second = first with { OutputPath = Path.Combine(directory.FullName, "second.log") };

        // Act
        SampleLogGenerator.Generate(first);
        SampleLogGenerator.Generate(second);

        // Assert
        (await File.ReadAllTextAsync(second.OutputPath, CancellationToken))
            .Should()
            .Be(await File.ReadAllTextAsync(first.OutputPath, CancellationToken));
    }

    [Fact]
    public void Main_WithArguments_GeneratesTheFile()
    {
        // Arrange
        var outputPath = Path.Combine(directory.FullName, "cli.log");

        // Act
        var exitCode = Program.Main(["--format", "log4net", "--lines", "1_000", "--output", outputPath]);

        // Assert
        exitCode.Should().Be(0);
        File.ReadAllLines(outputPath).Should().HaveCount(1_000);
    }

    [Theory]
    [InlineData("--format", "unknown")]
    [InlineData("--lines", "-5")]
    public void Main_WithInvalidArguments_ReturnsAnError(
        string option,
        string value)
    {
        // Arrange
        var outputPath = Path.Combine(directory.FullName, "invalid.log");

        // Act
        var exitCode = Program.Main([option, value, "--output", outputPath]);

        // Assert
        exitCode.Should().NotBe(0);
        File.Exists(outputPath).Should().BeFalse();
    }

    public void Dispose()
        => directory.Delete(recursive: true);

    private static LogFileCollectorBase CreateCollector(LogFormat format)
        => format switch
        {
            LogFormat.NLog => new NLogFileCollector(new NLogFileExtractor()),
            LogFormat.Log4Net => new Log4NetFileCollector(new Log4NetFileExtractor()),
            _ => new SerilogFileCollector(new SerilogFileExtractor()),
        };

    private GeneratorOptions CreateOptions(LogFormat format)
        => new(
            format,
            LineCount,
            Path.Combine(directory.FullName, $"sample-{format}.log".ToLowerInvariant()));
}