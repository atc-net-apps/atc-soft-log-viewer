namespace Atc.LogFiles.Analyzer;

public class LogStatistics
{
    public long Count { get; set; }

    public long CriticalCount { get; set; }

    public long ErrorCount { get; set; }

    public long WarningCount { get; set; }

    public long InformationCount { get; set; }

    public long DebugCount { get; set; }

    public long TraceCount { get; set; }

    /// <summary>
    /// Gets or sets the number of lines that were not read because they are in another format.
    /// </summary>
    public long LinesNotReadCount { get; set; }

    /// <summary>
    /// Gets or sets the number of files in the watched folder that are in another format.
    /// </summary>
    public long FilesNotReadCount { get; set; }

    public override string ToString()
        => $"{nameof(Count)}: {Count}, {nameof(CriticalCount)}: {CriticalCount}, {nameof(ErrorCount)}: {ErrorCount}, {nameof(WarningCount)}: {WarningCount}, {nameof(InformationCount)}: {InformationCount}, {nameof(DebugCount)}: {DebugCount}, {nameof(TraceCount)}: {TraceCount}, {nameof(LinesNotReadCount)}: {LinesNotReadCount}, {nameof(FilesNotReadCount)}: {FilesNotReadCount}";
}