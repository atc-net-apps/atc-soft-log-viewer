namespace Atc.LogViewer.SampleGenerator;

/// <summary>
/// The log file formats the generator can write, matching the layouts the log viewer reads.
/// </summary>
public enum LogFormat
{
    /// <summary>
    /// <c>${longdate} ${uppercase:${level}} ${message:withexception=true}</c>.
    /// </summary>
    NLog,

    /// <summary>
    /// <c>%date [%thread] %-5level %logger - %message%newline%exception</c>.
    /// </summary>
    Log4Net,

    /// <summary>
    /// <c>{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message}{NewLine}{Exception}</c>.
    /// </summary>
    Serilog,
}