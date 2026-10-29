namespace Atc.LogAnalyzer.Tests;

public sealed class LogTimelineTests
{
    private static readonly DateTime Now = new(2024, 2, 16, 10, 37, 12, DateTimeKind.Unspecified);
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    [Fact]
    public void Create_BucketStarts_EndWithTheBucketThatHoldsNow()
    {
        // Act
        var actual = LogTimeline.Create([], Now, OneHour, bucketCount: 3);

        // Assert
        actual.BucketStarts.Should().Equal(
            new DateTime(2024, 2, 16, 8, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2024, 2, 16, 9, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2024, 2, 16, 10, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void Create_CountsEntriesPerLogLevelAndBucket()
    {
        // Arrange
        var entries = new[]
        {
            CreateEntry(At(8, 5), LogLevel.Error),
            CreateEntry(At(10, 1), LogLevel.Error),
            CreateEntry(At(10, 30), LogLevel.Error),
            CreateEntry(At(10, 31), LogLevel.Information),
        };

        // Act
        var actual = LogTimeline.Create(entries, Now, OneHour, bucketCount: 3);

        // Assert
        actual.GetCounts(LogLevel.Error).Should().Equal(1, 0, 2);
        actual.GetCounts(LogLevel.Information).Should().Equal(0, 0, 1);
        actual.GetCounts(LogLevel.Warning).Should().Equal(0, 0, 0);
    }

    [Fact]
    public void Create_SameHourOnTwoDays_LandsInTwoBuckets()
    {
        // Arrange
        var entries = new[]
        {
            CreateEntry(At(9, 10).AddDays(-1), LogLevel.Error),
            CreateEntry(At(9, 10), LogLevel.Error),
        };

        // Act
        var actual = LogTimeline.Create(entries, Now, OneHour, bucketCount: 48);

        // Assert
        var counts = actual.GetCounts(LogLevel.Error);
        counts[^2].Should().Be(1);
        counts[^26].Should().Be(1);
        counts.Sum().Should().Be(2);
    }

    [Fact]
    public void Create_EntryBeforeTheFirstBucket_IsNotCounted()
    {
        // Arrange - 24 hours before now is in the same hour of the day as now, but before the first bucket
        var entries = new[]
        {
            CreateEntry(Now.AddHours(-24).AddMinutes(1), LogLevel.Error),
        };

        // Act
        var actual = LogTimeline.Create(entries, Now, OneHour, bucketCount: 24);

        // Assert
        actual.GetCounts(LogLevel.Error).Sum().Should().Be(0);
    }

    [Fact]
    public void Create_EntryAfterTheLastBucket_IsNotCounted()
    {
        // Arrange
        var entries = new[]
        {
            CreateEntry(At(11, 0), LogLevel.Error),
        };

        // Act
        var actual = LogTimeline.Create(entries, Now, OneHour, bucketCount: 24);

        // Assert
        actual.GetCounts(LogLevel.Error).Sum().Should().Be(0);
    }

    [Fact]
    public void Create_MinuteBuckets_CountTheCurrentMinute()
    {
        // Arrange
        var entries = new[]
        {
            CreateEntry(Now.AddSeconds(30), LogLevel.Warning),
            CreateEntry(Now.AddMinutes(-59), LogLevel.Warning),
        };

        // Act
        var actual = LogTimeline.Create(entries, Now, TimeSpan.FromMinutes(1), bucketCount: 60);

        // Assert
        var counts = actual.GetCounts(LogLevel.Warning);
        counts[0].Should().Be(1);
        counts[^1].Should().Be(1);
        actual.BucketStarts[0].Should().Be(new DateTime(2024, 2, 16, 9, 38, 0, DateTimeKind.Unspecified));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithoutBuckets_Throws(int bucketCount)
    {
        // Act
        var act = () => LogTimeline.Create([], Now, OneHour, bucketCount);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithoutBucketSize_Throws()
    {
        // Act
        var act = () => LogTimeline.Create([], Now, TimeSpan.Zero, bucketCount: 24);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static DateTime At(
        int hour,
        int minute)
        => new(2024, 2, 16, hour, minute, 0, DateTimeKind.Unspecified);

    private static AtcLogEntry CreateEntry(
        DateTime timeStamp,
        LogLevel logLevel)
        => new(
            SourceIdentifier: "app.log",
            SourceSystem: "App",
            LineNumber: 1,
            TimeStamp: timeStamp,
            LogLevel: logLevel,
            MessageShort: "Message",
            MessageFull: "Message");
}