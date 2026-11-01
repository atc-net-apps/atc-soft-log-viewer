namespace AtcSoft.Log.Viewer.Tests;

public sealed class ChartTimelineModeTests
{
    [Theory]
    [InlineData(ChartTimelineMode.Latest60Minutes, 1, 60, "HH:mm")]
    [InlineData(ChartTimelineMode.Latest12Hours, 60, 12, "H:00")]
    [InlineData(ChartTimelineMode.Latest24Hours, 60, 24, "H:00")]
    [InlineData(ChartTimelineMode.Latest48Hours, 60, 48, "H:00")]
    public void GetBuckets_ReturnsTheBucketsOfTheMode(
        ChartTimelineMode mode,
        int bucketMinutes,
        int bucketCount,
        string labelFormat)
    {
        // Act
        var actual = mode.GetBuckets();

        // Assert
        actual.Should().Be(new TimelineBuckets(TimeSpan.FromMinutes(bucketMinutes), bucketCount, labelFormat));
    }

    [Fact]
    public void GetBuckets_UnknownMode_Throws()
    {
        // Act
        var act = () => ((ChartTimelineMode)99).GetBuckets();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}