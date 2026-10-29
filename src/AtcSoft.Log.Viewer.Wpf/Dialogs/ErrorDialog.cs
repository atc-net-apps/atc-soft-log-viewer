namespace AtcSoft.Log.Viewer.Wpf.Dialogs;

/// <summary>
/// Shows an error in the app's own dialog style (Atc.Wpf <see cref="InfoDialogBox"/>), from any thread.
/// </summary>
public static class ErrorDialog
{
    /// <summary>
    /// Shows the error and waits until it is closed.
    /// </summary>
    /// <remarks>
    /// Falls back to a plain message box when the styled dialog cannot be shown, for example
    /// while the app is shutting down, so an error is never lost.
    /// </remarks>
    /// <param name="titleBarText">The dialog's title.</param>
    /// <param name="message">The error.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK - the plain message box is the last resort.")]
    public static void Show(
        string titleBarText,
        string message)
    {
        var application = Application.Current;
        var dispatcher = application?.Dispatcher;
        if (application is null || dispatcher is null || dispatcher.HasShutdownStarted)
        {
            ShowPlain(titleBarText, message);
            return;
        }

        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => Show(titleBarText, message));
            return;
        }

        try
        {
            var owner = application.MainWindow is { IsLoaded: true } mainWindow
                ? mainWindow
                : null;

            var dialogBox = new InfoDialogBox(
                owner!,
                new DialogBoxSettings(DialogBoxType.Ok, LogCategoryType.Error)
                {
                    TitleBarText = titleBarText,
                },
                message);

            dialogBox.ShowDialog();
        }
        catch (Exception)
        {
            ShowPlain(titleBarText, message);
        }
    }

    private static void ShowPlain(
        string titleBarText,
        string message)
        => MessageBox.Show(message, titleBarText, MessageBoxButton.OK, MessageBoxImage.Error);
}