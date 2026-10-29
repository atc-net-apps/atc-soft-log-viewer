namespace Atc.LogViewer.SampleGenerator;

/// <summary>
/// Writes the first line of an entry (time stamp, level and message) in a log format.
/// </summary>
/// <remarks>
/// Time stamps are formatted by hand, since <see cref="DateTime.ToString(string)"/> is too slow for files
/// with hundreds of millions of lines.
/// </remarks>
internal static class RootLineWriter
{
    public static void Append(
        StringBuilder sb,
        LogFormat format,
        DateTime timeStamp,
        SampleLevel level,
        string loggerName,
        int threadId)
    {
        switch (format)
        {
            case LogFormat.NLog:
                // 2024-03-26 09:15:01.1231 INFO message
                AppendDateTime(sb, timeStamp, '.', fractionDigits: 4);
                sb.Append(' ').Append(GetUpperCaseLevel(level)).Append(' ');
                break;
            case LogFormat.Log4Net:
                // 2024-03-26 09:15:01,123 [8] INFO  Contoso.Orders.OrderService - message
                AppendDateTime(sb, timeStamp, ',', fractionDigits: 3);
                sb.Append(" [").Append(threadId).Append("] ");
                sb.Append(GetUpperCaseLevel(level).PadRight(5)).Append(' ');
                sb.Append(loggerName).Append(" - ");
                break;
            case LogFormat.Serilog:
                // 2024-03-26 09:15:01.123 +01:00 [INF] message
                AppendDateTime(sb, timeStamp, '.', fractionDigits: 3);
                sb.Append(" +01:00 [").Append(GetSerilogLevel(level)).Append("] ");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, message: null);
        }
    }

    private static string GetUpperCaseLevel(SampleLevel level)
        => level switch
        {
            SampleLevel.Trace => "TRACE",
            SampleLevel.Debug => "DEBUG",
            SampleLevel.Information => "INFO",
            SampleLevel.Warning => "WARN",
            SampleLevel.Error => "ERROR",
            _ => "FATAL",
        };

    private static string GetSerilogLevel(SampleLevel level)
        => level switch
        {
            SampleLevel.Trace => "VRB",
            SampleLevel.Debug => "DBG",
            SampleLevel.Information => "INF",
            SampleLevel.Warning => "WRN",
            SampleLevel.Error => "ERR",
            _ => "FTL",
        };

    private static void AppendDateTime(
        StringBuilder sb,
        DateTime value,
        char fractionSeparator,
        int fractionDigits)
    {
        // yyyy-MM-dd HH:mm:ss<separator><fraction>
        Span<char> buffer = stackalloc char[24];
        WriteDigits(buffer, 0, value.Year, 4);
        buffer[4] = '-';
        WriteDigits(buffer, 5, value.Month, 2);
        buffer[7] = '-';
        WriteDigits(buffer, 8, value.Day, 2);
        buffer[10] = ' ';
        WriteDigits(buffer, 11, value.Hour, 2);
        buffer[13] = ':';
        WriteDigits(buffer, 14, value.Minute, 2);
        buffer[16] = ':';
        WriteDigits(buffer, 17, value.Second, 2);
        buffer[19] = fractionSeparator;

        var fraction = (int)((value.Ticks % TimeSpan.TicksPerSecond) / (fractionDigits == 4 ? 1_000 : 10_000));
        WriteDigits(buffer, 20, fraction, fractionDigits);

        sb.Append(buffer[..(20 + fractionDigits)]);
    }

    private static void WriteDigits(
        Span<char> target,
        int start,
        int number,
        int digits)
    {
        for (var i = start + digits - 1; i >= start; i--)
        {
            target[i] = (char)('0' + (number % 10));
            number /= 10;
        }
    }
}