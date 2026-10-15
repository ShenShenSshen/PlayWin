using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using Windows.UI.ViewManagement;

namespace PlayWin.WinUI.Helper;

// Only attached to ordinary content roots, never to a player or the window chrome.
public static class PageDesign
{
    public static readonly DependencyProperty InsetsProperty = DependencyProperty.RegisterAttached(
        "Insets", typeof(bool), typeof(PageDesign), new PropertyMetadata(false, OnInsetsChanged));
    public static bool GetInsets(DependencyObject value) => (bool)value.GetValue(InsetsProperty);
    public static void SetInsets(DependencyObject value, bool enabled) => value.SetValue(InsetsProperty, enabled);
    private static void OnInsetsChanged(DependencyObject value, DependencyPropertyChangedEventArgs change)
    {
        if (value is not Grid root) return;
        root.Loaded -= Loaded; root.Unloaded -= Unloaded; root.SizeChanged -= SizeChanged;
        if (!(bool)change.NewValue) return;
        root.Loaded += Loaded; root.Unloaded += Unloaded; root.SizeChanged += SizeChanged;
        ApplyInset(root);
    }
    private static void ApplyInset(Grid root) => root.Padding = new Thickness(root.ActualWidth < 640 ? 16 : 24);
    private static void SizeChanged(object sender, SizeChangedEventArgs args) => ApplyInset((Grid)sender);
    private static void Loaded(object sender, RoutedEventArgs args)
    {
        var root = (Grid)sender; ApplyInset(root);
        var visual = ElementCompositionPreview.GetElementVisual(root);
        visual.StopAnimation("Opacity"); visual.Opacity = 1;
        if (!new UISettings().AnimationsEnabled) return;
        var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0.6f); fade.InsertKeyFrame(1, 1);
        fade.Duration = TimeSpan.FromMilliseconds(160);
        visual.StartAnimation("Opacity", fade);
    }
    private static void Unloaded(object sender, RoutedEventArgs args)
    {
        var visual = ElementCompositionPreview.GetElementVisual((Grid)sender);
        visual.StopAnimation("Opacity"); visual.Opacity = 1;
    }
}
