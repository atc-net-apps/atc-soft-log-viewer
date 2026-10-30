namespace AtcSoft.Log.Viewer.Wpf.Dialogs;

/// <summary>
/// Shows the running version and the latest release, and opens the download of a newer one.
/// </summary>
public partial class CheckForUpdatesBoxDialog
{
    private readonly IGitHubReleaseService gitHubReleaseService;
    private GitHubRelease? latestRelease;

    public CheckForUpdatesBoxDialog(IGitHubReleaseService gitHubReleaseService)
    {
        ArgumentNullException.ThrowIfNull(gitHubReleaseService);

        this.gitHubReleaseService = gitHubReleaseService;

        InitializeComponent();

        CurrentVersionTextBlock.Text = WpfApp.CurrentVersion.ToString(3);

        Loaded += OnLoaded;
    }

    [SuppressMessage("AsyncUsage", "AsyncFixer03:Fire-and-forget async-void methods or delegates", Justification = "An event handler; the check catches its own failures.")]
    private async void OnLoaded(
        object? sender,
        RoutedEventArgs e)
    {
        // Bounded by the service's request timeout; a late answer to a closed dialog does no harm
        latestRelease = await gitHubReleaseService
            .GetLatestAsync(CancellationToken.None)
            .ConfigureAwait(true);

        if (latestRelease is null)
        {
            StatusTextBlock.Text = "Unable to check for updates. Try again later.";
            return;
        }

        LatestVersionTextBlock.Text = latestRelease.Version.ToString(3);
        if (latestRelease.IsNewerThan(WpfApp.CurrentVersion))
        {
            StatusTextBlock.Text = "A new version is available.";
            DownloadButton.IsEnabled = true;
        }
        else
        {
            StatusTextBlock.Text = "You have the latest version.";
        }
    }

    private void OnDownload(
        object sender,
        RoutedEventArgs e)
    {
        if (latestRelease is null)
        {
            return;
        }

        // The browser downloads the installer, or shows the release page when the release has none
        Process.Start(new ProcessStartInfo
        {
            FileName = latestRelease.DownloadUrl.AbsoluteUri,
            UseShellExecute = true,
        });
    }

    private void OnClose(
        object sender,
        RoutedEventArgs e)
        => Close();
}