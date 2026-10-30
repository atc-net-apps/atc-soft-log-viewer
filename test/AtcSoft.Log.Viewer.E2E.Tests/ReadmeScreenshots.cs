namespace AtcSoft.Log.Viewer.E2E;

/// <summary>
/// Takes the README's screenshots: the list and the two charts, in the light and the dark theme, with
/// sample logs of the last 50 minutes. Not a test: it runs only when asked, because it writes to docs/images.
/// </summary>
/// <remarks>
/// Run it with the UI tests enabled: <c>ATCSOFT_E2E=1</c> and <c>ATCSOFT_SCREENSHOTS=1</c>. The window must be
/// visible on the screen while it runs, since a screenshot is taken of the screen.
/// </remarks>
[Collection(E2ETestGroup.Name)]
public sealed class ReadmeScreenshots
{
    public const string EnableVariable = "ATCSOFT_SCREENSHOTS";

    private const nint PerMonitorAwareV2 = -4;
    private const int ExtendedFrameBounds = 9;

    private static readonly string[] Systems = ["Contoso.Orders", "Contoso.Billing", "Contoso.Identity"];

    private static readonly string[] InfoMessages =
    [
        "Order {0} received from customer {1}",
        "Order {0} validated in {2} ms",
        "Invoice {0} created for customer {1}",
        "User {1} signed in",
        "Payment for order {0} captured",
        "Order {0} shipped with tracking number {3}",
    ];

    [Theory]
    [InlineData("Light.Blue", "lm")]
    [InlineData("Dark.Blue", "dm")]
    public void Capture(
        string theme,
        string prefix)
    {
        Assert.SkipUnless(
            E2ESettings.IsEnabled &&
            string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal),
            $"Takes the README screenshots only when {E2ESettings.EnableVariable}=1 and {EnableVariable}=1.");

        // UI Automation and the screen capture must agree on pixels when Windows scales the display
        SetProcessDpiAwarenessContext(PerMonitorAwareV2);

        // Arrange
        var folder = E2ESettings.NewFolder("screenshot-logs-" + prefix);
        var count = WriteSampleLogs(folder);

        using var run = new ViewerRun(
            "screenshots-" + prefix,
            folder,
            LogFileCollectorType.Serilog,
            settings: new Dictionary<string, string>(StringComparer.Ordinal) { ["Application__Theme"] = theme });
        var app = run.App;
        var main = run.Main;
        run.WaitForCount(ViewerRun.CollectedCountId, count, TimeSpan.FromSeconds(60));

        if (main.Patterns.Window.TryGetPattern(out var window))
        {
            window.SetWindowVisualState(WindowVisualState.Normal);
        }

        var scale = GetDpiForWindow(main.Properties.NativeWindowHandle.Value) / 96.0;
        var transform = main.Patterns.Transform.Pattern;
        transform.Move(40, 40);
        transform.Resize(1400 * scale, 860 * scale);
        main.SetForeground();
        DesktopApp.Settle(TimeSpan.FromSeconds(1.5));

        // Act
        Save(main, prefix + "-list.png");

        app.SelectTab(main, "View");
        DesktopApp.Press(app.Find(main, "Log Level Distribution"));
        DesktopApp.Settle(TimeSpan.FromSeconds(2));
        Save(main, prefix + "-graph-pie.png");

        DesktopApp.Press(app.Find(main, "Timeline Distribution"));
        DesktopApp.Settle(TimeSpan.FromSeconds(2));
        Save(main, prefix + "-graph-bar.png");

        // Assert
        app.ShouldNotHaveCrashed();
        app.CloseApp(main).Should().BeTrue("the viewer must close when its window is closed");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(nint value);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static extern uint GetDpiForWindow(nint hwnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static extern nint GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static extern int DwmGetWindowAttribute(
        nint hwnd,
        int attribute,
        out NativeRect value,
        int size);

    /// <summary>
    /// Saves the window as Windows draws it, without the invisible resize border around it.
    /// </summary>
    private static void Save(
        AutomationElement window,
        string fileName)
    {
        // A screenshot is taken of the screen, so the viewer must be in front of every other window
        var handle = window.Properties.NativeWindowHandle.Value;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (GetForegroundWindow() != handle)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new InvalidOperationException($"The viewer is not the foreground window, so {fileName} would show another window.");
            }

            window.AsWindow().SetForeground();
            DesktopApp.Settle(TimeSpan.FromMilliseconds(500));
        }

        DesktopApp.Settle(TimeSpan.FromMilliseconds(500));
        var path = Path.Combine(E2ESettings.RepositoryRoot, "docs", "images", fileName);
        var bounds = window.BoundingRectangle;
        if (DwmGetWindowAttribute(window.Properties.NativeWindowHandle.Value, ExtendedFrameBounds, out var frame, System.Runtime.InteropServices.Marshal.SizeOf<NativeRect>()) == 0)
        {
            bounds = new System.Drawing.Rectangle(frame.Left, frame.Top, frame.Right - frame.Left, frame.Bottom - frame.Top);
        }

        FlaUI.Core.Capturing.Capture.Rectangle(bounds).ToFile(path);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Writes one Serilog file per system with entries over the last 50 minutes: mostly information,
    /// some debug and trace, a few warnings, errors with a stack trace, and one critical.
    /// </summary>
    /// <returns>The number of entries written.</returns>
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Sample log content, seeded so every run looks the same.")]
    private static int WriteSampleLogs(string folder)
    {
        var random = new Random(42);
        var now = DateTime.Now;
        var total = 0;
        foreach (var system in Systems)
        {
            var lines = new List<string>();
            var time = now.AddMinutes(-50);
            while (time < now.AddSeconds(-5))
            {
                time = time.AddMilliseconds(random.Next(2_000, 20_000));
                var roll = random.Next(100);
                var (level, message, stackTrace) = roll switch
                {
                    < 8 => ("VRB", $"Cache lookup for key order:{random.Next(1_000, 9_999)}", false),
                    < 22 => ("DBG", $"Query took {random.Next(2, 80)} ms", false),
                    < 82 => ("INF", string.Format(CultureInfo.InvariantCulture, InfoMessages[random.Next(InfoMessages.Length)], random.Next(60_000, 69_999), random.Next(100, 999), random.Next(5, 300), $"DK{random.Next(100_000, 999_999)}"), false),
                    < 93 => ("WRN", $"Retrying call to the payment gateway, attempt {random.Next(2, 4)}", false),
                    < 99 => ("ERR", $"Order {random.Next(60_000, 69_999)} could not be processed", true),
                    _ => ("FTL", "Database connection lost", true),
                };

                lines.Add($"{time.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)} [{level}] {message}");
                if (stackTrace)
                {
                    lines.Add("System.InvalidOperationException: The operation could not be completed");
                    lines.Add($"   at {system}.Worker.ProcessAsync(CancellationToken token) in Worker.cs:line {random.Next(20, 200)}");
                }

                total++;
            }

            File.WriteAllLines(Path.Combine(folder, $"{system}-{now:yyyyMMdd}.log"), lines);
        }

        return total;
    }
}