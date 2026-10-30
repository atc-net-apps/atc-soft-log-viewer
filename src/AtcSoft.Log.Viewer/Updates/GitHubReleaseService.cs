namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// Reads the latest release of the app from GitHub.
/// </summary>
public sealed class GitHubReleaseService : IGitHubReleaseService, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly HttpClient httpClient;

    public GitHubReleaseService()
    {
        httpClient = new HttpClient { Timeout = RequestTimeout };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AtcSoft-LogViewer");
    }

    /// <inheritdoc />
    public async Task<GitHubRelease?> GetLatestAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await httpClient
                .GetStringAsync(UpdateCheck.LatestReleaseApiUrl, cancellationToken)
                .ConfigureAwait(false);

            return GitHubRelease.Parse(json, UpdateCheck.InstallerAssetName);
        }
        catch (HttpRequestException)
        {
            // Offline, rate limited, or no release yet (404): the check just has no answer
            return null;
        }
        catch (TaskCanceledException)
        {
            // Timed out
            return null;
        }
    }

    public void Dispose()
        => httpClient.Dispose();
}