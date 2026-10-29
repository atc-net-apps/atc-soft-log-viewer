namespace Atc.LogViewer.SampleGenerator;

/// <summary>
/// Generates large sample log files for testing the log viewer.
/// </summary>
/// <example>
/// dotnet run --project tool/Atc.LogViewer.SampleGenerator -c Release -- --format nlog --lines 100_000_000 --output sample/sample-nlog-big.log.
/// </example>
public static class Program
{
    public static int Main(string[] args)
    {
        var formatOption = new Option<LogFormat>("--format", "-f")
        {
            Description = "The log file format: nlog, log4net or serilog.",
            DefaultValueFactory = _ => LogFormat.NLog,
        };

        var linesOption = new Option<long>("--lines", "-l")
        {
            Description = "The number of lines to write. Underscores are allowed, e.g. 100_000_000.",
            DefaultValueFactory = _ => 1_000_000,
            CustomParser = result =>
            {
                var value = result.Tokens.Single().Value.Replace("_", string.Empty, StringComparison.Ordinal);
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lines) &&
                    lines > 0)
                {
                    return lines;
                }

                result.AddError($"'{result.Tokens.Single().Value}' is not a positive number of lines.");
                return 0;
            },
        };

        var outputOption = new Option<FileInfo>("--output", "-o")
        {
            Description = "The file to write. Defaults to sample-<format>-<lines>.log in the current directory.",
        };

        var seedOption = new Option<int>("--seed")
        {
            Description = "The random seed; the same seed gives the same content.",
            DefaultValueFactory = _ => 42,
        };

        var rootCommand = new RootCommand("Generates a log file with simulated application logging for testing Atc.LogViewer.")
        {
            formatOption,
            linesOption,
            outputOption,
            seedOption,
        };

        rootCommand.SetAction(parseResult =>
        {
            var format = parseResult.GetValue(formatOption);
            var lines = parseResult.GetValue(linesOption);
            var output = parseResult.GetValue(outputOption)?.FullName
                         ?? Path.GetFullPath($"sample-{format.ToString().ToLowerInvariant()}-{lines}.log");

            Console.WriteLine($"Generating {lines:N0} {format} lines to {output}");
            var result = SampleLogGenerator.Generate(
                new GeneratorOptions(format, lines, output, parseResult.GetValue(seedOption)),
                linesWritten => Console.WriteLine($"  {linesWritten:N0} lines"));

            Console.WriteLine(
                $"Wrote {result.LineCount:N0} lines ({result.EntryCount:N0} entries, " +
                $"{result.ByteCount / (1024.0 * 1024 * 1024):N2} GB) in {result.Elapsed:mm\\:ss}");
            return 0;
        });

        return rootCommand
            .Parse(args)
            .Invoke();
    }
}