namespace Atc.LogFiles.Collector.Log4Net;

public class Log4NetFileExtractor : ILog4NetFileExtractor
{
    public AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line)
    {
        //// layout = "%date [%thread] %-5level %logger - %message%newline%exception",

        if (line is null ||
            line.Length < 36 ||
            !char.IsDigit(line[0]))
        {
            return null;
        }

        try
        {
            var indexOfThreadStart = line.IndexOf('[', StringComparison.Ordinal) + 1;
            var indexOfThreadEnd = line.IndexOf(']', indexOfThreadStart);

            var dateTimeString = line[..(indexOfThreadStart - 2)];
            var dateTime = DateTime.Parse(dateTimeString, GlobalizationConstants.EnglishCultureInfo);

            var lineAfterThread = line[(indexOfThreadEnd + 2)..];
            var indexOfLogLevelEnd = lineAfterThread.IndexOf(' ', StringComparison.Ordinal);

            var logLevelString = lineAfterThread[..indexOfLogLevelEnd];
            var logLevel = logLevelString switch
            {
                "TRACE" => LogLevel.Trace,
                "DEBUG" => LogLevel.Debug,
                "INFO" => LogLevel.Information,
                "WARN" => LogLevel.Warning,
                "ERROR" => LogLevel.Error,
                "FATAL" => LogLevel.Critical,
                _ => LogLevel.None,
            };

            if (logLevel == LogLevel.None)
            {
                return null;
            }

            var indexOfMessageStart = lineAfterThread.IndexOf('-', StringComparison.Ordinal) + 2;
            if (indexOfMessageStart < 3)
            {
                return null;
            }

            var message = lineAfterThread[indexOfMessageStart..];

            return new AtcLogEntry(
                sourceIdentifier,
                sourceSystem,
                lineNumber,
                dateTime,
                logLevel,
                message,
                message);
        }
        catch
        {
            return null;
        }
    }
}