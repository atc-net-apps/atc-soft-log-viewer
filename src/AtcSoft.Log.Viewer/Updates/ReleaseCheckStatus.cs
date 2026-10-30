namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// What the update check found.
/// </summary>
public enum ReleaseCheckStatus
{
    /// <summary>The latest release was read.</summary>
    Found,

    /// <summary>The repository has no release yet (GitHub answers 404).</summary>
    NoRelease,

    /// <summary>No answer: offline, rate limited, timed out, or an answer that isn't a release.</summary>
    Unavailable,
}