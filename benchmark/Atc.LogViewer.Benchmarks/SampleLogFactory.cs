namespace Atc.LogViewer.Benchmarks;

/// <summary>
/// Creates Serilog-formatted log content where every 10th entry is an error with a stack trace.
/// </summary>
internal static class SampleLogFactory
{
    private static readonly string[] Levels = ["VRB", "DBG", "INF", "INF", "INF", "WRN", "INF", "INF", "DBG", "ERR"];

    public static FileInfo CreateSerilogFile(int entryCount)
    {
        var directory = Directory.CreateTempSubdirectory("atc-logviewer-benchmark");
        var file = new FileInfo(Path.Combine(directory.FullName, "benchmark-20240216.log"));

        using var writer = new StreamWriter(file.FullName, append: false, Encoding.UTF8);
        var timeStamp = new DateTimeOffset(2024, 2, 16, 9, 0, 0, TimeSpan.FromHours(1));
        for (var i = 0; i < entryCount; i++)
        {
            var level = Levels[i % Levels.Length];
            writer.Write(timeStamp.AddMilliseconds(i).ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
            writer.Write(" [");
            writer.Write(level);
            writer.Write("] Processing order ");
            writer.Write(i.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(" for customer Contoso with a typical message length");

            if (level == "ERR")
            {
                writer.WriteLine("System.InvalidOperationException: Order could not be processed");
                writer.WriteLine("   at Contoso.Orders.OrderService.Process(Order order) in OrderService.cs:line 42");
                writer.WriteLine("   at Contoso.Orders.OrderWorker.ExecuteAsync(CancellationToken token) in OrderWorker.cs:line 17");
            }
        }

        return file;
    }

    public static AtcLogEntry[] CreateEntries(int entryCount)
    {
        var timeStamp = new DateTime(2024, 2, 16, 9, 0, 0, DateTimeKind.Unspecified);
        var entries = new AtcLogEntry[entryCount];
        for (var i = 0; i < entryCount; i++)
        {
            var logLevel = (LogLevel)(i % 6);
            var message = $"Processing order {i} for customer Contoso";
            entries[i] = new AtcLogEntry(
                "benchmark-20240216",
                i % 2 == 0 ? "Api" : "Worker",
                i + 1,
                timeStamp.AddMilliseconds(i),
                logLevel,
                message,
                message);
        }

        return entries;
    }
}