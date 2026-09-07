using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PathSegment = FileSurfer.Core.Models.PathSegment;

namespace FileSurfer.Core.Views.Helpers;

/// <summary>
/// A Windows-11-Explorer-style breadcrumb bar: a row of clickable directory-name buttons
/// separated by chevrons, collapsing the leading segments behind a "…" overflow menu when
/// they do not fit. Display only — the editable path box and the "Searching…" text are
/// separate siblings handled by the hosting view.
/// </summary>
public class BreadcrumbBar : Panel
{
    // FiraCode Nerd Font chevron-right (the same glyph the GoForward button uses).
    private const string ChevronGlyph = "";
    private const string OverflowGlyph = "…";
    private const double MinLastSegmentWidth = 40;
    private const double FallbackHeight = 30;

    /// <summary>
    /// Identifies the <see cref="Segments"/> property.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> SegmentsProperty =
        AvaloniaProperty.Register<BreadcrumbBar, IEnumerable?>(nameof(Segments));

    private readonly Button _overflowButton;
    private readonly MenuFlyout _overflowMenu = new();
    private readonly List<Button> _segmentButtons = new();
    private readonly List<TextBlock?> _chevrons = new();
    private readonly List<PathSegment> _segments = new();

    private INotifyCollectionChanged? _subscribedCollection;
    private double[] _segmentWidths = Array.Empty<double>();
    private double[] _chevronWidths = Array.Empty<double>();
    private double _overflowWidth;
    private bool _overflowActive;
    private int _overflowCount;
    private int _lastBuiltOverflowCount = -1;

    static BreadcrumbBar()
    {
        SegmentsProperty.Changed.AddClassHandler<BreadcrumbBar>((bar, e) => bar.OnSegmentsChanged(e));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BreadcrumbBar"/> class.
    /// </summary>
    public BreadcrumbBar()
    {
        Background = Brushes.Transparent;
        ClipToBounds = true;

        _overflowButton = new Button
        {
            Content = OverflowGlyph,
            Flyout = _overflowMenu,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
        };
        _overflowButton.Classes.Add("BreadcrumbOverflow");
        Children.Add(_overflowButton);
    }

    /// <summary>
    /// Gets or sets the ordered, root-to-leaf sequence of <see cref="PathSegment"/>s to display.
    /// </summary>
    public IEnumerable? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>
    /// Raised when the user activates a segment (by clicking it or picking it from the overflow menu).
    /// </summary>
    public event EventHandler<BreadcrumbSegmentEventArgs>? SegmentInvoked;

    /// <summary>
    /// Raised when the user clicks the empty area of the bar, requesting the editable path box.
    /// </summary>
    public event EventHandler? EditRequested;

    private void OnSegmentsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_subscribedCollection is not null)
            _subscribedCollection.CollectionChanged -= OnSegmentsCollectionChanged;

        _subscribedCollection = e.NewValue as INotifyCollectionChanged;
        if (_subscribedCollection is not null)
            _subscribedCollection.CollectionChanged += OnSegmentsCollectionChanged;

        RebuildChildren();
    }

    private void OnSegmentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildChildren();

    private void RebuildChildren()
    {
        foreach (Button button in _segmentButtons)
            Children.Remove(button);
        foreach (TextBlock? chevron in _chevrons)
            if (chevron is not null)
                Children.Remove(chevron);

        _segmentButtons.Clear();
        _chevrons.Clear();
        _segments.Clear();

        if (Segments is not null)
            foreach (object? item in Segments)
                if (item is PathSegment segment)
                    _segments.Add(segment);

        for (int i = 0; i < _segments.Count; i++)
        {
            if (i > 0)
            {
                TextBlock chevron = new() { Text = ChevronGlyph, ClipToBounds = true };
                chevron.Classes.Add("BreadcrumbChevron");
                _chevrons.Add(chevron);
                Children.Add(chevron);
            }
            else
            {
                _chevrons.Add(null);
            }

            bool isLast = i == _segments.Count - 1;
            Button button = new()
            {
                Content = new TextBlock
                {
                    Text = _segments[i].DisplayName,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = isLast ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                },
                Tag = i,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
            };
            button.Classes.Add("BreadcrumbSegment");
            if (isLast)
                button.Classes.Add("Current");

            button.Click += OnSegmentClick;
            _segmentButtons.Add(button);
            Children.Add(button);
        }

        _lastBuiltOverflowCount = -1;
        InvalidateMeasure();
    }

    private void OnSegmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index } && index >= 0 && index < _segments.Count)
            RaiseSegmentInvoked(_segments[index]);
    }

    private void RaiseSegmentInvoked(PathSegment segment) =>
        SegmentInvoked?.Invoke(this, new BreadcrumbSegmentEventArgs(segment));

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (
            ReferenceEquals(e.Source, this)
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
        )
            EditRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double height = double.IsFinite(availableSize.Height) ? availableSize.Height : FallbackHeight;
        int count = _segments.Count;

        if (_segmentWidths.Length != count)
        {
            _segmentWidths = new double[count];
            _chevronWidths = new double[count];
        }

        _overflowButton.Measure(Size.Infinity);
        _overflowWidth = _overflowButton.DesiredSize.Width;

        // Measure only the currently-visible children; hidden ones keep their last known width so
        // the overflow decision (made in ArrangeOverride against the real width) never oscillates.
        for (int i = 0; i < count; i++)
        {
            if (_segmentButtons[i].IsVisible)
            {
                _segmentButtons[i].Measure(Size.Infinity);
                _segmentWidths[i] = _segmentButtons[i].DesiredSize.Width;
            }

            if (_chevrons[i] is { IsVisible: true } chevron)
            {
                chevron.Measure(Size.Infinity);
                _chevronWidths[i] = chevron.DesiredSize.Width;
            }
            else if (_chevrons[i] is null)
            {
                _chevronWidths[i] = 0;
            }
        }

        // This control adapts to the width it is given (like trimmed text) and must never force
        // its host column wider, so it reports a minimal desired width.
        return new Size(0, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = _segments.Count;
        double height = finalSize.Height;

        double fullWidth = 0;
        for (int i = 0; i < count; i++)
            fullWidth += _segmentWidths[i] + _chevronWidths[i];

        int firstVisible;
        if (count <= 1 || fullWidth <= finalSize.Width)
        {
            _overflowActive = false;
            firstVisible = 0;
        }
        else
        {
            _overflowActive = true;
            double budget = finalSize.Width - _overflowWidth;
            double running = _segmentWidths[count - 1] + _chevronWidths[count - 1];
            firstVisible = count - 1;
            for (int i = count - 2; i >= 1; i--)
            {
                double add = _segmentWidths[i] + _chevronWidths[i];
                if (running + add > budget)
                    break;

                running += add;
                firstVisible = i;
            }
        }

        _overflowCount = firstVisible;
        for (int i = 0; i < count; i++)
        {
            bool visible = i >= firstVisible;
            SetVisible(_segmentButtons[i], visible);
            if (_chevrons[i] is { } chevron)
                SetVisible(chevron, visible);
        }
        SetVisible(_overflowButton, _overflowActive);
        UpdateOverflowMenu();

        double x = 0;
        if (_overflowActive)
        {
            _overflowButton.Arrange(new Rect(x, 0, _overflowWidth, height));
            x += _overflowWidth;
        }

        for (int i = firstVisible; i < count; i++)
        {
            if (_chevrons[i] is { } chevron)
            {
                chevron.Arrange(new Rect(x, 0, _chevronWidths[i], height));
                x += _chevronWidths[i];
            }

            double width = _segmentWidths[i];
            if (i == count - 1)
            {
                double remaining = finalSize.Width - x;
                width = Math.Max(Math.Min(width, remaining), MinLastSegmentWidth);
            }

            _segmentButtons[i].Arrange(new Rect(x, 0, width, height));
            x += width;
        }

        return finalSize;
    }

    private static void SetVisible(Control control, bool visible)
    {
        if (control.IsVisible != visible)
            control.IsVisible = visible;
    }

    private void UpdateOverflowMenu()
    {
        int overflowCount = _overflowActive ? _overflowCount : 0;
        if (overflowCount == _lastBuiltOverflowCount)
            return;

        _lastBuiltOverflowCount = overflowCount;
        _overflowMenu.Items.Clear();

        for (int i = 0; i < overflowCount && i < _segments.Count; i++)
        {
            PathSegment segment = _segments[i];
            MenuItem menuItem = new() { Header = segment.DisplayName };
            menuItem.Click += (_, _) => RaiseSegmentInvoked(segment);
            _overflowMenu.Items.Add(menuItem);
        }
    }
}
