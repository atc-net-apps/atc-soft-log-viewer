// ReSharper disable SuggestBaseTypeForParameter
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
namespace AtcSoft.Log.Viewer.Wpf;

[SuppressMessage("Design", "MA0048:File name must match type name", Justification = "OK - partial class")]
[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1601:Partial elements should be documented", Justification = "OK - partial class")]
[SuppressMessage("AsyncUsage", "AsyncFixer03:Fire-and-forget async-void methods or delegates", Justification = "OK.")]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "OK.")]
public partial class MainWindowViewModel
{
    private void LoadRecentOpenFiles()
    {
        var recentOpenFileViewModels = RecentOpenFileHelper.Load(WpfApp.DataPaths.Root);

        RecentOpenFiles.Clear();
        RecentOpenFiles.SuppressOnChangedNotification = true;
        RecentOpenFiles.AddRange(recentOpenFileViewModels);
        RecentOpenFiles.SuppressOnChangedNotification = false;
    }

    private void AddLoadedProfileFileToRecentOpenFiles(FileInfo file)
    {
        RecentOpenFiles.Add(new RecentOpenFileViewModel(WpfApp.DataPaths.Root, DateTime.Now, file.FullName));

        var recentOpenFilesOption = new RecentOpenFilesOption();
        foreach (var vm in RecentOpenFiles.OrderByDescending(x => x.TimeStamp))
        {
            var item = new RecentOpenFileOption
            {
                TimeStamp = vm.TimeStamp,
                FilePath = vm.File,
            };

            if (recentOpenFilesOption.RecentOpenFiles.FirstOrDefault(x => x.FilePath == item.FilePath) is not null)
            {
                continue;
            }

            if (!File.Exists(item.FilePath))
            {
                continue;
            }

            recentOpenFilesOption.RecentOpenFiles.Add(item);
        }

        var recentOpenFilesFilePath = Path.Combine(WpfApp.DataPaths.Root.FullName, AtcFileNameConstants.RecentOpenFilesFileName);
        if (!Directory.Exists(WpfApp.DataPaths.ProfilesDirectory.FullName))
        {
            Directory.CreateDirectory(WpfApp.DataPaths.Root.FullName);
        }

        var json = JsonSerializer.Serialize(recentOpenFilesOption, WpfApp.JsonSerializerOptions);
        File.WriteAllText(recentOpenFilesFilePath, json);

        LoadRecentOpenFiles();
    }

    private async Task LoadProfileFile(
        FileInfo file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.Log(LogLevel.Trace, $"Loading profile file: {file.FullName}");

            var profileOptions = await ProfileFile
                .LoadAsync(file, cancellationToken)
                .ConfigureAwait(true);

            profileFile = file;
            ProfileViewModel = profileOptions.ToViewModel();

            AddLoadedProfileFileToRecentOpenFiles(file);

            var logDirectory = new DirectoryInfo(ProfileViewModel.LogFolder);
            if (logDirectory.Exists)
            {
                await ClearDataAndCollectFromFolder(
                        logDirectory,
                        cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile file: {ProfileFile}, Error: {ErrorMessage}", file.FullName, ex.Message);
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK);
        }
    }

    private async Task SaveProfileFile(
        FileInfo file,
        ProfileViewModel profile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.Log(LogLevel.Trace, $"Saving profile file: {file.FullName}");

            await ProfileFile
                .SaveAsync(file, profile.ToOptions(), cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile file: {ProfileFile}, Error: {ErrorMessage}", file.FullName, ex.Message);
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK);
        }
    }

    private async Task CollectFromFile(FileInfo file)
    {
        IsBusy = true;

        var logFileCollectorType = logAnalyzer.DetermineCollectorType(file);
        if (logFileCollectorType == LogFileCollectorType.None)
        {
            IsBusy = false;

            var dialogBox = new InfoDialogBox(
                Application.Current.MainWindow!,
                new DialogBoxSettings(DialogBoxType.Ok, LogCategoryType.Warning)
                {
                    TitleBarText = "Invalid log file",
                },
                "This is not a valid log file");

            dialogBox.ShowDialog();
            return;
        }

        ProfileViewModel.CollectorType = logFileCollectorType;

        await logAnalyzer
            .CollectFile(
                ProfileViewModel.CollectorType,
                file,
                ProfileViewModel.CollectorConfiguration,
                ProfileViewModel.CollectorConfiguration.MonitorFiles,
                CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        IsBusy = false;
    }

    private async Task CollectFromFolder(DirectoryInfo directory)
    {
        var files = directory.GetFiles();
        foreach (var file in files)
        {
            await CollectFromFile(file).ConfigureAwait(false);
        }
    }

    private async Task ClearDataAndCollectFromFolder(
        DirectoryInfo directory,
        CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        var logFileCollectorType = logAnalyzer.DetermineCollectorType(directory);
        if (logFileCollectorType == LogFileCollectorType.None)
        {
            IsBusy = false;

            var dialogBox = new InfoDialogBox(
                Application.Current.MainWindow!,
                new DialogBoxSettings(DialogBoxType.Ok, LogCategoryType.Warning)
                {
                    TitleBarText = "Invalid log folder",
                },
                "There is no valid log files in this folder");

            dialogBox.ShowDialog();
            return;
        }

        logAnalyzer.StopMonitoringAndClearLogEntries();
        await ApplyFilter()
            .ConfigureAwait(continueOnCapturedContext: false);

        await logAnalyzer
            .CollectFolder(
                ProfileViewModel.CollectorType,
                directory,
                ProfileViewModel.CollectorConfiguration,
                ProfileViewModel.CollectorConfiguration.MonitorFiles,
                cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        IsBusy = false;
    }

    private void CountLogEntriesStatsAndSend()
    {
        var logStatistics = logAnalyzer.GetLogStatistics();

        Messenger.Default.Send(
            new LogEntriesStatsMessage(
                Count: logStatistics.Count,
                TotalCount: LogEntries.Count,
                CriticalCount: displayedLogLevelCounts[(int)LogLevel.Critical],
                TotalCriticalCount: logStatistics.CriticalCount,
                ErrorCount: displayedLogLevelCounts[(int)LogLevel.Error],
                TotalErrorCount: logStatistics.ErrorCount,
                WarningCount: displayedLogLevelCounts[(int)LogLevel.Warning],
                TotalWarningCount: logStatistics.WarningCount,
                InformationCount: displayedLogLevelCounts[(int)LogLevel.Information],
                TotalInformationCount: logStatistics.InformationCount,
                DebugCount: displayedLogLevelCounts[(int)LogLevel.Debug],
                TotalDebugCount: logStatistics.DebugCount,
                TraceCount: displayedLogLevelCounts[(int)LogLevel.Trace],
                TotalTraceCount: logStatistics.TraceCount));
    }
}