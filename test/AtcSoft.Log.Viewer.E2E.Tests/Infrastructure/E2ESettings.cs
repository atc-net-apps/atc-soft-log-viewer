namespace AtcSoft.Log.Viewer.E2E.Infrastructure;

/// <summary>
/// Whether the UI tests run at all, where the viewer is, and the run's folders.
/// </summary>
internal static class E2ESettings
{
    /// <summary>
    /// The UI tests open real windows, so they run only when this is "1".
    /// </summary>
    public const string EnableVariable = "ATCSOFT_E2E";

    private const string AppTargetFramework = "net10.0-windows10.0.19041.0";
    private const string ViewerProject = "AtcSoft.Log.Viewer.Wpf.App";

    public static bool IsEnabled
        => string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal);

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// Gets the build configuration of this test run (Debug or Release), so the viewer started
    /// is the one built with it.
    /// </summary>
    public static string Configuration { get; } = FindConfiguration();

    /// <summary>
    /// Gets the folder of this run: the viewer's data, the log folders and the UI tree dumps go
    /// below it, and it stays after the run for a look at what happened.
    /// </summary>
    public static string RunFolder { get; } = Path.Combine(
        Path.GetTempPath(),
        "AtcSoft.Log.Viewer.E2E",
        DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

    public static string ViewerExe
    {
        get
        {
            var path = Path.Combine(RepositoryRoot, "src", ViewerProject, "bin", Configuration, AppTargetFramework, ViewerProject + ".exe");
            return File.Exists(path)
                ? path
                : throw new FileNotFoundException($"Build {ViewerProject} ({Configuration}) first; the UI tests start its executable.", path);
        }
    }

    public static void SkipUnlessEnabled()
        => Assert.SkipUnless(
            IsEnabled,
            $"The UI tests open real windows; set {EnableVariable}=1 to run them.");

    /// <summary>
    /// Creates a new folder below the run folder; a number is added when the name is taken.
    /// </summary>
    /// <param name="name">The folder name.</param>
    /// <returns>The full path.</returns>
    public static string NewFolder(string name)
    {
        var folder = Path.Combine(RunFolder, name);
        var number = 2;
        while (Directory.Exists(folder))
        {
            folder = Path.Combine(RunFolder, $"{name}-{number}");
            number++;
        }

        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string FindRepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AtcSoft.Log.Viewer.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
               ?? throw new InvalidOperationException("The repository root (AtcSoft.Log.Viewer.slnx) was not found above " + AppContext.BaseDirectory);
    }

    private static string FindConfiguration()
    {
        // ...\bin\{Configuration}\{TargetFramework}\
        var targetFrameworkFolder = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        return targetFrameworkFolder.Parent?.Name ?? "Debug";
    }
}