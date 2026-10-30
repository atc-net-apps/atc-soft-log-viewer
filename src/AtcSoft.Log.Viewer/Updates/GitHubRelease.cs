namespace AtcSoft.Log.Viewer.Updates;

/// <summary>
/// A release of the app on GitHub, as the releases API describes it.
/// </summary>
/// <param name="Version">The version, from the tag (<c>v1.2.0</c> is 1.2.0).</param>
/// <param name="ReleaseUrl">The release page.</param>
/// <param name="InstallerUrl">The download of this app's installer, or <see langword="null"/> when the release doesn't have it.</param>
public sealed record GitHubRelease(
    Version Version,
    Uri ReleaseUrl,
    Uri? InstallerUrl)
{
    /// <summary>
    /// Gets where the user gets the release: the installer, or the release page when it isn't there.
    /// </summary>
    public Uri DownloadUrl => InstallerUrl ?? ReleaseUrl;

    /// <summary>
    /// Reads a release from the GitHub releases API.
    /// </summary>
    /// <param name="json">The response of <c>releases/latest</c>.</param>
    /// <param name="installerAssetName">The file name of this app's installer. Only that asset is offered,
    /// never another file in the release.</param>
    /// <returns>The release, or <see langword="null"/> when <paramref name="json"/> isn't a release with a version tag.</returns>
    public static GitHubRelease? Parse(
        string json,
        string installerAssetName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("tag_name", out var tagName) ||
                !Version.TryParse(tagName.GetString()?.TrimStart('v'), out var version) ||
                !root.TryGetProperty("html_url", out var htmlUrl) ||
                !Uri.TryCreate(htmlUrl.GetString(), UriKind.Absolute, out var releaseUrl))
            {
                return null;
            }

            return new GitHubRelease(version, releaseUrl, FindAsset(root, installerAssetName));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tells whether this release is newer than the running version. Only major, minor and build count:
    /// the exe reports 1.2.0.0 for the release tagged v1.2.0.
    /// </summary>
    /// <param name="current">The running version.</param>
    /// <returns><see langword="true"/> when this release is newer.</returns>
    public bool IsNewerThan(Version current)
    {
        ArgumentNullException.ThrowIfNull(current);

        return ThreeParts(Version) > ThreeParts(current);
    }

    private static Version ThreeParts(Version version)
        => new(version.Major, version.Minor, System.Math.Max(version.Build, 0));

    private static Uri? FindAsset(
        JsonElement release,
        string assetName)
    {
        if (!release.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var name) &&
                string.Equals(name.GetString(), assetName, StringComparison.OrdinalIgnoreCase) &&
                asset.TryGetProperty("browser_download_url", out var downloadUrl) &&
                Uri.TryCreate(downloadUrl.GetString(), UriKind.Absolute, out var url))
            {
                return url;
            }
        }

        return null;
    }
}