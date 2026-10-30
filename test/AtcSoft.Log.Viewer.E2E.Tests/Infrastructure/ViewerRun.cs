namespace AtcSoft.Log.Viewer.E2E.Infrastructure;

/// <summary>
/// ATC Log Viewer started with a data folder of its own below the run folder, so the user's
/// profiles and recent files are never read or changed. With a log folder, it gets a profile
/// for that folder as its last used profile, and opens it at start, the way a user opens it
/// again: no folder dialog is needed.
/// </summary>
internal sealed class ViewerRun : IDisposable
{
    public const string MainWindowTitle = "ATC Log.Viewer";

    /// <summary>
    /// The status bar count of the entries collected (all, before filtering).
    /// </summary>
    public const string CollectedCountId = "CollectedCount";

    /// <summary>
    /// The status bar count of the entries in the list.
    /// </summary>
    public const string ShownCountId = "ShownCount";

    /// <summary>
    /// The list of log entries.
    /// </summary>
    public const string LogEntriesId = "LogEntries";

    private readonly ChildProcess process;

    public ViewerRun(
        string name,
        string? logFolder = null,
        LogFileCollectorType collectorType = LogFileCollectorType.None,
        string? workingDirectory = null)
    {
        DataFolder = E2ESettings.NewFolder(name + "-data");
        if (logFolder is not null)
        {
            WriteLastUsedProfile(logFolder, collectorType);
        }

        process = ChildProcess.Start(
            E2ESettings.ViewerExe,
            [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AppDataPaths.DataPathVariable] = DataFolder,
                ["Application__OpenRecentFileOnStartup"] = "true",
            },
            Path.Combine(DataFolder, "viewer-output.log"),
            workingDirectory);

        try
        {
            App = new DesktopApp(process.Process, process.OutputPath, Path.Combine(DataFolder, "ui-trees"));
            Main = App.WaitForWindowTitled(MainWindowTitle, DesktopApp.StartTimeout);
        }
        catch
        {
            App?.Dispose();
            process.Dispose();
            throw;
        }
    }

    public string DataFolder { get; }

    public DesktopApp App { get; }

    public Window Main { get; }

    /// <summary>
    /// Reads a count in the status bar.
    /// </summary>
    /// <param name="automationId"><see cref="CollectedCountId"/> or <see cref="ShownCountId"/>.</param>
    /// <returns>The count.</returns>
    public long ReadCount(string automationId)
    {
        var text = App.FindById(Main, automationId).Properties.Name.ValueOrDefault ?? string.Empty;
        var digits = string.Concat(text.Where(char.IsDigit));
        return digits.Length == 0
            ? 0
            : long.Parse(digits, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Waits until a count in the status bar reaches the expected value.
    /// </summary>
    /// <param name="automationId"><see cref="CollectedCountId"/> or <see cref="ShownCountId"/>.</param>
    /// <param name="expected">The count to wait for.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>The count.</returns>
    public long WaitForCount(
        string automationId,
        long expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        long count;
        do
        {
            App.ShouldNotHaveCrashed();
            count = ReadCount(automationId);
            if (count >= expected)
            {
                return count;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        throw App.Fail($"The status bar count '{automationId}' is {count:N0}, not {expected:N0}, after {timeout.TotalSeconds:N0} s.");
    }

    /// <summary>
    /// Reads how far down the list is scrolled.
    /// </summary>
    /// <returns>0 at the top, 100 at the bottom; 100 when there is nothing to scroll.</returns>
    public double ReadListScrollPercent()
    {
        var scroll = App.FindById(Main, LogEntriesId).Patterns.Scroll.Pattern;
        return scroll.VerticallyScrollable.ValueOrDefault
            ? scroll.VerticalScrollPercent.ValueOrDefault
            : 100;
    }

    public void Dispose()
    {
        App.Dispose();
        process.Dispose();
    }

    private void WriteLastUsedProfile(
        string logFolder,
        LogFileCollectorType collectorType)
    {
        var paths = new AppDataPaths(new DirectoryInfo(DataFolder));
        paths.EnsureCreated();

        var profileFile = new FileInfo(Path.Combine(paths.ProfilesDirectory.FullName, "e2e.json"));
        ProfileFile
            .SaveAsync(
                profileFile,
                new ProfileOptions
                {
                    Name = "e2e",
                    LogFolder = logFolder,
                    CollectorType = collectorType,
                })
            .GetAwaiter()
            .GetResult();

        // The same layout as the app writes (RecentOpenFilesOption, property names as is).
        var recentOpenFiles = new
        {
            RecentOpenFiles = new[]
            {
                new { TimeStamp = DateTime.Now, FilePath = profileFile.FullName },
            },
        };
        File.WriteAllText(paths.RecentOpenFilesFile.FullName, System.Text.Json.JsonSerializer.Serialize(recentOpenFiles));
    }
}