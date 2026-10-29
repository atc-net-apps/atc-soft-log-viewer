namespace AtcSoft.Log.Viewer.Wpf;

/// <summary>
/// Shared settings for the WPF front end.
/// </summary>
public static class WpfApp
{
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "OK.")]
    public static readonly BitmapImage DefaultIcon = new(new Uri("pack://application:,,,/AtcSoft.Log.Viewer.Wpf;component/Resources/AppIcon.ico", UriKind.Absolute));

    /// <summary>
    /// Gets the per-user data folder: profiles, recent files and custom application settings.
    /// </summary>
    public static AppDataPaths DataPaths { get; } = AppDataPaths.ForCurrentUser();

    public static JsonSerializerOptions JsonSerializerOptions
    {
        get
        {
            var jsonSerializerOptions = JsonSerializerOptionsFactory.Create();
            jsonSerializerOptions.PropertyNamingPolicy = null;
            jsonSerializerOptions.Converters.Add(new SolidColorBrushToHexJsonConverter());
            return jsonSerializerOptions;
        }
    }
}