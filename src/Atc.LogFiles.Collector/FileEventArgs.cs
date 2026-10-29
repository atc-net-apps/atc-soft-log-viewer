namespace Atc.LogFiles.Collector;

/// <summary>
/// A file that has been collected.
/// </summary>
public sealed class FileEventArgs : EventArgs
{
    public FileEventArgs(FileInfo file)
        => File = file;

    public FileInfo File { get; }
}