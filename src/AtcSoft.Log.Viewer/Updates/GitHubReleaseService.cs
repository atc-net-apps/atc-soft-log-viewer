namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// Reads the latest release of the app from GitHub.
/// </summary>
public sealed class GitHubReleaseService : IGitHubReleaseService, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly ReleaseCheck Unavailable = new(ReleaseCheckStatus.Unavailable, Release: null);
    private readonly HttpClient httpClient;
    private readonly Uri releaseUrl;

    public GitHubReleaseService()
        : this(UpdateCheck.GetLatestReleaseUrl(Environment.GetEnvironmentVariable(UpdateCheck.ReleaseUrlVariable)))
    {
    }

    /// <param name="releaseUrl">Where to read the latest release: the GitHub API, or a local file.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The HttpClient owns the handler and disposes it with the service.")]
    public GitHubReleaseService(Uri releaseUrl)
        : this(releaseUrl, new HttpClientHandler())
    {
    }

    /// <param name="releaseUrl">Where to read the latest release: the GitHub API, or a local file.</param>
    /// <param name="handler">Sends the requests; the service disposes it.</param>
    public GitHubReleaseService(
        Uri releaseUrl,
        HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(releaseUrl);
        ArgumentNullException.ThrowIfNull(handler);

        this.releaseUrl = releaseUrl;
        httpClient = new HttpClient(handler, disposeHandler: true) { Timeout = RequestTimeout };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AtcSoft-LogViewer");
    }

    /// <inheritdoc />
    public async Task<ReleaseCheck> CheckAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            string json;
            if (releaseUrl.IsFile)
            {
                json = await File.ReadAllTextAsync(releaseUrl.LocalPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var response = await httpClient.GetAsync(releaseUrl, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // GitHub's answer for releases/latest until the first release is published
                    return new ReleaseCheck(ReleaseCheckStatus.NoRelease, Release: null);
                }

                if (!response.IsSuccessStatusCode)
                {
                    // Rate limited (403) or a GitHub problem
                    return Unavailable;
                }

                json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            var release = GitHubRelease.Parse(json, UpdateCheck.InstallerAssetName);
            return release is null
                ? Unavailable
                : new ReleaseCheck(ReleaseCheckStatus.Found, release);
        }
        catch (IOException)
        {
            // A release file that isn't there
            return Unavailable;
        }
        catch (HttpRequestException)
        {
            // Offline
            return Unavailable;
        }
        catch (TaskCanceledException)
        {
            // Timed out
            return Unavailable;
        }
    }

    public void Dispose()
        => httpClient.Dispose();
}