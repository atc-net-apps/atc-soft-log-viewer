namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// Where the app looks for a newer version: the latest GitHub release of this repository.
/// </summary>
public static class UpdateCheck
{
    /// <summary>
    /// The environment variable that turns off the check at start-up, e.g. for the UI tests.
    /// "1" or "true" turns it off.
    /// </summary>
    public const string SkipVariable = "ATCSOFT_LOGVIEWER_SKIP_UPDATE_CHECK";

    /// <summary>
    /// The environment variable that points the check at another release, for the UI tests: a local JSON
    /// file in the format of the GitHub releases API. The links in it must still be on github.com.
    /// </summary>
    public const string ReleaseUrlVariable = "ATCSOFT_LOGVIEWER_RELEASE_URL";

    /// <summary>
    /// The installer the release workflow attaches to every release. It is named after the installer project.
    /// </summary>
    public const string InstallerAssetName = "AtcSoft.Log.Viewer.Installer.msi";

    /// <summary>
    /// Gets the GitHub API address of the latest release.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "The app's own repository.")]
    public static Uri LatestReleaseApiUrl { get; } = new("https://api.github.com/repos/atc-net-apps/atc-soft-log-viewer/releases/latest");

    /// <summary>
    /// Gets where the check reads the latest release.
    /// </summary>
    /// <param name="overrideValue">The value of <see cref="ReleaseUrlVariable"/>, or <see langword="null"/> when it isn't set.</param>
    /// <returns>The file or address it names, or <see cref="LatestReleaseApiUrl"/>.</returns>
    public static Uri GetLatestReleaseUrl(string? overrideValue)
        => Uri.TryCreate(overrideValue, UriKind.Absolute, out var url) &&
           (url.IsFile || url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url
            : LatestReleaseApiUrl;

    /// <summary>
    /// Tells whether the value of <see cref="SkipVariable"/> turns the start-up check off.
    /// </summary>
    /// <param name="value">The variable's value, or <see langword="null"/> when it isn't set.</param>
    /// <returns><see langword="true"/> for "1" or "true".</returns>
    public static bool IsSkipped(string? value)
        => value is not null &&
           (value.Trim() == "1" || (bool.TryParse(value, out var skip) && skip));
}