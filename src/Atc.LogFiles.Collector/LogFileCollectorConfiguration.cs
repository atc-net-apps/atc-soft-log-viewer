namespace Atc.LogFiles.Collector;

public class LogFileCollectorConfiguration
{
    /// <summary>
    /// Gets or sets how many days back files are read, by when they were last written. 0, which older
    /// profiles may have saved, reads the last year.
    /// </summary>
    public ushort MaxDaysBack { get; set; } = 10;

    public bool MonitorFiles { get; set; } = true;

    public IList<string> FileNameTerms { get; set; } = [];

    public override string ToString()
        => $"{nameof(MaxDaysBack)}: {MaxDaysBack}, {nameof(MonitorFiles)}: {MonitorFiles}, {nameof(FileNameTerms)}.Count: {FileNameTerms.Count}";
}