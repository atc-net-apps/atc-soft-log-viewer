namespace AtcSoft.Log.Viewer.Timeline;

/// <summary>
/// The buckets of a timeline chart.
/// </summary>
/// <param name="BucketSize">The size of a bucket.</param>
/// <param name="BucketCount">The number of buckets.</param>
/// <param name="LabelFormat">The date and time format for the label of a bucket.</param>
public sealed record TimelineBuckets(
    TimeSpan BucketSize,
    int BucketCount,
    string LabelFormat);