namespace AtcSoft.Log.Viewer.E2E;

/// <summary>
/// The sample AppHost started with Aspire.Hosting.Testing, the way F5 starts it, writing to a
/// folder below the run folder instead of sample/output.
/// </summary>
[Collection(E2ETestGroup.Name)]
public sealed class SampleAppHostTests
{
    private const string Simulator = "log-simulator";
    private const string Viewer = "log-viewer";
    private const string ViewerProcessName = "AtcSoft.Log.Viewer.Wpf.App";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The simulator page in Edge (headless): Start and Stop change the status, a burst and a
    /// roll show in the files table and in the folder, and the page has no errors.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Simulator_Page_Controls_The_Writing()
    {
        E2ESettings.SkipUnlessEnabled();

        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = E2ESettings.NewFolder("apphost-logs");

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AtcSoft_Log_Viewer_Sample_AppHost>(
            ["StartViewer=false", $"LogFolder={folder}"],
            cancellationToken);
        builder.Resources.Should().NotContain(x => x.Name == Viewer, "StartViewer=false leaves the viewer out");

        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        await app.ResourceNotifications
            .WaitForResourceAsync(Simulator, KnownResourceStates.Running, cancellationToken)
            .WaitAsync(StartTimeout, cancellationToken);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { Locale = "en-US" });
        var problems = new List<string>();
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.Ordinal))
            {
                problems.Add($"console: {message.Text}");
            }
        };
        page.PageError += (_, error) => problems.Add($"page error: {error}");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                problems.Add($"{response.Status} {response.Request.Method} {response.Url}");
            }
        };

        await page.GotoAsync(app.GetEndpoint(Simulator, "http").ToString());

        // The page shows the folder the AppHost gave the simulator
        await Assertions.Expect(page.Locator("#folder")).ToHaveTextAsync(folder);

        // Start and a burst
        await page.ClickAsync("#start");
        await Assertions.Expect(page.Locator("#status")).ToHaveTextAsync("running");
        await page.ClickAsync("[data-burst='1000']");
        await Assertions.Expect(page.Locator("#files")).ToContainTextAsync("orders.log");
        await Assertions.Expect(page.Locator("#files")).ToContainTextAsync("payments.log");

        // Roll by rename
        await page.SelectOptionAsync("#rollMode", "Rename");
        await page.ClickAsync("#roll");
        await Assertions.Expect(page.Locator("#files")).ToContainTextAsync("orders.log.1");
        File.Exists(Path.Combine(folder, "orders.log.1")).Should().BeTrue("the roll renames the file in the folder");

        // Stop
        await page.ClickAsync("#stop");
        await Assertions.Expect(page.Locator("#status")).ToHaveTextAsync("stopped");
        await Assertions.Expect(page.Locator("#events")).ToContainTextAsync("Stopped");

        problems.Should().BeEmpty("the simulator page must work without errors");
    }

    /// <summary>
    /// The AppHost starts the viewer as well, and its window opens: the case where Aspire's
    /// dashboard listed the viewer but no window showed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Viewer_Starts_From_The_AppHost_With_A_Window()
    {
        E2ESettings.SkipUnlessEnabled();

        var cancellationToken = TestContext.Current.CancellationToken;
        var folder = E2ESettings.NewFolder("apphost-viewer-logs");
        var viewerData = E2ESettings.NewFolder("apphost-viewer-data");
        var startedAfter = DateTime.Now;

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AtcSoft_Log_Viewer_Sample_AppHost>(
            [$"LogFolder={folder}", $"ViewerDataPath={viewerData}"],
            cancellationToken);

        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        await app.ResourceNotifications
            .WaitForResourceAsync(Viewer, KnownResourceStates.Running, cancellationToken)
            .WaitAsync(StartTimeout, cancellationToken);

        // Aspire starts the viewer with dotnet run, which starts the viewer's executable
        var viewer = await WaitForProcessAsync(ViewerProcessName, startedAfter, StartTimeout, cancellationToken);
        try
        {
            using var desktop = new DesktopApp(viewer, $"the {Viewer} console log in the Aspire dashboard", Path.Combine(viewerData, "ui-trees"));
            var main = desktop.WaitForWindowTitled(ViewerRun.MainWindowTitle, StartTimeout);

            main.IsOffscreen.Should().BeFalse("the viewer's window must show on the screen");
            desktop.ShouldNotHaveCrashed();
        }
        finally
        {
            if (!viewer.HasExited)
            {
                viewer.Kill(entireProcessTree: true);
            }

            viewer.Dispose();
        }
    }

    private static async Task<Process> WaitForProcessAsync(
        string name,
        DateTime startedAfter,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var found = default(Process);
            foreach (var process in Process.GetProcessesByName(name))
            {
                if (found is null && StartedAfter(process, startedAfter))
                {
                    found = process;
                }
                else
                {
                    process.Dispose();
                }
            }

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"No {name} process started within {timeout.TotalSeconds:N0} s.");
    }

    private static bool StartedAfter(
        Process process,
        DateTime time)
    {
        try
        {
            return process.StartTime >= time;
        }
        catch (InvalidOperationException)
        {
            // It exited.
            return false;
        }
    }
}