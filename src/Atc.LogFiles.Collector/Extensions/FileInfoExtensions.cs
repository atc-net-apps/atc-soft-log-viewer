namespace Atc.LogFiles.Collector.Extensions;

public static class FileInfoExtensions
{
    /// <summary>
    /// How recently a file must have been written to for it to be followed.
    /// </summary>
    public static readonly TimeSpan FollowWindow = TimeSpan.FromDays(1);

    /// <summary>
    /// Tells whether a file may still be written to, so it is worth following: it was created today,
    /// or written within the last day. A service often writes to the same file across midnight.
    /// </summary>
    /// <param name="fileInfo">The file.</param>
    /// <returns><see langword="true"/> when the file should be followed.</returns>
    public static bool IsRecentlyWritten(this FileInfo fileInfo)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);

        var now = DateTime.Now;
        return fileInfo.CreationTime.Date == now.Date ||
               fileInfo.LastWriteTime >= now - FollowWindow;
    }
}