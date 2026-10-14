using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Windows.Input;
namespace AllLive.WinUI.Controls;
public partial class MyAdaptiveGridView : GridView
{
    public ICommand LoadMoreCommand { get; set; }
    public bool CanLoadMore { get; set; }
    public double LoadMoreBottomOffset { get; set; } = 100;
    public static readonly DependencyProperty DataLoadingProperty = DependencyProperty.Register(nameof(DataLoading), typeof(bool), typeof(MyAdaptiveGridView), new PropertyMetadata(true, OnLoadingChanged));
    public bool DataLoading { get => (bool)GetValue(DataLoadingProperty); set => SetValue(DataLoadingProperty, value); }
    public static readonly DependencyProperty DesiredWidthProperty = DependencyProperty.Register(nameof(DesiredWidth), typeof(double), typeof(MyAdaptiveGridView), new PropertyMetadata(260d, OnSizeSettingChanged));
    public double DesiredWidth { get => (double)GetValue(DesiredWidthProperty); set => SetValue(DesiredWidthProperty, value); }
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(MyAdaptiveGridView), new PropertyMetadata(196d, OnSizeSettingChanged));
    public double ItemHeight { get => (double)GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }
    private ScrollViewer scrollViewer;
    private double savedOffset;
    public MyAdaptiveGridView()
    {
        DefaultStyleKey = typeof(GridView);
        SizeChanged += (_, _) => UpdateItemSize();
        Loaded += (_, _) =>
        {
            UpdateItemSize();
            DispatcherQueue.TryEnqueue(() => { if (IsLoaded && Items.Count > 0) scrollViewer?.ChangeView(null, savedOffset, null, true); });
        };
        Unloaded += (_, _) => savedOffset = scrollViewer?.VerticalOffset ?? 0;
    }
    protected override void OnApplyTemplate()
    {
        if (scrollViewer != null) scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
        base.OnApplyTemplate();
        scrollViewer = GetTemplateChild("ScrollViewer") as ScrollViewer;
        if (scrollViewer != null) scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        UpdateItemSize();
    }
    private static void OnSizeSettingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((MyAdaptiveGridView)sender).UpdateItemSize();
    private static void OnLoadingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (MyAdaptiveGridView)sender;
        if (!view.DataLoading && view.CanLoadMore && view.scrollViewer?.ScrollableHeight == 0) view.LoadMoreCommand?.Execute(null);
    }
    private void UpdateItemSize()
    {
        if (ItemsPanelRoot is not ItemsWrapGrid panel || ActualWidth <= 0) return;
        var available = Math.Max(1, ActualWidth - Padding.Left - Padding.Right);
        var columns = Math.Max(1, Math.Floor(available / Math.Max(1, DesiredWidth)));
        panel.ItemWidth = available / columns;
        // Room cards have 8 pixels of margin and two 24-pixel text rows beneath a 16:9 cover.
        panel.ItemHeight = ItemTemplate == (DataTemplate)App.Current.Resources["RoomItemTemplate"]
            ? Math.Max(1, panel.ItemWidth - 8) * 9 / 16 + 56 : ItemHeight;
    }
    private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs args)
    {
        if (!DataLoading && CanLoadMore && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - LoadMoreBottomOffset)
            LoadMoreCommand?.Execute(null);
    }
}
