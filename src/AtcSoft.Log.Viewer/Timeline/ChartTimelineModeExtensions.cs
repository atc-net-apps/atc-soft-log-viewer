namespace AtcSoft.Log.Viewer.Timeline;

public static class ChartTimelineModeExtensions
{
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    public static TimelineBuckets GetBuckets(this ChartTimelineMode mode)
        => mode switch
        {
            ChartTimelineMode.Latest60Minutes => new TimelineBuckets(OneMinute, 60, "HH:mm"),
            ChartTimelineMode.Latest12Hours => new TimelineBuckets(OneHour, 12, "H:00"),
            ChartTimelineMode.Latest24Hours => new TimelineBuckets(OneHour, 24, "H:00"),
            ChartTimelineMode.Latest48Hours => new TimelineBuckets(OneHour, 48, "H:00"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, message: null),
        };
}