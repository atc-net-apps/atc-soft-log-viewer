# Atc.LogFiles libraries

The log viewer reads log files with two libraries in this repo. They are plain C# (`net10.0`), have no UI types and don't reference the app, so another front end, or a later NuGet package, can use them as they are.

| Project | What |
|---|---|
| `Atc.LogFiles.Collector` | Parses log4net, NLog and Serilog files and folders into `AtcLogEntry` records, including multi-line entries such as stack traces and JSON payloads, and tails files for new entries |
| `Atc.LogFiles.Analyzer` | Keeps the collected entries, filters them (levels, text, time range, source system), counts them per level, matches highlight rules and builds timeline buckets |

They were `Atc.LogCollector` and `Atc.LogAnalyzer` in the retired [atc-net/atc-logviewer](https://github.com/atc-net/atc-logviewer) repo. They were never published to NuGet.

## Read a file

```csharp
var collector = new SerilogFileCollector(new SerilogFileExtractor());
collector.CollectedEntry += (_, e) => Console.WriteLine($"{e.Entry.TimeStamp:O} {e.Entry.LogLevel} {e.Entry.MessageShort}");

var file = new FileInfo("app-20240216.log");
if (collector.CanParseFileFormat(file))
{
    await collector.CollectFile(file, new LogFileCollectorConfiguration(), useMonitoring: false, CancellationToken.None);
}
```

Use `NLogFileCollector` or `Log4NetFileCollector` for the other formats. With `useMonitoring: true`, a file created today is tailed, and new entries are raised as they are written.

## Filter and count

```csharp
var analyzer = new LogAnalyzer(
    new Log4NetFileCollector(new Log4NetFileExtractor()),
    new NLogFileCollector(new NLogFileExtractor()),
    new SerilogFileCollector(new SerilogFileExtractor()));

var folder = new DirectoryInfo(@"C:\logs\orders");
var collectorType = analyzer.DetermineCollectorType(folder);

// Entries matching the current filter: once when the collect is done, then in batches while tailing
analyzer.CollectedEntries += (_, e) => Console.WriteLine($"{e.Batch.Entries.Count} entries");

await analyzer.CollectFolder(collectorType, folder, new LogFileCollectorConfiguration(), useMonitoring: true, CancellationToken.None);

var errors = analyzer.ApplyFilter(
    new LogFilter(
        LogLevelTrace: false,
        LogLevelDebug: false,
        LogLevelInfo: false,
        LogLevelWarning: false,
        LogLevelError: true,
        LogLevelCritical: true,
        IncludeText: "order",
        DateTimeFrom: null,
        DateTimeTo: null,
        SourceSystem: string.Empty));

var statistics = analyzer.GetLogStatistics();
```

## Highlight and timeline

```csharp
// The first matching rule wins. The index lets a front end keep its own colors per rule.
var matcher = new HighlightMatcher(
[
    new HighlightRule("timeout", IgnoreCase: true),
    new HighlightRule("NullReferenceException"),
]);

var ruleIndex = matcher.FindMatch(entry); // HighlightMatcher.NoMatch when no rule matches

// Counts per log level for the latest 24 hours, one bucket per hour
var timeline = LogTimeline.Create(analyzer.GetFilteredLogEntries(), DateTime.Now, TimeSpan.FromHours(1), bucketCount: 24);
var errorsPerHour = timeline.GetCounts(LogLevel.Error);
```
