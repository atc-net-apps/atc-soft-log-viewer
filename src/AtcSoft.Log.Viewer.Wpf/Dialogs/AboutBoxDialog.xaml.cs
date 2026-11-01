namespace AtcSoft.Log.Viewer.Wpf.Dialogs;

/// <summary>
/// Interaction logic for AboutBoxDialog.xaml
/// </summary>
public partial class AboutBoxDialog
{
    public AboutBoxDialog()
    {
        InitializeComponent();

        IconImage.Source = WpfApp.DefaultIcon;

        // The version of the exe, not of this library
        VersionTextBlock.Text = Assembly
            .GetEntryAssembly()?
            .GetName()
            .Version?
            .ToString() ?? string.Empty;
    }

    private void OnOk(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}