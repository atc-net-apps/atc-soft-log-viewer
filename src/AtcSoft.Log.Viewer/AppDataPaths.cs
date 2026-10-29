namespace AtcSoft.Log.Viewer;

/// <summary>
/// Where the app keeps its per-user data: profiles, recent files and custom application settings.
/// </summary>
public sealed class AppDataPaths
{
    public AppDataPaths(DirectoryInfo root)
    {
        ArgumentNullException.ThrowIfNull(root);

        Root = root;
    }

    /// <summary>
    /// The folder of earlier versions: <c>%ProgramData%\ATC\atc-logviewer</c> (machine-wide).
    /// </summary>
    public static DirectoryInfo LegacyRoot
        => new(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ATC",
                "atc-logviewer"));

    public DirectoryInfo Root { get; }

    public DirectoryInfo ProfilesDirectory
        => new(Path.Combine(Root.FullName, "Profiles"));

    public FileInfo RecentOpenFilesFile
        => new(Path.Combine(Root.FullName, "RecentOpenFiles.json"));

    public FileInfo CustomAppSettingsFile
        => new(Path.Combine(Root.FullName, "appsettings.custom.json"));

    /// <summary>
    /// The per-user folder: <c>%LOCALAPPDATA%\AtcSoft\AtcSoft.Log.Viewer</c>.
    /// </summary>
    /// <returns>The paths.</returns>
    public static AppDataPaths ForCurrentUser()
        => new(
            new DirectoryInfo(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppIdentity.Vendor,
                    AppIdentity.AppId)));

    /// <summary>
    /// Creates the root and profiles folders when they are missing.
    /// </summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root.FullName);
        Directory.CreateDirectory(ProfilesDirectory.FullName);
    }
}