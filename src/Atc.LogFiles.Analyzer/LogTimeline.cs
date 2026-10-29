namespace Atc.LogFiles.Analyzer;

/// <summary>
/// Log entry counts per log level in time buckets of equal size, for a timeline chart.
/// </summary>
/// <remarks>
/// The last bucket holds "now". Buckets start on whole multiples of the bucket size,
/// so one-hour buckets start on the hour and one-minute buckets on the minute.
/// </remarks>
public sealed class LogTimeline
{
    private readonly long[][] countsPerLogLevel;

    private LogTimeline(
        DateTime[] bucketStarts,
        TimeSpan bucketSize,
        long[][] countsPerLogLevel)
    {
        BucketStarts = bucketStarts;
        BucketSize = bucketSize;
        this.countsPerLogLevel = countsPerLogLevel;
    }

    /// <summary>
    /// Gets the start time of each bucket, oldest first.
    /// </summary>
    public IReadOnlyList<DateTime> BucketStarts { get; }

    /// <summary>
    /// Gets the size of each bucket.
    /// </summary>
    public TimeSpan BucketSize { get; }

    /// <summary>
    /// Counts the entries per log level in <paramref name="bucketCount"/> buckets that end with the bucket holding <paramref name="now"/>.
    /// Entries outside the buckets are not counted.
    /// </summary>
    /// <param name="entries">The log entries.</param>
    /// <param name="now">The current time, in the same time zone as the time stamps of the entries.</param>
    /// <param name="bucketSize">The size of a bucket, for example one hour.</param>
    /// <param name="bucketCount">The number of buckets, for example 24.</param>
    /// <returns>The timeline.</returns>
    public static LogTimeline Create(
        IEnumerable<AtcLogEntry> entries,
        DateTime now,
        TimeSpan bucketSize,
        int bucketCount)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bucketSize, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bucketCount, 0);

        var sizeTicks = bucketSize.Ticks;
        var lastStartTicks = now.Ticks - (now.Ticks % sizeTicks);
        var firstStartTicks = lastStartTicks - ((bucketCount - 1) * sizeTicks);
        var endTicks = lastStartTicks + sizeTicks;

        var bucketStarts = new DateTime[bucketCount];
        for (var i = 0; i < bucketCount; i++)
        {
            bucketStarts[i] = new DateTime(firstStartTicks + (i * sizeTicks), now.Kind);
        }

        var counts = new long[(int)LogLevel.None + 1][];
        for (var i = 0; i < counts.Length; i++)
        {
            counts[i] = new long[bucketCount];
        }

        foreach (var entry in entries)
        {
            var ticks = entry.TimeStamp.Ticks;
            if (ticks < firstStartTicks ||
                ticks >= endTicks ||
                entry.LogLevel is < LogLevel.Trace or > LogLevel.None)
            {
                continue;
            }

            var bucketIndex = (int)((ticks - firstStartTicks) / sizeTicks);
            counts[(int)entry.LogLevel][bucketIndex]++;
        }

        return new LogTimeline(bucketStarts, bucketSize, counts);
    }

    /// <summary>
    /// Gets the number of entries with the log level in each bucket, oldest first.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <returns>One count per bucket.</returns>
    public IReadOnlyList<long> GetCounts(LogLevel logLevel)
    {
        if (logLevel is < LogLevel.Trace or > LogLevel.None)
        {
            throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, message: null);
        }

        return countsPerLogLevel[(int)logLevel];
    }
}