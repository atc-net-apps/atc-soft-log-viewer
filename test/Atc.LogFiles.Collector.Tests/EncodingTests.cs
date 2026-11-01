namespace Atc.LogFiles.Collector.Tests;

/// <summary>
/// Older log4net and NLog setups write the Windows code page, not UTF-8. Danish letters must read right either way.
/// </summary>
public sealed class EncodingTests : IDisposable
{
    private const string Message = "Kunde Søren Ærø åbnede ordre på 1.250 kr. – €";

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("atc-encoding-tests");

    public static TheoryData<string> Encodings
        => new() { "windows-1252", "utf-8", "utf-8 with BOM" };

    [Theory]
    [MemberData(nameof(Encodings))]
    public async Task AFile_IsReadInItsEncoding(string encodingName)
    {
        // Arrange
        var file = new FileInfo(Path.Combine(directory.FullName, "app.log"));
        var line = $"2026-10-30 00:08:20.678 +01:00 [INF] {Message}{Environment.NewLine}";
        await File.WriteAllBytesAsync(file.FullName, Encode(line, encodingName), TestContext.Current.CancellationToken);

        var collector = new SerilogFileCollector(new SerilogFileExtractor());
        var received = new List<string>();
        collector.CollectedEntry += (_, e) => received.Add(e.Entry.MessageFull);

        // Act
        await collector.CollectFile(file, new LogFileCollectorConfiguration(), useMonitoring: false, TestContext.Current.CancellationToken);

        // Assert
        received.Should().Equal(Message);
    }

    public void Dispose()
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp directory
        }
    }

    private static byte[] Encode(
        string text,
        string encodingName)
        => encodingName switch
        {
            "windows-1252" => Windows1252.GetBytes(text),
            "utf-8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text),
            _ => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)],
        };
}