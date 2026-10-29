namespace Atc.LogFiles.Collector.Serilog;

public sealed class SerilogFileExtractor : ISerilogFileExtractor
{
    public AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line)
    {
        //// layout = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message}{NewLine}{Exception}",

        if (line is null ||
            line.Length < 38 ||
            !char.IsDigit(line[0]))
        {
            return null;
        }

        try
        {
            var indexOfLogLevelStart = line.IndexOf('[', StringComparison.Ordinal) + 1;
            var indexOfLogLevelEnd = line.IndexOf(']', indexOfLogLevelStart);

            var dateTimeString = line[..(indexOfLogLevelStart - 2)];
            var dateTime = DateTime.Parse(dateTimeString, GlobalizationConstants.EnglishCultureInfo);

            var logLevelString = line[indexOfLogLevelStart..indexOfLogLevelEnd];
            var logLevel = logLevelString switch
            {
                "VRB" => LogLevel.Trace,
                "DBG" => LogLevel.Debug,
                "INF" => LogLevel.Information,
                "WRN" => LogLevel.Warning,
                "ERR" => LogLevel.Error,
                "FTL" => LogLevel.Critical,
                _ => LogLevel.None,
            };

            if (logLevel == LogLevel.None)
            {
                return null;
            }

            var message = line[(indexOfLogLevelEnd + 2)..];

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