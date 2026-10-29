namespace AtcSoft.Log.Viewer.E2E;

/// <summary>
/// The viewer follows a folder the log simulator writes to, in this process: the counts in the
/// status bar must match what was written while files are appended, rolled, deleted and created
/// empty, and the window must keep answering during a burst.
/// </summary>
/// <remarks>
/// The folder is opened through a profile with no log format set, the way that crashed before
/// (the format must come from the files).
/// </remarks>
[Collection(E2ETestGroup.Name)]
public sealed class LiveFolderTests
{
    private static readonly TimeSpan CountTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public void Follows_Appends_Rolls_Deletes_And_New_Files()
    {
        E2ESettings.SkipUnlessEnabled();

        var folder = E2ESettings.NewFolder("live-logs");
        using var simulator = CreateSimulator(folder);
        simulator.Burst(1_000);

        using var run = new ViewerRun("live", folder);
        var app = run.App;

        // Opened at start
        run.WaitForCount(ViewerRun.CollectedCountId, 1_000, CountTimeout);

        // Appended to the open files
        simulator.Burst(500);
        run.WaitForCount(ViewerRun.CollectedCountId, 1_500, CountTimeout);

        // Rolled by rename: orders.log -> orders.log.1, a new orders.log
        simulator.RollNow();
        simulator.Burst(500);
        run.WaitForCount(ViewerRun.CollectedCountId, 2_000, CountTimeout);

        // A rolled file deleted: the entries already read stay
        simulator.DeleteFile("orders.log.1");
        DesktopApp.Settle(TimeSpan.FromSeconds(2));
        app.ShouldNotHaveCrashed();

        // A new file, created empty and written to 3 s later
        simulator.CreateEmptyFile();
        run.WaitForCount(ViewerRun.CollectedCountId, 2_001, CountTimeout);

        // Nothing read twice
        DesktopApp.Settle(TimeSpan.FromSeconds(2));
        run.ReadCount(ViewerRun.CollectedCountId).Should().Be(2_001, "every entry is read once, also across a roll");
        run.ReadCount(ViewerRun.ShownCountId).Should().BePositive("the list shows the entries");
        app.ShouldNotHaveCrashed();

        app.CloseApp(run.Main).Should().BeTrue("the viewer must close when its window is closed");
    }

    [Fact]
    public async Task Window_Keeps_Answering_During_A_Burst()
    {
        E2ESettings.SkipUnlessEnabled();

        const int burst = 50_000;
        var folder = E2ESettings.NewFolder("burst-logs");
        using var simulator = CreateSimulator(folder);
        simulator.Burst(1_000);

        using var run = new ViewerRun("burst", folder);
        run.WaitForCount(ViewerRun.CollectedCountId, 1_000, CountTimeout);

        // Read the status bar through UI Automation while the burst is written and shown: each
        // read needs the window's UI thread, so a slow read is a window that does not answer.
        var writing = Task.Run(() => simulator.Burst(burst), TestContext.Current.CancellationToken);
        var slowest = TimeSpan.Zero;
        var reads = 0;
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        long count = 0;
        while (count < burst + 1_000 && DateTime.UtcNow < deadline)
        {
            var watch = Stopwatch.StartNew();
            count = run.ReadCount(ViewerRun.CollectedCountId);
            watch.Stop();
            reads++;
            if (watch.Elapsed > slowest)
            {
                slowest = watch.Elapsed;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        await writing;
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{burst:N0} entries: {reads} reads of the status bar, the slowest took {slowest.TotalMilliseconds:N0} ms; count {count:N0}.");

        count.Should().Be(burst + 1_000, "every entry of the burst is shown in the count");
        slowest.Should().BeLessThan(TimeSpan.FromSeconds(2), "the window must keep answering while entries arrive");
        run.App.ShouldNotHaveCrashed();
    }

    [Fact]
    public void Empty_Folder_Shows_A_Message_Not_A_Crash()
    {
        E2ESettings.SkipUnlessEnabled();

        var folder = E2ESettings.NewFolder("empty-logs");

        using var run = new ViewerRun("empty", folder);
        var app = run.App;

        // An owned dialog is also below the main window in the tree, so the main window is left out.
        var mainHandle = run.Main.Properties.NativeWindowHandle.ValueOrDefault;
        var dialog = app.WaitForWindow(
            w => w.Properties.NativeWindowHandle.ValueOrDefault != mainHandle &&
                 (w.Title.Contains("Invalid log folder", StringComparison.OrdinalIgnoreCase) ||
                  app.TryFind(w, "There is no valid log files in this folder") is not null),
            DesktopApp.WindowTimeout,
            "saying the folder has no log files");
        app.CloseWindow(dialog);

        app.ShouldNotHaveCrashed();
        run.ReadCount(ViewerRun.CollectedCountId).Should().Be(0);
    }

    private static LogSimulator CreateSimulator(string folder)
    {
        var simulator = new LogSimulator(
            Options.Create(new SimulatorOptions { Folder = folder }),
            TimeProvider.System);
        simulator.UpdateSettings(new SimulatorSettings(Format: LogFormat.Serilog, FileCount: 2, RollMode: RollMode.Rename));
        return simulator;
    }
}