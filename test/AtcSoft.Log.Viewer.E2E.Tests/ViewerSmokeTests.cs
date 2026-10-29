namespace AtcSoft.Log.Viewer.E2E;

/// <summary>
/// The viewer from start to close with no log folder: the ribbon's tabs and views, the
/// highlights pane, and the dialogs the Application menu opens. It fails on a step that
/// breaks, a crash dialog or the app exiting.
/// </summary>
/// <remarks>
/// Left out on purpose: the file and folder pickers (Open profile, Open log folder; the
/// other tests open a folder through a profile instead), Save profile and Exit.
/// </remarks>
[Collection(E2ETestGroup.Name)]
public sealed class ViewerSmokeTests
{
    [Fact]
    public void Every_View_And_Dialog_Opens_Without_A_Crash()
    {
        E2ESettings.SkipUnlessEnabled();

        using var run = new ViewerRun("smoke");
        var app = run.App;
        var main = run.Main;
        var steps = new SmokeSteps(recover: () => CloseDialogs(app, main));

        steps.Run("UI tree at start", () => app.DumpTree("start"));

        // Ribbon
        steps.Run("Home tab", () => app.SelectTab(main, "Home"));
        steps.Run("Highlights pane", () => PressAndCheck(app, main, "Highlights"));
        steps.Run("View tab", () => app.SelectTab(main, "View"));
        steps.Run("Log Level Distribution", () => PressAndCheck(app, main, "Log Level Distribution"));
        steps.Run("Timeline Distribution", () => PressAndCheck(app, main, "Timeline Distribution"));
        steps.Run("List View", () => PressAndCheck(app, main, "List View"));
        steps.Run("back to Home tab", () => app.SelectTab(main, "Home"));

        // Application menu (the ribbon's backstage)
        foreach (var item in new[] { "About", "Application settings", "New profile" })
        {
            steps.Run(item, () =>
            {
                var dialog = app.OpenWindow(() => PressApplicationMenuItem(app, main, item), item);
                DesktopApp.Settle(TimeSpan.FromMilliseconds(800));
                app.ShouldNotHaveCrashed();
                app.CloseWindow(dialog);
            });
        }

        steps.Run("no crash", app.ShouldNotHaveCrashed);
        steps.Failures.Should().BeEmpty(steps.Report());

        app.CloseApp(main).Should().BeTrue("the viewer must close when its window is closed");
    }

    /// <summary>
    /// Opens the Application menu (the ribbon's backstage) and presses one of its items.
    /// </summary>
    /// <param name="app">The app.</param>
    /// <param name="main">The main window.</param>
    /// <param name="itemName">The item's text.</param>
    internal static void PressApplicationMenuItem(
        DesktopApp app,
        Window main,
        string itemName)
    {
        var item = app.TryFind(main, itemName);
        if (item is null || item.IsOffscreen)
        {
            // The ribbon's Backstage has no name in the UI tree, only its class.
            DesktopApp.Press(app.FindWhere(
                main,
                e => string.Equals(e.Properties.ClassName.ValueOrDefault, "Backstage", StringComparison.Ordinal),
                "of the class Backstage (the Application menu)"));

            item = app.FindWhere(
                main,
                e => string.Equals(e.Properties.Name.ValueOrDefault, itemName, StringComparison.Ordinal) && !e.IsOffscreen,
                $"named '{itemName}' in the Application menu");
        }

        DesktopApp.Press(item);
    }

    private static void PressAndCheck(
        DesktopApp app,
        Window main,
        string name)
    {
        DesktopApp.Press(app.Find(main, name));
        DesktopApp.Settle(TimeSpan.FromMilliseconds(600));
        app.ShouldNotHaveCrashed();
    }

    private static void CloseDialogs(
        DesktopApp app,
        Window main)
    {
        var mainHandle = main.Properties.NativeWindowHandle.ValueOrDefault;
        foreach (var window in app.Windows().Where(w => w.Properties.NativeWindowHandle.ValueOrDefault != mainHandle).Reverse())
        {
            app.CloseWindow(window);
        }
    }
}