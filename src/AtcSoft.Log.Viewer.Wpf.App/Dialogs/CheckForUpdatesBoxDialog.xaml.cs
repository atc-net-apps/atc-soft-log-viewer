namespace AtcSoft.Log.Viewer.Wpf.App.Dialogs;

/// <summary>
/// Interaction logic for CheckForUpdatesBoxDialog.
/// </summary>
public partial class CheckForUpdatesBoxDialog
{
    public CheckForUpdatesBoxDialog(
        ICheckForUpdatesBoxDialogViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}