namespace AtcSoft.Log.Viewer.Wpf.UserControls;

public class StatusBarViewModel : ViewModelBase
{
    public StatusBarViewModel()
    {
        Messenger.Default.Register<LogEntriesStatsMessage>(this, OnLogEntriesStatsMessageHandler);
        Messenger.Default.Register<NotReadMessage>(this, OnNotReadMessageHandler);
    }

    public LogEntriesStatsMessage? LogEntriesStats { get; set; }

    /// <summary>
    /// Gets the warning about lines and files that were not read, for example "Not read: 5,810 lines".
    /// </summary>
    public string NotReadText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the files and example lines that were not read, for the tooltip.
    /// </summary>
    public string NotReadDetails { get; private set; } = string.Empty;

    private void OnLogEntriesStatsMessageHandler(LogEntriesStatsMessage obj)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntriesStats = obj;

            RaisePropertyChanged(nameof(LogEntriesStats));
        });
    }

    private void OnNotReadMessageHandler(NotReadMessage obj)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (string.Equals(NotReadText, obj.ShortText, StringComparison.Ordinal) &&
                string.Equals(NotReadDetails, obj.Details, StringComparison.Ordinal))
            {
                return;
            }

            NotReadText = obj.ShortText;
            NotReadDetails = obj.Details;
            RaisePropertyChanged(nameof(NotReadText));
            RaisePropertyChanged(nameof(NotReadDetails));
        });
    }
}