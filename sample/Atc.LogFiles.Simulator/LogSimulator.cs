namespace Atc.LogFiles.Simulator;

/// <summary>
/// Writes log entries to a folder like a running system does, and rolls, adds and deletes files on request.
/// </summary>
/// <remarks>
/// Every action runs under one lock, so a roll or a delete never lands in the middle of a write.
/// Each write opens the file, appends and closes it, as file loggers do, so the file can be renamed between writes.
/// </remarks>
public sealed class LogSimulator : IDisposable
{
    private const int MaxEvents = 40;
    private const int MaxRenamedFiles = 5;

    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan EmptyFileFirstWriteDelay = TimeSpan.FromSeconds(3);

    private static readonly string[] ServiceNames = ["orders", "payments", "shipping", "inventory", "billing", "auth", "search", "email"];

    private static readonly string[] InfoMessages =
    [
        "Order {0} received from customer {1}",
        "Payment {0} authorized for {1} DKK",
        "Shipment {0} handed over to the carrier",
        "Cache refreshed with {0} items in {1} ms",
        "Request GET /api/orders/{0} answered in {1} ms",
        "User {1} signed in from a new device",
    ];

    private static readonly string[] WarningMessages =
    [
        "Retrying call to the payment gateway, attempt {0} of 3",
        "Slow response from the inventory service: {1} ms",
        "Queue length is {0}, above the warning limit",
    ];

    private static readonly string[] ErrorMessages =
    [
        "Order {0} could not be processed",
        "Timeout calling the shipping service after {1} ms",
        "Payment {0} was declined by the bank",
    ];

    private readonly Lock syncLock = new();
    private readonly Random random = new();
    private readonly Timer timer;
    private readonly Timer emptyFileTimer;
    private readonly DirectoryInfo folder;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, string> currentFileNames = new(StringComparer.Ordinal);
    private readonly LinkedList<SimulatorEvent> events = new();
    private readonly List<string> emptyFilesWaitingForFirstLine = [];
    private SimulatorSettings settings = new();
    private bool isRunning;
    private double pendingEntries;
    private long entriesWritten;
    private int serviceIndex;
    private int extraFileCount;

    public LogSimulator(
        IOptions<SimulatorOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.timeProvider = timeProvider;
        folder = Directory.CreateDirectory(options.Value.Folder);
        timer = new Timer(_ => Tick());
        emptyFileTimer = new Timer(_ => WriteFirstLineToEmptyFiles());
    }

    public SimulatorState GetState()
    {
        lock (syncLock)
        {
            folder.Refresh();
            var files = folder
                .GetFiles()
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new SimulatorFile(x.Name, x.Length, x.LastWriteTime))
                .ToArray();

            return new SimulatorState(
                isRunning,
                folder.FullName,
                settings,
                entriesWritten,
                files,
                [.. events]);
        }
    }

    public void UpdateSettings(SimulatorSettings newSettings)
    {
        ArgumentNullException.ThrowIfNull(newSettings);

        lock (syncLock)
        {
            var oldFormat = settings.Format;
            settings = newSettings.Normalize();
            AddEvent($"Settings: {settings.Format}, {settings.LinesPerSecond} entries/s, {settings.FileCount} files, roll {settings.RollMode}" +
                     (settings.RollSizeKb > 0 ? $" at {settings.RollSizeKb} KB" : string.Empty));

            if (settings.Format != oldFormat)
            {
                StartNewFilesForNewFormat();
            }
        }
    }

    public void Start()
    {
        lock (syncLock)
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;
            pendingEntries = 0;
            timer.Change(TickInterval, TickInterval);
            AddEvent("Started");
        }
    }

    public void Stop()
    {
        lock (syncLock)
        {
            if (!isRunning)
            {
                return;
            }

            isRunning = false;
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            AddEvent("Stopped");
        }
    }

    /// <summary>
    /// Rolls the current file of every service now.
    /// </summary>
    public void RollNow()
    {
        lock (syncLock)
        {
            for (var i = 0; i < settings.FileCount; i++)
            {
                Roll(ServiceNames[i]);
            }
        }
    }

    /// <summary>
    /// Writes many entries at once, spread over the files, like a busy moment or a flood of errors.
    /// </summary>
    /// <param name="count">The number of entries.</param>
    public void Burst(int count)
    {
        count = Math.Clamp(count, 1, 1_000_000);
        lock (syncLock)
        {
            WriteEntries(count);
            AddEvent($"Burst of {count:N0} entries");
        }
    }

    /// <summary>
    /// Creates an empty log file and writes its first line a few seconds later,
    /// like a logger that creates the file before it has anything to write.
    /// </summary>
    public void CreateEmptyFile()
    {
        lock (syncLock)
        {
            extraFileCount++;
            var fileName = $"late-{extraFileCount}.log";
            if (TryRun($"Create empty {fileName}", () => File.WriteAllBytes(Path.Combine(folder.FullName, fileName), [])))
            {
                emptyFilesWaitingForFirstLine.Add(fileName);
                emptyFileTimer.Change(EmptyFileFirstWriteDelay, Timeout.InfiniteTimeSpan);
                AddEvent($"Created empty {fileName}; the first line follows in {EmptyFileFirstWriteDelay.TotalSeconds:0} s");
            }
        }
    }

    public void DeleteFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        lock (syncLock)
        {
            if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            {
                AddEvent($"Not a file name: {fileName}", isError: true);
                return;
            }

            if (TryRun($"Delete {fileName}", () => File.Delete(Path.Combine(folder.FullName, fileName))))
            {
                AddEvent($"Deleted {fileName}");
            }
        }
    }

    public void DeleteAllFiles()
    {
        lock (syncLock)
        {
            foreach (var file in folder.GetFiles())
            {
                TryRun($"Delete {file.Name}", file.Delete);
            }

            currentFileNames.Clear();
            emptyFilesWaitingForFirstLine.Clear();
            AddEvent("Deleted all files");
        }
    }

    public void Dispose()
    {
        timer.Dispose();
        emptyFileTimer.Dispose();
    }

    internal string GetCurrentFileName(string serviceName)
    {
        if (!currentFileNames.TryGetValue(serviceName, out var fileName))
        {
            fileName = $"{serviceName}.log";
            currentFileNames[serviceName] = fileName;
        }

        return fileName;
    }

    private void Tick()
    {
        lock (syncLock)
        {
            if (!isRunning)
            {
                return;
            }

            pendingEntries += settings.LinesPerSecond * TickInterval.TotalSeconds;
            var count = (int)pendingEntries;
            pendingEntries -= count;

            if (count > 0)
            {
                WriteEntries(count);
            }
        }
    }

    private void WriteEntries(int count)
    {
        var linesPerFile = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var serviceName = ServiceNames[serviceIndex % settings.FileCount];
            serviceIndex++;

            if (!linesPerFile.TryGetValue(serviceName, out var sb))
            {
                sb = new StringBuilder();
                linesPerFile[serviceName] = sb;
            }

            AppendEntry(sb, serviceName);
        }

        foreach (var (serviceName, sb) in linesPerFile)
        {
            var fileName = GetCurrentFileName(serviceName);
            if (TryRun($"Write to {fileName}", () => File.AppendAllText(Path.Combine(folder.FullName, fileName), sb.ToString())))
            {
                RollIfFull(serviceName);
            }
        }

        entriesWritten += count;
    }

    private void AppendEntry(
        StringBuilder sb,
        string serviceName)
    {
        var roll = random.Next(100);
        var level = roll < settings.ErrorPercent
            ? LogLevel.Error
            : roll < settings.ErrorPercent + 10
                ? LogLevel.Warning
                : roll < settings.ErrorPercent + 25
                    ? LogLevel.Debug
                    : LogLevel.Information;

        var templates = level switch
        {
            LogLevel.Error => ErrorMessages,
            LogLevel.Warning => WarningMessages,
            _ => InfoMessages,
        };

        var message = string.Format(
            CultureInfo.InvariantCulture,
            templates[random.Next(templates.Length)],
            random.Next(1_000, 99_999),
            random.Next(5, 2_000));

        sb.AppendLine(LogLineFormatter.Format(
            settings.Format,
            timeProvider.GetLocalNow(),
            level,
            $"Contoso.{char.ToUpperInvariant(serviceName[0])}{serviceName[1..]}.Worker",
            Environment.CurrentManagedThreadId,
            message));

        if (level == LogLevel.Error &&
            random.Next(100) < settings.StackTracePercent)
        {
            sb.AppendLine("System.InvalidOperationException: The operation failed");
            sb.AppendLine(CultureInfo.InvariantCulture, $"   at Contoso.{serviceName}.Worker.Process() in /src/Worker.cs:line {random.Next(20, 400)}");
            sb.AppendLine("   at Contoso.Hosting.MessagePump.Run()");
        }
    }

    /// <summary>
    /// Sends every service to a new file, so no file has two formats: the viewer reads one format
    /// per folder, found from the newest file. A real system gets new files too when a deploy
    /// changes the log layout.
    /// </summary>
    private void StartNewFilesForNewFormat()
    {
        var stamp = timeProvider.GetLocalNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var switched = 0;
        foreach (var serviceName in ServiceNames)
        {
            // A service with no file yet starts in the new format anyway
            if (File.Exists(Path.Combine(folder.FullName, GetCurrentFileName(serviceName))))
            {
                currentFileNames[serviceName] = $"{serviceName}-{stamp}.log";
                switched++;
            }
        }

        if (switched > 0)
        {
            AddEvent($"Format changed to {settings.Format}: new entries go to new files (<service>-{stamp}.log). Open the folder again in the viewer to read them.");
        }
    }

    private void RollIfFull(string serviceName)
    {
        if (settings.RollSizeKb <= 0)
        {
            return;
        }

        var file = new FileInfo(Path.Combine(folder.FullName, GetCurrentFileName(serviceName)));
        if (file.Exists &&
            file.Length >= settings.RollSizeKb * 1024L)
        {
            Roll(serviceName);
        }
    }

    private void Roll(string serviceName)
    {
        var fileName = GetCurrentFileName(serviceName);
        var path = Path.Combine(folder.FullName, fileName);
        var stamp = timeProvider.GetLocalNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);

        switch (settings.RollMode)
        {
            case RollMode.Rename:
                if (TryRun($"Roll {fileName} by rename", () => RollByRename(path)))
                {
                    AddEvent($"Rolled {fileName} → {fileName}.1 (older ones shifted)");
                }

                break;

            case RollMode.NewFile:
                currentFileNames[serviceName] = $"{serviceName}-{stamp}.log";
                AddEvent($"Rolled {fileName}: new entries go to {currentFileNames[serviceName]}");
                break;

            case RollMode.CopyTruncate:
                if (TryRun($"Roll {fileName} by copy and truncate", () => RollByCopyTruncate(path, $"{path}.{stamp}")))
                {
                    AddEvent($"Rolled {fileName}: copied to {fileName}.{stamp} and truncated");
                }

                break;

            default:
                throw new InvalidOperationException($"Unknown roll mode {settings.RollMode}");
        }
    }

    private static void RollByRename(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete($"{path}.{MaxRenamedFiles}");
        for (var i = MaxRenamedFiles - 1; i >= 1; i--)
        {
            if (File.Exists($"{path}.{i}"))
            {
                File.Move($"{path}.{i}", $"{path}.{i + 1}");
            }
        }

        File.Move(path, $"{path}.1");
    }

    private static void RollByCopyTruncate(
        string path,
        string copyPath)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.Copy(path, copyPath);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        stream.SetLength(0);
    }

    private void WriteFirstLineToEmptyFiles()
    {
        lock (syncLock)
        {
            foreach (var fileName in emptyFilesWaitingForFirstLine)
            {
                var sb = new StringBuilder();
                AppendEntry(sb, "orders");
                if (TryRun($"Write to {fileName}", () => File.AppendAllText(Path.Combine(folder.FullName, fileName), sb.ToString())))
                {
                    AddEvent($"Wrote the first line to {fileName}");
                }
            }

            emptyFilesWaitingForFirstLine.Clear();
        }
    }

    /// <summary>
    /// Runs a file action and records an error instead of throwing. For example, a file that the viewer
    /// still has open can't be created again under the same name until the viewer lets go of it.
    /// </summary>
    private bool TryRun(
        string what,
        Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (IOException ex)
        {
            AddEvent($"{what} failed: {ex.Message}", isError: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            AddEvent($"{what} failed: {ex.Message}", isError: true);
        }

        return false;
    }

    private void AddEvent(
        string text,
        bool isError = false)
    {
        events.AddFirst(new SimulatorEvent(timeProvider.GetLocalNow(), text, isError));
        while (events.Count > MaxEvents)
        {
            events.RemoveLast();
        }
    }
}