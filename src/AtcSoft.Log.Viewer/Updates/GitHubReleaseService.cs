namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// Reads the latest release of the app from GitHub.
/// </summary>
public sealed class GitHubReleaseService : IGitHubReleaseService, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly HttpClient httpClient;
    private readonly Uri releaseUrl;

    public GitHubReleaseService()
        : this(UpdateCheck.GetLatestReleaseUrl(Environment.GetEnvironmentVariable(UpdateCheck.ReleaseUrlVariable)))
    {
    }

    /// <param name="releaseUrl">Where to read the latest release: the GitHub API, or a local file.</param>
    public GitHubReleaseService(Uri releaseUrl)
    {
        ArgumentNullException.ThrowIfNull(releaseUrl);

        this.releaseUrl = releaseUrl;
        httpClient = new HttpClient { Timeout = RequestTimeout };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AtcSoft-LogViewer");
    }

    /// <inheritdoc />
    public async Task<GitHubRelease?> GetLatestAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var json = releaseUrl.IsFile
                ? await File.ReadAllTextAsync(releaseUrl.LocalPath, cancellationToken).ConfigureAwait(false)
                : await httpClient.GetStringAsync(releaseUrl, cancellationToken).ConfigureAwait(false);

            return GitHubRelease.Parse(json, UpdateCheck.InstallerAssetName);
        }
        catch (IOException)
        {
            // A release file that isn't there
            return null;
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