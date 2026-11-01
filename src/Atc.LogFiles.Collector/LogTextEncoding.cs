namespace Atc.LogFiles.Collector;

/// <summary>
/// The text encoding of a log file. Most loggers write UTF-8, but older log4net and NLog setups write the
/// Windows code page (Windows-1252), where æ, ø, å and € are single bytes that aren't valid UTF-8.
/// </summary>
internal static class LogTextEncoding
{
    private const int SampleSize = 64 * 1024;

    static LogTextEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1252 = Encoding.GetEncoding(1252);
    }

    public static Encoding Windows1252 { get; }

    /// <summary>
    /// Finds the encoding of a file from its start: UTF-8 when it has a byte order mark or its bytes are valid
    /// UTF-8, and Windows-1252 otherwise.
    /// </summary>
    /// <param name="stream">The file, at its start. The position is put back.</param>
    /// <returns>The encoding to read the file with.</returns>
    public static Encoding Detect(Stream stream)
    {
        var start = stream.Position;
        var sample = new byte[SampleSize];
        var count = 0;
        int read;
        while (count < sample.Length &&
               (read = stream.Read(sample, count, sample.Length - count)) > 0)
        {
            count += read;
        }

        stream.Position = start;

        var bytes = sample.AsSpan(0, count);
        if (count == SampleSize)
        {
            // The sample may end inside a character; judge only its complete lines
            var lastLineBreak = bytes.LastIndexOf((byte)'\n');
            if (lastLineBreak >= 0)
            {
                bytes = bytes[..(lastLineBreak + 1)];
            }
        }

        // A byte order mark (UTF-8 or UTF-16) is honored by the reader
        return bytes.StartsWith(Encoding.UTF8.Preamble) ||
               bytes.StartsWith(Encoding.Unicode.Preamble) ||
               bytes.StartsWith(Encoding.BigEndianUnicode.Preamble) ||
               System.Text.Unicode.Utf8.IsValid(bytes)
            ? Encoding.UTF8
            : Windows1252;
    }

    /// <summary>
    /// Decodes one line: as UTF-8 when it is valid UTF-8, and as Windows-1252 otherwise.
    /// </summary>
    /// <param name="lineBytes">The line, without its line break.</param>
    /// <returns>The text.</returns>
    public static string DecodeLine(ReadOnlySpan<byte> lineBytes)
        => System.Text.Unicode.Utf8.IsValid(lineBytes)
            ? Encoding.UTF8.GetString(lineBytes)
            : Windows1252.GetString(lineBytes);
}