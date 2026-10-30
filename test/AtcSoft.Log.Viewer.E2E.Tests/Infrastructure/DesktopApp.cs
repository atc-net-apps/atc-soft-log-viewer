namespace AtcSoft.Log.Viewer.E2E.Infrastructure;

/// <summary>
/// The viewer under test, driven through UI Automation patterns only (no mouse, no keystrokes):
/// find by name or automation id, invoke, select tabs, close windows. Every wait has a deadline.
/// </summary>
internal sealed class DesktopApp : IDisposable
{
    /// <summary>
    /// The window class of a plain Windows message box (and the common file dialogs).
    /// </summary>
    private const string PlainMessageBoxClass = "#32770";

    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan ElementTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The titles of the message boxes the app shows when something goes wrong
    /// (App.xaml.cs and the profile loading).
    /// </summary>
    private static readonly string[] CrashTitles =
    [
        "Dispatcher Unhandled Exception",
        "CurrentDomain Unhandled Exception",
        "Error",
    ];

    private readonly FlaUI.Core.Application application;
    private readonly string outputHint;
    private readonly string dumpFolder;
    private int dumpCount;

    /// <param name="process">The app's process.</param>
    /// <param name="outputHint">Where the app's output is, for a failure message.</param>
    /// <param name="dumpFolder">Where the UI tree dumps go.</param>
    public DesktopApp(
        Process process,
        string outputHint,
        string dumpFolder)
    {
        ArgumentNullException.ThrowIfNull(process);

        Process = process;
        this.outputHint = outputHint;
        this.dumpFolder = dumpFolder;
        Automation = new UIA3Automation();
        application = FlaUI.Core.Application.Attach(process.Id);
    }

    public Process Process { get; }

    public UIA3Automation Automation { get; }

    public ConditionFactory Conditions
        => Automation.ConditionFactory;

    /// <summary>
    /// Gets the app's windows: the top-level ones and every window they own. UI Automation
    /// shows an owned (modal) dialog as a child of its owner, not of the desktop.
    /// </summary>
    /// <returns>The windows.</returns>
    public IReadOnlyList<Window> Windows()
    {
        var windows = new List<Window>();
        try
        {
            foreach (var window in application.GetAllTopLevelWindows(Automation))
            {
                // A context menu or a drop-down is a pop-up window, not a dialog.
                if (!string.Equals(window.Properties.ClassName.ValueOrDefault, "Popup", StringComparison.Ordinal))
                {
                    AddWithOwned(window, windows, depth: 0);
                }
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or TimeoutException or InvalidOperationException)
        {
            // The app is between windows.
        }

        return windows;
    }

    public Window WaitForWindow(
        Func<Window, bool> match,
        TimeSpan timeout,
        string what)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ThrowIfExited(what);

            var window = Windows().FirstOrDefault(w => SafeMatch(match, w));
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(250);
        }

        throw Fail($"No window {what} within {timeout.TotalSeconds:N0} s.");
    }

    public Window WaitForWindowTitled(
        string titlePart,
        TimeSpan? timeout = null)
        => WaitForWindow(
            w => w.Title.Contains(titlePart, StringComparison.OrdinalIgnoreCase),
            timeout ?? WindowTimeout,
            $"titled '{titlePart}'");

    /// <summary>
    /// Invokes what <paramref name="open"/> finds and waits for the new window it opens.
    /// </summary>
    /// <param name="open">Presses what opens the window.</param>
    /// <param name="what">What opens it, for a failure message.</param>
    /// <returns>The new window.</returns>
    public Window OpenWindow(
        Action open,
        string what)
    {
        ArgumentNullException.ThrowIfNull(open);

        var before = Windows().Select(Handle).ToHashSet();
        open();
        return WaitForWindow(w => !before.Contains(Handle(w)), WindowTimeout, $"opened by {what}");
    }

    /// <summary>
    /// Fails when the app shows one of its crash dialogs or a plain Windows message box
    /// (not in the app's style), or has exited.
    /// </summary>
    public void ShouldNotHaveCrashed()
    {
        ThrowIfExited("the check for crash dialogs");

        var windows = Windows();
        var crash = windows.FirstOrDefault(w => CrashTitles.Any(t => string.Equals(SafeName(w), t, StringComparison.Ordinal)));
        if (crash is not null)
        {
            throw Fail($"The app shows the crash dialog '{SafeName(crash)}' ({SafeClassName(crash)}): {TextOf(crash)}");
        }

        var plain = windows.FirstOrDefault(w => string.Equals(SafeClassName(w), PlainMessageBoxClass, StringComparison.Ordinal));
        if (plain is not null)
        {
            throw Fail($"The app shows a plain Windows message box '{SafeName(plain)}', not a dialog in its own style: {TextOf(plain)}");
        }
    }

    public AutomationElement Find(
        AutomationElement root,
        string name,
        ControlType? controlType = null,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? ElementTimeout);
        while (DateTime.UtcNow < deadline)
        {
            var element = TryFind(root, name, controlType);
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(200);
        }

        throw Fail($"No {controlType?.ToString() ?? "element"} named '{name}' in '{SafeName(root)}'.");
    }

    public AutomationElement? TryFind(
        AutomationElement root,
        string name,
        ControlType? controlType = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        ConditionBase condition = Conditions.ByName(name);
        if (controlType is { } type)
        {
            condition = condition.And(Conditions.ByControlType(type));
        }

        try
        {
            return root.FindFirstDescendant(condition);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    public AutomationElement FindById(
        AutomationElement root,
        string automationId,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var deadline = DateTime.UtcNow + (timeout ?? ElementTimeout);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (root.FindFirstDescendant(Conditions.ByAutomationId(automationId)) is { } element)
                {
                    return element;
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // The window is changing.
            }

            Thread.Sleep(200);
        }

        throw Fail($"No element with the automation id '{automationId}' in '{SafeName(root)}'.");
    }

    /// <summary>
    /// Finds the first element below the root that the predicate accepts, waiting for it.
    /// </summary>
    /// <param name="root">Where to look.</param>
    /// <param name="predicate">What to find.</param>
    /// <param name="what">What is looked for, for a failure message.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>The element.</returns>
    public AutomationElement FindWhere(
        AutomationElement root,
        Func<AutomationElement, bool> predicate,
        string what,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? ElementTimeout);
        while (DateTime.UtcNow < deadline)
        {
            var found = SafeFindAll(root, TrueCondition.Default)
                .FirstOrDefault(e => SafeMatch(predicate, e));
            if (found is not null)
            {
                return found;
            }

            Thread.Sleep(250);
        }

        throw Fail($"Nothing {what} in '{SafeName(root)}'.");
    }

    /// <summary>
    /// Gets whether <see cref="Press"/> can press the element: a button's text label has the button's name, but can't be pressed.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when the element has a pattern to press it with.</returns>
    public static bool CanPress(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var patterns = element.Patterns;
        return patterns.Invoke.IsSupported ||
               patterns.Toggle.IsSupported ||
               patterns.SelectionItem.IsSupported ||
               patterns.ExpandCollapse.IsSupported;
    }

    /// <summary>
    /// Presses a button, a ribbon button, a menu item or a tab the way UI Automation does:
    /// Invoke, else Toggle, else Select, else Expand. Never a mouse click.
    /// </summary>
    /// <param name="element">The element.</param>
    public static void Press(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var patterns = element.Patterns;
        if (patterns.Invoke.IsSupported)
        {
            patterns.Invoke.Pattern.Invoke();
        }
        else if (patterns.Toggle.IsSupported)
        {
            patterns.Toggle.Pattern.Toggle();
        }
        else if (patterns.SelectionItem.IsSupported)
        {
            patterns.SelectionItem.Pattern.Select();
        }
        else if (patterns.ExpandCollapse.IsSupported)
        {
            patterns.ExpandCollapse.Pattern.Expand();
        }
        else
        {
            throw new InvalidOperationException($"'{SafeName(element)}' ({element.Properties.ControlType.ValueOrDefault}) has no pattern to press it with.");
        }
    }

    /// <summary>
    /// Selects the tab with the name.
    /// </summary>
    /// <param name="root">Where the tab is.</param>
    /// <param name="name">The tab's header.</param>
    public void SelectTab(
        AutomationElement root,
        string name)
    {
        var tab = Find(root, name, ControlType.TabItem);
        tab.Patterns.SelectionItem.Pattern.Select();
        Settle(TimeSpan.FromMilliseconds(400));
    }

    /// <summary>
    /// Closes a dialog with its Cancel / Close / OK button, else through the window itself,
    /// and waits until it is gone.
    /// </summary>
    /// <param name="window">The dialog.</param>
    public void CloseWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = Handle(window);
        foreach (var name in new[] { "Cancel", "Close", "OK", "No" })
        {
            var button = TryFind(window, name, ControlType.Button);
            if (button is null || !button.IsEnabled || button.IsOffscreen)
            {
                continue;
            }

            try
            {
                Press(button);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                continue;
            }

            if (WaitGone(handle, TimeSpan.FromSeconds(5)))
            {
                return;
            }
        }

        try
        {
            window.Close();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Closing as it closes.
        }

        if (!WaitGone(handle, TimeSpan.FromSeconds(10)))
        {
            throw Fail($"The window '{SafeName(window)}' did not close.");
        }
    }

    /// <summary>
    /// Writes every window's UI Automation tree to a file in the run folder, for a failure
    /// and for writing new steps.
    /// </summary>
    /// <param name="label">A word for the file name.</param>
    /// <returns>The file written.</returns>
    public string DumpTree(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        Directory.CreateDirectory(dumpFolder);
        var path = Path.Combine(
            dumpFolder,
            $"{Interlocked.Increment(ref dumpCount):00}-{string.Concat(label.Where(char.IsLetterOrDigit))}.txt");
        var text = new StringBuilder();
        foreach (var window in Windows())
        {
            Dump(window, 0, text);
        }

        File.WriteAllText(path, text.ToString());
        return path;
    }

    public Exception Fail(string message)
    {
        string dump;
        try
        {
            dump = DumpTree("failure");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or InvalidOperationException)
        {
            dump = "(no dump: " + ex.Message + ")";
        }

        return new InvalidOperationException($"{message} UI tree: {dump}");
    }

    /// <summary>
    /// Gives the app time to finish what a step started.
    /// </summary>
    /// <param name="duration">How long.</param>
    public static void Settle(TimeSpan duration)
        => Thread.Sleep(duration);

    /// <summary>
    /// Asks the app to close its main window and waits for the process to end.
    /// </summary>
    /// <param name="mainWindow">The main window.</param>
    /// <returns><see langword="true"/> when it ended by itself.</returns>
    public bool CloseApp(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        try
        {
            mainWindow.Close();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Closing as it closes.
        }

        return Process.WaitForExit(30_000);
    }

    public void Dispose()
    {
        Automation.Dispose();
        application.Dispose();
    }

    private static IntPtr Handle(Window window)
    {
        try
        {
            return window.Properties.NativeWindowHandle.ValueOrDefault;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return IntPtr.Zero;
        }
    }

    private static string SafeName(AutomationElement element)
    {
        try
        {
            return element.Properties.Name.ValueOrDefault ?? string.Empty;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return string.Empty;
        }
    }

    private static string SafeClassName(AutomationElement element)
    {
        try
        {
            return element.Properties.ClassName.ValueOrDefault ?? string.Empty;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return string.Empty;
        }
    }

    private static string TextOf(AutomationElement window)
        => string.Join(
            " | ",
            SafeFindAll(window, TrueCondition.Default)
                .Where(e => e.Properties.ControlType.ValueOrDefault == ControlType.Text)
                .Select(SafeName)
                .Where(x => x.Length > 0));

    private static bool SafeMatch<T>(
        Func<T, bool> match,
        T item)
    {
        try
        {
            return match(item);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            return false;
        }
    }

    private static AutomationElement[] SafeFindAll(
        AutomationElement root,
        ConditionBase condition,
        TreeScope scope = TreeScope.Descendants)
    {
        try
        {
            return scope == TreeScope.Children ? root.FindAllChildren(condition) : root.FindAllDescendants(condition);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return [];
        }
    }

    private static void Dump(
        AutomationElement element,
        int depth,
        StringBuilder text)
    {
        if (depth > 40)
        {
            return;
        }

        try
        {
            var properties = element.Properties;
            text.Append(' ', depth * 2)
                .Append(properties.ControlType.ValueOrDefault)
                .Append(" name='").Append(properties.Name.ValueOrDefault)
                .Append("' id='").Append(properties.AutomationId.ValueOrDefault)
                .Append("' class='").Append(properties.ClassName.ValueOrDefault)
                .Append("' enabled=").Append(properties.IsEnabled.ValueOrDefault)
                .Append(" offscreen=").Append(properties.IsOffscreen.ValueOrDefault)
                .AppendLine();

            foreach (var child in element.FindAllChildren())
            {
                Dump(child, depth + 1, text);
            }
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            text.Append(' ', depth * 2).AppendLine("(gone)");
        }
    }

    private void ThrowIfExited(string what)
    {
        if (Process.HasExited)
        {
            throw new InvalidOperationException($"The app exited (code {Process.ExitCode}) while waiting for {what}. Output: {outputHint}");
        }
    }

    private void AddWithOwned(
        Window window,
        List<Window> windows,
        int depth)
    {
        windows.Add(window);
        if (depth > 5)
        {
            return;
        }

        foreach (var child in SafeFindAll(window, Conditions.ByControlType(ControlType.Window), TreeScope.Children))
        {
            AddWithOwned(child.AsWindow(), windows, depth + 1);
        }
    }

    private bool WaitGone(
        IntPtr handle,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!Windows().Any(w => Handle(w) == handle))
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return false;
    }
}