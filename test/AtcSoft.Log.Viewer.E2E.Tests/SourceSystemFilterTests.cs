namespace AtcSoft.Log.Viewer.E2E;

/// <summary>
/// The source systems filter in the ribbon: ticking two of three systems and pressing OK shows the entries
/// of those two, Clear shows every entry again, and after Select all a new system shows too.
/// </summary>
/// <remarks>
/// The ticks apply on OK: applying the filter disables the window while it runs, which would close the
/// drop-down after the first tick.
/// </remarks>
[Collection(E2ETestGroup.Name)]
public sealed class SourceSystemFilterTests
{
    private const string FilterId = "SourceSystemsFilter";

    private static readonly TimeSpan CountTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public void Filters_On_The_Ticked_Source_Systems()
    {
        E2ESettings.SkipUnlessEnabled();

        // Arrange
        var folder = E2ESettings.NewFolder("source-system-filter");
        WriteLog(folder, "Contoso.Orders", 3);
        WriteLog(folder, "Contoso.Billing", 2);
        WriteLog(folder, "Contoso.Identity", 1);

        using var run = new ViewerRun("source-system-filter", folder, LogFileCollectorType.Serilog);
        var app = run.App;
        run.WaitForCount(ViewerRun.CollectedCountId, 6, CountTimeout);
        WaitForExactCount(run, ViewerRun.ShownCountId, 6);

        // Act
        var comboBox = app.FindWhere(
            app.FindById(run.Main, FilterId),
            e => e.ControlType == ControlType.ComboBox,
            "that is a combo box");
        comboBox.Patterns.ExpandCollapse.Pattern.Expand();
        app.Find(comboBox, "Contoso.Orders", ControlType.CheckBox).Patterns.Toggle.Pattern.Toggle();
        app.Find(comboBox, "Contoso.Identity", ControlType.CheckBox).Patterns.Toggle.Pattern.Toggle();
        DesktopApp.Press(app.FindById(comboBox, "Ok"));

        // Assert
        WaitForExactCount(run, ViewerRun.ShownCountId, 4);
        comboBox.Patterns.Value.Pattern.Value.Value.Should().Be("Contoso.Identity, Contoso.Orders");

        // The date pickers have "Clear" buttons too; this is the one in the ribbon
        DesktopApp.Press(app.FindWhere(
            run.Main,
            e => e.Properties.Name.ValueOrDefault == "Clear" && e.Properties.ClassName.ValueOrDefault == "RibbonButton",
            "that is the ribbon's Clear button"));
        WaitForExactCount(run, ViewerRun.ShownCountId, 6);

        // Select all, then a system that wasn't there when it was pressed
        comboBox.Patterns.ExpandCollapse.Pattern.Expand();
        app.FindById(comboBox, "SelectAll").Patterns.Toggle.Pattern.Toggle();
        DesktopApp.Press(app.FindById(comboBox, "Ok"));
        WriteLog(folder, "Contoso.Shipping", 1);
        run.WaitForCount(ViewerRun.CollectedCountId, 7, CountTimeout);
        WaitForExactCount(run, ViewerRun.ShownCountId, 7);
        comboBox.Patterns.Value.Pattern.Value.Value.Should().Be("All");

        app.ShouldNotHaveCrashed();
        app.CloseApp(run.Main).Should().BeTrue("the viewer must close when its window is closed");
    }

    private static void WaitForExactCount(
        ViewerRun run,
        string automationId,
        long expected)
    {
        var deadline = DateTime.UtcNow + CountTimeout;
        long count;
        do
        {
            run.App.ShouldNotHaveCrashed();
            count = run.ReadCount(automationId);
            if (count == expected)
            {
                return;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        throw run.App.Fail($"The status bar count '{automationId}' is {count:N0}, not {expected:N0}.");
    }

    private static void WriteLog(
        string folder,
        string sourceSystem,
        int entries)
    {
        var start = DateTime.Now.AddMinutes(-10);
        var lines = Enumerable
            .Range(0, entries)
            .Select(i => $"{start.AddSeconds(i).ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)} [INF] {sourceSystem} entry {i + 1}");
        File.WriteAllLines(Path.Combine(folder, $"{sourceSystem}-{DateTime.Now:yyyyMMdd}.log"), lines);
    }
}