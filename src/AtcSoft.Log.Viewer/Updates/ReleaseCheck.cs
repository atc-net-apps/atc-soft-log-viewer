namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// The result of the update check.
/// </summary>
/// <param name="Status">What the check found.</param>
/// <param name="Release">The latest release, when <paramref name="Status"/> is <see cref="ReleaseCheckStatus.Found"/>.</param>
public sealed record ReleaseCheck(
    ReleaseCheckStatus Status,
    GitHubRelease? Release);