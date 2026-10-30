namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// Reads the latest release of the app from GitHub.
/// </summary>
public interface IGitHubReleaseService
{
    /// <summary>
    /// Gets the latest release.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The release, or <see langword="null"/> when it can't be read: offline, rate limited or no release yet.</returns>
    Task<GitHubRelease?> GetLatestAsync(CancellationToken cancellationToken);
}