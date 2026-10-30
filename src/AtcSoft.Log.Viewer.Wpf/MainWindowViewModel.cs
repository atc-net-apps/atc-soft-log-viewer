namespace AtcSoft.Log.Viewer.Wpf;

public partial class MainWindowViewModel : MainWindowViewModelBase
{
    /// <summary>
    /// Batches with more entries than this replace the list content in one go;
    /// smaller (live) batches are added one by one to keep the scroll position and selection.
    /// </summary>
    private const int MaxEntriesToAddOneByOne = 1_000;

    private readonly long[] displayedLogLevelCounts = new long[(int)LogLevel.None + 1];
    private readonly List<LogEntryBatch> earlyBatches = [];
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly ILogAnalyzer logAnalyzer;
    private readonly IGitHubReleaseService gitHubReleaseService;
    private string? newVersionIsAvailable;
    private bool isHighlightsFlyoutOpen;
    private long displayedFilterVersion;
    private long displayedSourceSystemsVersion = -1;
    private int filterRequestId;
    private HighlightEditorViewModel? highlightEditorViewModel;
    private BitmapImage? icon;
    private FileInfo? profileFile;
    private bool enableAutoScrollToEnd = true;
    private AtcLogEntryEx? selectedLogEntry;
    private ViewMode viewMode;
    private bool isTraceEnabled = true;
    private bool isDebugEnabled = true;
    private bool isInfoEnabled = true;
    private bool isWarningEnabled = true;
    private bool isErrorEnabled = true;
    private bool isCriticalEnabled = true;
    private string filterText = string.Empty;
    private DateTime? filterDateTimeFrom;
    private DateTime? filterDateTimeTo;
    private string selectedSourceSystemKey = string.Empty;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        ILogAnalyzer logAnalyzer,
        IGitHubReleaseService gitHubReleaseService,
        StatusBarViewModel statusBarViewModel,
        IOptions<BasicApplicationOptions> applicationOptions)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(logAnalyzer);
        ArgumentNullException.ThrowIfNull(gitHubReleaseService);
        ArgumentNullException.ThrowIfNull(statusBarViewModel);
        ArgumentNullException.ThrowIfNull(applicationOptions);

        this.logger = logger;
        this.logAnalyzer = logAnalyzer;
        this.gitHubReleaseService = gitHubReleaseService;

        Icon = WpfApp.DefaultIcon;

        ApplicationOptions = new BasicApplicationSettingsViewModel(applicationOptions.Value)
        {
            ShowLanguage = false,
        };

        ProfileViewModel = new ProfileViewModel();
        ChartLogLevelPieViewModel = new ChartLogLevelPieViewModel();
        ChartTimelineViewModel = new ChartTimelineViewModel(LogEntries);
        StatusBarViewModel = statusBarViewModel;

        LoadRecentOpenFiles();

        displayedFilterVersion = this.logAnalyzer
            .ApplyFilter(CreateLogFilter())
            .FilterVersion;

        this.logAnalyzer.CollectedEntries += OnCollectedEntries;
        this.logAnalyzer.NotReadChanged += OnNotReadChanged;

        if (ApplicationOptions.OpenRecentFileOnStartup &&
            RecentOpenFiles.Count > 0)
        {
            OpenLastUsedProfileCommand.ExecuteAsync(parameter: null);
        }

        if (!UpdateCheck.IsSkipped(Environment.GetEnvironmentVariable(UpdateCheck.SkipVariable)))
        {
            TaskHelper.FireAndForget(CheckForNewVersion());
        }
    }

    /// <summary>
    /// Gets the notice shown above the log when a newer version is released, or <see langword="null"/>.
    /// </summary>
    public string? NewVersionIsAvailable
    {
        get => newVersionIsAvailable;
        private set
        {
            newVersionIsAvailable = value;
            OnPropertyChanged();
        }
    }

    public bool IsHighlightsFlyoutOpen
    {
        get => isHighlightsFlyoutOpen;
        set
        {
            if (isHighlightsFlyoutOpen == value)
            {
                return;
            }

            isHighlightsFlyoutOpen = value;
            OnPropertyChanged();
        }
    }

    public HighlightEditorViewModel? HighlightEditorViewModel
    {
        get => highlightEditorViewModel;
        set
        {
            highlightEditorViewModel = value;
            OnPropertyChanged();
        }
    }

    public BitmapImage? Icon
    {
        get => icon;
        set
        {
            icon = value;
            RaisePropertyChanged();
        }
    }

    public BasicApplicationSettingsViewModel ApplicationOptions { get; set; }

    public ProfileViewModel ProfileViewModel { get; set; }

    public ChartLogLevelPieViewModel ChartLogLevelPieViewModel { get; set; }

    public ChartTimelineViewModel ChartTimelineViewModel { get; set; }

    public StatusBarViewModel StatusBarViewModel { get; set; }

    public ObservableCollectionEx<RecentOpenFileViewModel> RecentOpenFiles { get; } = [];

    public ObservableCollectionEx<AtcLogEntryEx> LogEntries { get; } = [];

    public IDictionary<string, string> SourceSystems { get; private set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string SelectedSourceSystemKey
    {
        get => selectedSourceSystemKey;
        set
        {
            selectedSourceSystemKey = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public AtcLogEntryEx? SelectedLogEntry
    {
        get => selectedLogEntry;
        set
        {
            selectedLogEntry = value;
            RaisePropertyChanged();
        }
    }

    public static CultureInfo DateTimePickerUiCulture
        => GlobalizationConstants.DanishCultureInfo;

    public ViewMode ViewMode
    {
        get => viewMode;
        set
        {
            viewMode = value;
            RaisePropertyChanged();
        }
    }

    public bool EnableAutoScrollToEnd
    {
        get => enableAutoScrollToEnd;
        set
        {
            enableAutoScrollToEnd = value;
            RaisePropertyChanged();
        }
    }

    public bool IsTraceEnabled
    {
        get => isTraceEnabled;
        set
        {
            isTraceEnabled = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public bool IsDebugEnabled
    {
        get => isDebugEnabled;
        set
        {
            isDebugEnabled = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public bool IsInfoEnabled
    {
        get => isInfoEnabled;
        set
        {
            isInfoEnabled = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public bool IsWarningEnabled
    {
        get => isWarningEnabled;
        set
        {
            isWarningEnabled = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public bool IsErrorEnabled
    {
        get => isErrorEnabled;
        set
        {
            isErrorEnabled = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public bool IsCriticalEnabled
    {
        get => isCriticalEnabled;
        set
        {
            isCriticalEnabled = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public string FilterText
    {
        get => filterText;
        set
        {
            filterText = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public DateTime? FilterDateTimeFrom
    {
        get => filterDateTimeFrom;
        set
        {
            filterDateTimeFrom = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public DateTime? FilterDateTimeTo
    {
        get => filterDateTimeTo;
        set
        {
            filterDateTimeTo = value;
            RaisePropertyChanged();
            _ = ApplyFilter();
        }
    }

    public new void OnKeyDown(
        object sender,
        KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnKeyDown(sender, e);

        if (!e.Handled &&
            Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key == Key.N)
        {
            _ = NewProfileCommandHandler()
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (!e.Handled &&
            Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key == Key.P)
        {
            _ = OpenProfileCommandHandler()
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (!e.Handled &&
            Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key == Key.R)
        {
            _ = OpenLastUsedProfileCommandHandler()
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (!e.Handled &&
            Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key == Key.O)
        {
            _ = OpenLogFolderCommandHandler()
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (!e.Handled &&
            Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key == Key.S &&
            CanSaveProfileCommandHandler())
        {
            _ = SaveProfileCommandHandler()
                .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    public void OnDrop(
        object sender,
        DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var data = e.Data.GetData(DataFormats.FileDrop, autoConvert: true);
        if (data is not string[] droppedItems ||
            droppedItems.Length == 0)
        {
            return;
        }

        Task.Run(async () =>
        {
            foreach (var item in droppedItems)
            {
                if (item.Contains('.', StringComparison.Ordinal))
                {
                    var file = new FileInfo(item);
                    await CollectFromFile(file).ConfigureAwait(false);
                }
                else
                {
                    var folder = new DirectoryInfo(item);
                    await CollectFromFolder(folder).ConfigureAwait(false);
                }
            }
        });
    }

    private void OnNotReadChanged(
        object? sender,
        EventArgs e)
        => Application.Current?.Dispatcher.BeginInvoke(() => CountLogEntriesStatsAndSend());

    private void OnCollectedEntries(
        object? sender,
        LogEntryBatchEventArgs e)
        => Application.Current?.Dispatcher.BeginInvoke(() => AddBatch(e.Batch));

    /// <summary>
    /// Adds a batch from the analyzer. Runs on the UI thread.
    /// </summary>
    private void AddBatch(LogEntryBatch batch)
    {
        if (batch.FilterVersion < displayedFilterVersion)
        {
            // Its entries are already in the result of the displayed filter
            return;
        }

        if (batch.FilterVersion > displayedFilterVersion)
        {
            // Belongs to a filter whose result is still being prepared
            earlyBatches.Add(batch);
            return;
        }

        AddEntries(CreateLogEntriesEx(batch.Entries, CreateHighlighting()));
        RefreshSourceSystems();
        CountLogEntriesStatsAndSend();
    }

    /// <summary>
    /// Adds a batch, which is in order, at its time: usually after what is shown, but a slower file's
    /// entries can be older than entries already shown.
    /// </summary>
    private void AddEntries(IReadOnlyList<AtcLogEntryEx> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var isAfterShown = LogEntries.Count == 0 ||
                           LogEntryOrder.Instance.Compare(LogEntries[^1].Entry, entries[0].Entry) <= 0;
        if (isAfterShown &&
            entries.Count > MaxEntriesToAddOneByOne)
        {
            LogEntries.AddRange(entries);
        }
        else if (isAfterShown)
        {
            foreach (var entry in entries)
            {
                LogEntries.Add(entry);
            }
        }
        else if (entries.Count > MaxEntriesToAddOneByOne)
        {
            // Many older entries: one refill is cheaper than many inserts
            var merged = new List<AtcLogEntryEx>(LogEntries.Count + entries.Count);
            merged.AddRange(LogEntries);
            merged.AddRange(entries);
            merged.Sort(static (x, y) => LogEntryOrder.Instance.Compare(x.Entry, y.Entry));
            LogEntries.Clear();
            LogEntries.AddRange(merged);
        }
        else
        {
            foreach (var entry in entries)
            {
                LogEntries.Insert(LogEntryOrder.FindInsertIndex(LogEntries, entry.Entry, static x => x.Entry), entry);
            }
        }

        foreach (var entry in entries)
        {
            displayedLogLevelCounts[(int)entry.LogLevel]++;
        }
    }

    private void RefreshSourceSystems()
    {
        // Live batches come up to 10 times a second; the list is only rebuilt when a new system appeared
        var version = logAnalyzer.SourceSystemsVersion;
        if (version == displayedSourceSystemsVersion)
        {
            return;
        }

        displayedSourceSystemsVersion = version;
        var sourceSystems = logAnalyzer.GetSourceSystems();
        if (sourceSystems.Keys.SequenceEqual(SourceSystems.Keys, StringComparer.Ordinal))
        {
            return;
        }

        SourceSystems = sourceSystems;
        RaisePropertyChanged(nameof(SourceSystems));
    }

    private LogFilter CreateLogFilter()
        => new(
            IsTraceEnabled,
            IsDebugEnabled,
            IsInfoEnabled,
            IsWarningEnabled,
            IsErrorEnabled,
            IsCriticalEnabled,
            FilterText,
            FilterDateTimeFrom,
            FilterDateTimeTo,
            SelectedSourceSystemKey);

    private async Task ApplyFilter()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (!dispatcher.CheckAccess())
        {
            await dispatcher
                .InvokeAsync(ApplyFilter)
                .Task
                .Unwrap()
                .ConfigureAwait(continueOnCapturedContext: false);
            return;
        }

        var requestId = ++filterRequestId;
        IsBusy = true;

        // Set the filter on the UI thread, so filters are applied in the order they are requested
        var result = logAnalyzer.ApplyFilter(CreateLogFilter());
        var highlighting = CreateHighlighting();

        var entries = await Task
            .Run(() => CreateLogEntriesEx(result.Entries, highlighting))
            .ConfigureAwait(continueOnCapturedContext: true);

        if (requestId != filterRequestId)
        {
            // A newer filter has been requested
            return;
        }

        displayedFilterVersion = result.FilterVersion;
        Array.Clear(displayedLogLevelCounts);

        LogEntries.SuppressOnChangedNotification = true;
        LogEntries.Clear();
        AddEntries(entries);
        LogEntries.SuppressOnChangedNotification = false;

        foreach (var batch in earlyBatches)
        {
            if (batch.FilterVersion == displayedFilterVersion)
            {
                AddEntries(CreateLogEntriesEx(batch.Entries, highlighting));
            }
        }

        earlyBatches.RemoveAll(x => x.FilterVersion <= displayedFilterVersion);

        RefreshSourceSystems();
        CountLogEntriesStatsAndSend();

        IsBusy = false;
    }

    private Highlighting CreateHighlighting()
    {
        var highlights = ProfileViewModel.Highlights;
        return new Highlighting(
            new HighlightMatcher(
                highlights
                    .Select(x => new HighlightRule(x.Text, x.IgnoreCasing))
                    .ToList()),
            highlights
                .Select(x => (Foreground: (Brush)x.Foreground, Background: (Brush)x.Background))
                .ToList(),
            ThemeManagerHelper.GetBrushByResourceKey(AtcAppsBrushKeyType.ThemeForeground));
    }

    private static List<AtcLogEntryEx> CreateLogEntriesEx(
        IReadOnlyList<AtcLogEntry> logEntries,
        Highlighting highlighting)
    {
        var result = new List<AtcLogEntryEx>(logEntries.Count);
        foreach (var logEntry in logEntries)
        {
            var foreground = highlighting.DefaultForeground;
            Brush background = Brushes.Transparent;

            var ruleIndex = highlighting.Matcher.FindMatch(logEntry);
            if (ruleIndex != HighlightMatcher.NoMatch)
            {
                (foreground, background) = highlighting.Brushes[ruleIndex];
            }

            result.Add(new AtcLogEntryEx(logEntry, foreground, background));
        }

        return result;
    }

    /// <summary>
    /// The highlight rules of the profile, and the brushes to use for each rule (same order).
    /// </summary>
    private sealed record Highlighting(
        HighlightMatcher Matcher,
        IReadOnlyList<(Brush Foreground, Brush Background)> Brushes,
        Brush DefaultForeground);
}