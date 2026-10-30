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
    private const string NotReadId = "NotRead";

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
    public async Task Window_Keeps_Answering_At_5000_Entries_A_Second()
    {
        E2ESettings.SkipUnlessEnabled();

        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = E2ESettings.NewFolder("rate-logs");
        using var simulator = CreateSimulator(folder);
        simulator.Burst(1_000);

        using var run = new ViewerRun("rate", folder);
        run.WaitForCount(ViewerRun.CollectedCountId, 1_000, CountTimeout);

        // How long a read takes while nothing arrives, to compare with
        var idle = new List<TimeSpan>();
        for (var i = 0; i < 10; i++)
        {
            var idleWatch = Stopwatch.StartNew();
            run.ReadCount(ViewerRun.CollectedCountId);
            idle.Add(idleWatch.Elapsed);
            await Task.Delay(100, cancellationToken);
        }

        idle.Sort();

        // A steady flow arrives as many small batches, each one added to the list on the UI thread
        simulator.UpdateSettings(new SimulatorSettings(Format: LogFormat.Serilog, LinesPerSecond: 5_000, FileCount: 4, RollMode: RollMode.Rename));
        simulator.Start();
        var times = new List<TimeSpan>();
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < end)
        {
            var watch = Stopwatch.StartNew();
            run.ReadCount(ViewerRun.CollectedCountId);
            times.Add(watch.Elapsed);
            await Task.Delay(100, cancellationToken);
        }

        var scrollDuringFlow = run.ReadListScrollPercent();
        simulator.Stop();
        var written = simulator.GetState().EntriesWritten;
        var count = run.WaitForCount(ViewerRun.CollectedCountId, written, CountTimeout);
        DesktopApp.Settle(TimeSpan.FromSeconds(1));
        var scrollAfterFlow = run.ReadListScrollPercent();

        times.Sort();
        var median = times[times.Count / 2];
        var slowest = times[^1];
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{written - 1_000:N0} entries in 10 s: {times.Count} reads of the status bar, median {median.TotalMilliseconds:N0} ms, slowest {slowest.TotalMilliseconds:N0} ms " +
            $"(idle: median {idle[idle.Count / 2].TotalMilliseconds:N0} ms); count {count:N0}; " +
            $"list scrolled to {scrollDuringFlow:N1} % during the flow, {scrollAfterFlow:N1} % after.");

        count.Should().Be(written, "every entry written is counted once");
        slowest.Should().BeLessThan(TimeSpan.FromSeconds(2), "the window must keep answering while entries arrive");
        scrollDuringFlow.Should().BeGreaterThan(90, "auto-scroll keeps the list near the newest entries while they arrive");
        scrollAfterFlow.Should().BeGreaterThan(99.9, "auto-scroll ends at the newest entry");
        run.App.ShouldNotHaveCrashed();
    }

    [Fact]
    public void Lines_And_Files_In_Another_Format_Show_A_Warning()
    {
        E2ESettings.SkipUnlessEnabled();

        var folder = E2ESettings.NewFolder("other-format-logs");
        using var simulator = CreateSimulator(folder);
        simulator.Burst(100);

        using var run = new ViewerRun("other-format", folder);
        var app = run.App;
        run.WaitForCount(ViewerRun.CollectedCountId, 100, CountTimeout);
        run.Main.FindFirstDescendant(app.Conditions.ByAutomationId(NotReadId)).Should().BeNull("everything was read so far");

        // A logger whose layout changed while it kept writing to the same file
        File.AppendAllLines(
            Path.Combine(folder, "orders.log"),
            [.. Enumerable.Range(1, 3).Select(i => $"{DateTime.Now:yyyy-MM-dd HH:mm:ss,fff} [8] INFO  Worker - log4net line {i}")]);

        // The simulator sends a new format to new files
        simulator.UpdateSettings(new SimulatorSettings(Format: LogFormat.Log4Net, FileCount: 2, RollMode: RollMode.Rename));
        simulator.Burst(10);

        var warning = app.FindById(run.Main, NotReadId, TimeSpan.FromSeconds(30));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        string text;
        do
        {
            text = warning.Properties.Name.ValueOrDefault ?? string.Empty;
            if (text.Contains("3 lines", StringComparison.Ordinal) && text.Contains("2 files", StringComparison.Ordinal))
            {
                break;
            }

            DesktopApp.Settle(TimeSpan.FromMilliseconds(250));
        }
        while (DateTime.UtcNow < deadline);

        TestContext.Current.TestOutputHelper?.WriteLine($"Status bar: {text}");
        text.Should().Be("Not read: 3 lines, 2 files");
        run.ReadCount(ViewerRun.CollectedCountId).Should().Be(100, "lines in another format are not read as entries");
        app.ShouldNotHaveCrashed();
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