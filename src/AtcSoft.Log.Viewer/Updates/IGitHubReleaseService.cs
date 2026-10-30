namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// Reads the latest release of the app from GitHub.
/// </summary>
public interface IGitHubReleaseService
{
    /// <summary>
    /// Checks for the latest release.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The release, or why there is none: no release yet, or no answer.</returns>
    Task<ReleaseCheck> CheckAsync(CancellationToken cancellationToken);
}