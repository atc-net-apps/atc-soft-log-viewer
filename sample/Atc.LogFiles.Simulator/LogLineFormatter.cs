namespace Atc.LogFiles.Simulator;

/// <summary>
/// Formats the first line of a log entry in the default file layout of each logging library.
/// </summary>
public static class LogLineFormatter
{
    public static string Format(
        LogFormat format,
        DateTimeOffset timeStamp,
        LogLevel level,
        string loggerName,
        int threadId,
        string message)
        => format switch
        {
            // {Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message}
            LogFormat.Serilog => string.Create(
                CultureInfo.InvariantCulture,
                $"{timeStamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{GetSerilogLevel(level)}] {message}"),

            // ${longdate} ${uppercase:${level}} ${message}
            LogFormat.NLog => string.Create(
                CultureInfo.InvariantCulture,
                $"{timeStamp:yyyy-MM-dd HH:mm:ss.ffff} {GetUpperCaseLevel(level)} {message}"),

            // %date [%thread] %-5level %logger - %message
            LogFormat.Log4Net => string.Create(
                CultureInfo.InvariantCulture,
                $"{timeStamp:yyyy-MM-dd HH:mm:ss,fff} [{threadId}] {GetUpperCaseLevel(level),-5} {loggerName} - {message}"),

            _ => throw new ArgumentOutOfRangeException(nameof(format), format, message: null),
        };

    private static string GetSerilogLevel(LogLevel level)
        => level switch
        {
            LogLevel.Trace => "VRB",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            _ => "FTL",
        };

    private static string GetUpperCaseLevel(LogLevel level)
        => level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => "FATAL",
        };
}