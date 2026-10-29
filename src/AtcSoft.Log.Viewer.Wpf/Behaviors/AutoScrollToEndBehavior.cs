namespace AtcSoft.Log.Viewer.Wpf.Behaviors;

/// <summary>
/// Keeps a list view scrolled to its last item while items are added.
/// </summary>
/// <remarks>
/// Live entries are added one by one (to keep the selection), so a busy log raises thousands of
/// changes a second. Scrolling on each change made the window stop answering; this behavior scrolls
/// once when the window has drawn the changes, however many arrived.
/// </remarks>
public sealed class AutoScrollToEndBehavior : Behavior<ListView>
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register(
            nameof(IsEnabled),
            typeof(bool),
            typeof(AutoScrollToEndBehavior),
            new PropertyMetadata(defaultValue: false));

    private bool isScrollPending;

    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        ((INotifyCollectionChanged)AssociatedObject.Items).CollectionChanged += OnCollectionChanged;
    }

    protected override void OnDetaching()
    {
        ((INotifyCollectionChanged)AssociatedObject.Items).CollectionChanged -= OnCollectionChanged;
        base.OnDetaching();
    }

    private void OnCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (!IsEnabled || isScrollPending)
        {
            return;
        }

        isScrollPending = true;
        AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.Background, ScrollToEnd);
    }

    private void ScrollToEnd()
    {
        isScrollPending = false;

        var listView = AssociatedObject;
        if (listView is null || !IsEnabled || listView.Items.Count <= 1)
        {
            return;
        }

        listView.ScrollIntoView(listView.Items[^1]);
    }
}