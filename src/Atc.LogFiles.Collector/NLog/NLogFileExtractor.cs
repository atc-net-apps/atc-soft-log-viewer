// ReSharper disable CommentTypo
namespace Atc.LogFiles.Collector.NLog;

public sealed class NLogFileExtractor : INLogFileExtractor
{
    public AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line)
    {
        //// layout = "${longdate} ${uppercase:${level}} ${message:withexception=true}",

        if (line is null ||
            line.Length < 25 ||
            !char.IsDigit(line[0]))
        {
            return null;
        }

        try
        {
            var dateTimeString = line[..23];
            var dateTime = DateTime.Parse(dateTimeString, GlobalizationConstants.EnglishCultureInfo);

            var indexOfLogLevelStart = line.IndexOf(' ', 24) + 1;
            var indexOfLogLevelEnd = line.IndexOf(' ', indexOfLogLevelStart);

            var logLevelString = line[indexOfLogLevelStart..indexOfLogLevelEnd];
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

            var message = line[(indexOfLogLevelEnd + 1)..];

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