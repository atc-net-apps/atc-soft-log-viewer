namespace AtcSoft.Log.Viewer.Wpf.App.Models;

public class ChartTimelineViewModel : ViewModelBase
{
    private static readonly LogLevel[] LogLevels = LogLevelFactory.CreateList();
    private static readonly Dictionary<LogLevel, SKColor> LogLevelColors = LogLevelFactory.CreateSkColors();
    private readonly ObservableCollectionEx<AtcLogEntryEx> logEntries;
    private IDictionary<string, string> intervals = new Dictionary<string, string>(StringComparer.Ordinal);
    private string selectedInterval = string.Empty;
    private ISeries<long>[] series = [];
    private Axis[] xAxis = [];

    public ChartTimelineViewModel(
        ObservableCollectionEx<AtcLogEntryEx> logEntries)
    {
        ArgumentNullException.ThrowIfNull(logEntries);

        this.logEntries = logEntries;

        Intervals = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { nameof(ChartTimelineMode.Latest60Minutes), ChartTimelineMode.Latest60Minutes.GetDescription() },
            { nameof(ChartTimelineMode.Latest12Hours), ChartTimelineMode.Latest12Hours.GetDescription() },
            { nameof(ChartTimelineMode.Latest24Hours), ChartTimelineMode.Latest24Hours.GetDescription() },
            { nameof(ChartTimelineMode.Latest48Hours), ChartTimelineMode.Latest48Hours.GetDescription() },
        };

        SelectedInterval = Intervals.First().Key;
    }

    public IRelayCommand RefreshCommand
        => new RelayCommand(RefreshCommandHandler);

    public IDictionary<string, string> Intervals
    {
        get => intervals;
        set
        {
            intervals = value;
            OnPropertyChanged();
        }
    }

    public string SelectedInterval
    {
        get => selectedInterval;
        set
        {
            selectedInterval = value;
            RaisePropertyChanged();

            RefreshChart();
        }
    }

    public DrawnLabelVisual Title { get; set; } =
        new(
            new LabelGeometry
            {
                Text = "Timeline Distribution",
                TextSize = 24,
                Padding = new LiveChartsCore.Drawing.Padding(15),
                Paint = new SolidColorPaint(SKColors.DodgerBlue),
            });

    public ISeries<long>[] Series
    {
        get => series;
        set
        {
            series = value;
            RaisePropertyChanged();
        }
    }

    public Axis[] XAxis
    {
        get => xAxis;
        set
        {
            if (Equals(value, xAxis))
            {
                return;
            }

            xAxis = value;
            RaisePropertyChanged();
        }
    }

    public Axis[] YAxis { get; set; } =
    [
        new Axis
        {
            MinLimit = 0,
            MinStep = 1,
        },
    ];

    public void RefreshChart()
    {
        if (Enum.TryParse<ChartTimelineMode>(SelectedInterval, out var mode))
        {
            PopulateSeries(mode.GetBuckets());
        }
    }

    private void RefreshCommandHandler()
        => RefreshChart();

    private void PopulateSeries(TimelineBuckets buckets)
    {
        var timeline = LogTimeline.Create(logEntries, DateTime.Now, buckets.BucketSize, buckets.BucketCount);

        var seriesCollection = new List<StackedColumnSeries<long>>();
        foreach (var logLevel in LogLevels)
        {
            var stackedColumnSeries = new StackedColumnSeries<long>
            {
                Name = logLevel.ToString(),
                Values = timeline.GetCounts(logLevel).ToArray(),
                Fill = new SolidColorPaint(LogLevelColors[logLevel]),
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = DataLabelsPosition.Middle,
                DataLabelsRotation = -90,
                DataLabelsFormatter = point
                    => point.Coordinate.PrimaryValue.IsZero()
                        ? string.Empty
                        : $"{logLevel}: {point.Coordinate.PrimaryValue}",
            };

            seriesCollection.Add(stackedColumnSeries);
        }

        // ReSharper disable once CoVariantArrayConversion
        Series = seriesCollection.ToArray();

        XAxis =
        [
            new Axis
            {
                LabelsRotation = -90,
                Labels = timeline.BucketStarts
                    .Select(x => x.ToString(buckets.LabelFormat, CultureInfo.InvariantCulture))
                    .ToArray(),
            },
        ];
    }
}