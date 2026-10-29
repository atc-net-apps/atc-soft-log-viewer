namespace AtcSoft.Log.Viewer;

/// <summary>
/// Where the app keeps its per-user data: profiles, recent files and custom application settings.
/// </summary>
public sealed class AppDataPaths
{
    /// <summary>
    /// The environment variable that moves the data folder, for example for the end-to-end tests.
    /// </summary>
    public const string DataPathVariable = "ATCSOFT_LOGVIEWER_DATA_PATH";

    public AppDataPaths(DirectoryInfo root)
        : this(root, isOverridden: false)
    {
    }

    private AppDataPaths(
        DirectoryInfo root,
        bool isOverridden)
    {
        ArgumentNullException.ThrowIfNull(root);

        Root = root;
        IsOverridden = isOverridden;
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

    /// <summary>
    /// Gets a value indicating whether the folder was set with <see cref="DataPathVariable"/>.
    /// The data of earlier versions is then not copied into it.
    /// </summary>
    public bool IsOverridden { get; }

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
    /// Gets the folder set with <see cref="DataPathVariable"/>, or else the per-user folder.
    /// </summary>
    /// <returns>The paths.</returns>
    public static AppDataPaths FromEnvironment()
        => Resolve(Environment.GetEnvironmentVariable(DataPathVariable));

    /// <summary>
    /// Gets the folder <paramref name="overridePath"/> when it is set, or else the per-user folder.
    /// </summary>
    /// <param name="overridePath">The folder to use instead of the per-user folder.</param>
    /// <returns>The paths.</returns>
    public static AppDataPaths Resolve(string? overridePath)
        => string.IsNullOrWhiteSpace(overridePath)
            ? ForCurrentUser()
            : new AppDataPaths(new DirectoryInfo(overridePath), isOverridden: true);

    /// <summary>
    /// Creates the root and profiles folders when they are missing.
    /// </summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root.FullName);
        Directory.CreateDirectory(ProfilesDirectory.FullName);
    }
}