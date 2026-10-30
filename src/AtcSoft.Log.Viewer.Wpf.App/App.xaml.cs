// ReSharper disable NotAccessedField.Local
namespace AtcSoft.Log.Viewer.Wpf.App;

/// <summary>
/// Interaction logic for App.
/// </summary>
public partial class App
{
    private readonly ILogger<App> logger;
    private readonly IHost host;
    private IConfiguration? configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App()
    {
        var legacyDataCopyError = CopyLegacyDataIfNeeded();
        RestoreInstallerCustomAppSettingsIfNeeded();

        host = Host.CreateDefaultBuilder()
            .ConfigureLogging((_, logging) =>
            {
                logging
                    .ClearProviders()
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddDebug();
            })
            .ConfigureAppConfiguration(
                configurationBuilder =>
                {
                    configuration = configurationBuilder
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile(AtcFileNameConstants.AppSettings, optional: false, reloadOnChange: true)
                        .AddJsonFile(AtcFileNameConstants.AppSettingsCustom, optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();
                })
            .ConfigureServices((_, services) =>
            {
                services
                    .AddOptions<BasicApplicationOptions>()
                    .Bind(configuration!.GetRequiredSection(BasicApplicationOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddSingleton<ILog4NetFileExtractor, Log4NetFileExtractor>();
                services.AddSingleton<ILog4NetFileCollector, Log4NetFileCollector>();

                services.AddSingleton<INLogFileExtractor, NLogFileExtractor>();
                services.AddSingleton<INLogFileCollector, NLogFileCollector>();

                services.AddSingleton<ISerilogFileExtractor, SerilogFileExtractor>();
                services.AddSingleton<ISerilogFileCollector, SerilogFileCollector>();

                services.AddSingleton<ILogAnalyzer, Atc.LogFiles.Analyzer.LogAnalyzer>();
                services.AddSingleton<StatusBarViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        logger = host.Services.GetService<ILoggerFactory>()!.CreateLogger<App>();

        if (legacyDataCopyError is not null)
        {
            logger.LogError(legacyDataCopyError, "Could not copy the data of an earlier version from {LegacyRoot}", AppDataPaths.LegacyRoot.FullName);
        }
    }

    /// <summary>
    /// Raises the Startup event.
    /// </summary>
    /// <param name="e">The <see cref="StartupEventArgs"/> instance containing the event data.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        logger.LogInformation("App initializing");

        // Hook on error before app really starts
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
        Current.DispatcherUnhandledException += ApplicationOnDispatcherUnhandledException;
        base.OnStartup(e);

        if (Debugger.IsAttached)
        {
            BindingErrorTraceListener.StartTrace();
        }
    }

    /// <summary>
    /// Currents the domain unhandled exception.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="UnhandledExceptionEventArgs"/> instance containing the event data.</param>
    private void CurrentDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e is not { ExceptionObject: Exception ex })
        {
            return;
        }

        var exceptionMessage = ex.GetMessage(includeInnerMessage: true);

        logger.LogError($"CurrentDomain Unhandled Exception: {exceptionMessage}");

        ErrorDialog.Show("CurrentDomain Unhandled Exception", exceptionMessage);
    }

    /// <summary>
    /// Applications the on dispatcher unhandled exception.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="DispatcherUnhandledExceptionEventArgs"/> instance containing the event data.</param>
    private void ApplicationOnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        var exceptionMessage = e.Exception.GetMessage(includeInnerMessage: true);
        if (exceptionMessage.Contains(
                "BindingExpression:Path=HorizontalContentAlignment; DataItem=null; target element is 'ComboBoxItem'",
                StringComparison.Ordinal))
        {
            e.Handled = true;
            return;
        }

        logger.LogError($"Dispatcher Unhandled Exception: {exceptionMessage}");

        ErrorDialog.Show("Dispatcher Unhandled Exception", exceptionMessage);

        e.Handled = true;
        Shutdown(-1);
    }

    private async void ApplicationStartup(
        object sender,
        StartupEventArgs e)
    {
        logger.LogInformation("App starting");

        await host
            .StartAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

        var applicationOptions = new BasicApplicationOptions();
        configuration!
            .GetRequiredSection(BasicApplicationOptions.SectionName)
            .Bind(applicationOptions);

        CultureManager.SetCultures("en-US");

        ThemeManagerHelper.SetThemeAndAccent(Current, applicationOptions.Theme);

        Current.MainWindow = host
            .Services
            .GetService<MainWindow>()!;

        Current.MainWindow.Show();

        logger.LogInformation("App started");
    }

    private async void ApplicationExit(
        object sender,
        ExitEventArgs e)
    {
        logger.LogInformation("App closing");

        await host
            .StopAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

        host.Dispose();

        logger.LogInformation("App closed");
    }

    /// <summary>
    /// Copies the data of earlier versions from ProgramData to the per-user folder once, and creates the folders.
    /// </summary>
    /// <returns>The error when the copy or creating the folders failed; it is logged and the app starts anyway.</returns>
    private static Exception? CopyLegacyDataIfNeeded()
    {
        Exception? error = null;
        try
        {
            if (!WpfApp.DataPaths.IsOverridden)
            {
                LegacyDataMigrator.CopyIfNeeded(AppDataPaths.LegacyRoot, WpfApp.DataPaths.Root);
            }

            WpfApp.DataPaths.EnsureCreated();
        }
        catch (IOException ex)
        {
            error = ex;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = ex;
        }

        return error;
    }

    private static void RestoreInstallerCustomAppSettingsIfNeeded()
    {
        var currentFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AtcFileNameConstants.AppSettingsCustom));
        var backupFile = WpfApp.DataPaths.CustomAppSettingsFile;
        if (!currentFile.Exists ||
            !backupFile.Exists ||
            currentFile.LastWriteTime == backupFile.LastWriteTime)
        {
            return;
        }

        File.Copy(backupFile.FullName, currentFile.FullName, overwrite: true);
    }
}