namespace Atc.LogFiles.Collector;

/// <summary>
/// Reads a log file up to the end of its last complete line, so a line the logger is still writing is left
/// for the tail to read once its line break is written.
/// </summary>
internal sealed class CompleteLinesStream : Stream
{
    private const int ScanBufferSize = 4 * 1024;

    private readonly Stream inner;
    private readonly long length;
    private long position;

    private CompleteLinesStream(
        Stream inner,
        long length)
    {
        this.inner = inner;
        this.length = length;
    }

    /// <summary>
    /// Gets the byte position after the last line break, which is where reading stops.
    /// </summary>
    public long EndOffset => length;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Wraps a stream positioned at its start, and ends it after its last line break.
    /// </summary>
    /// <param name="inner">A readable, seekable stream at position 0.</param>
    /// <returns>The stream; empty when the stream has no line break.</returns>
    public static CompleteLinesStream Create(Stream inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        var end = FindEndOfLastCompleteLine(inner);
        inner.Seek(0, SeekOrigin.Begin);
        return new CompleteLinesStream(inner, end);
    }

    public override int Read(
        byte[] buffer,
        int offset,
        int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var bytesRead = inner.Read(buffer[..Limit(buffer.Length)]);
        position += bytesRead;
        return bytesRead;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var bytesRead = await inner.ReadAsync(buffer[..Limit(buffer.Length)], cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        position += bytesRead;
        return bytesRead;
    }

    public override void Flush()
    {
        // Read only
    }

    public override long Seek(
        long offset,
        SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(
        byte[] buffer,
        int offset,
        int count)
        => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private static long FindEndOfLastCompleteLine(Stream stream)
    {
        var buffer = new byte[ScanBufferSize];
        var end = stream.Length;
        while (end > 0)
        {
            var start = System.Math.Max(0, end - ScanBufferSize);
            var count = (int)(end - start);
            stream.Seek(start, SeekOrigin.Begin);
            stream.ReadExactly(buffer, 0, count);

            var index = buffer.AsSpan(0, count).LastIndexOf((byte)'\n');
            if (index >= 0)
            {
                return start + index + 1;
            }

            end = start;
        }

        return 0;
    }

    private int Limit(int count)
        => (int)System.Math.Min(count, length - position);
}