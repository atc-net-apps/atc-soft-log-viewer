namespace AtcSoft.Log.Viewer.RecentFiles;

/// <summary>
/// A profile file the user opened, and when.
/// </summary>
/// <param name="TimeStamp">When it was opened.</param>
/// <param name="FilePath">The full path of the profile file.</param>
public sealed record RecentFile(
    DateTime TimeStamp,
    string FilePath);