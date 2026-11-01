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

        //// longdate is "yyyy-MM-dd HH:mm:ss.ffff" (24 characters), then one space and the level. Checking the
        //// layout exactly keeps lines of other formats out: log4net's "yyyy-MM-dd HH:mm:ss,fff [thread] LEVEL"
        //// would otherwise pass, with the thread skipped.
        if (line is null ||
            line.Length < 27 ||
            !char.IsDigit(line[0]) ||
            line[19] != '.' ||
            line[24] != ' ' ||
            !DateTime.TryParseExact(
                line.AsSpan(0, 24),
                "yyyy-MM-dd HH:mm:ss.ffff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime))
        {
            return null;
        }

        try
        {
            const int indexOfLogLevelStart = 25;
            var indexOfLogLevelEnd = line.IndexOf(' ', indexOfLogLevelStart);
            if (indexOfLogLevelEnd < 0)
            {
                return null;
            }

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