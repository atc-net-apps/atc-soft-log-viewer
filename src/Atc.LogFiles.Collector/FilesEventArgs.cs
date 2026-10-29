namespace Atc.LogFiles.Collector;

/// <summary>
/// The files that have been collected.
/// </summary>
public sealed class FilesEventArgs : EventArgs
{
    public FilesEventArgs(IReadOnlyList<FileInfo> files)
        => Files = files;

    public IReadOnlyList<FileInfo> Files { get; }
}