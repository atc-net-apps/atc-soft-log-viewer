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

        VersionTextBlock.Text = Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version!
            .ToString();
    }

    private void OnOk(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}