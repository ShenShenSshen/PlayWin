using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PlayWin.Core.Models;
using PlayWin.WinUI.Helper;
using PlayWin.WinUI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PlayWin.WinUI;

internal static class VisualDesignChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static IEnumerable<DependencyObject> Children(DependencyObject value)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
        {
            var child = VisualTreeHelper.GetChild(value, i); yield return child;
            foreach (var descendant in Children(child)) yield return descendant;
        }
    }
    private static double Contrast(Windows.UI.Color a, Windows.UI.Color b)
    {
        double L(Windows.UI.Color c)
        {
            double S(byte v) { var x = v / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
            return .2126 * S(c.R) + .7152 * S(c.G) + .0722 * S(c.B);
        }
        return (Math.Max(L(a), L(b)) + .05) / (Math.Min(L(a), L(b)) + .05);
    }
    internal static async Task RunAsync(MainWindow window)
    {
        var report = new List<string>();
        var folder = AppPaths.EnsureDirectory();
        var failed = false;
        async Task Case(string name, Func<Task> check)
        {
            try { await check(); report.Add("PASS " + name); }
            catch (Exception ex) { failed = true; report.Add("FAIL " + name + "\n" + ex); }
            File.WriteAllLines(Path.Combine(folder, "visual-checks.log"), report);
        }
        async Task Show(FrameworkElement element)
        {
            window.Root.Content = element; window.Activate(); await Task.Delay(200); element.UpdateLayout();
        }
        async Task Capture(FrameworkElement element, string name)
        {
            var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(element);
            var pixels = (await bitmap.GetPixelsAsync()).ToArray();
            var dir = await StorageFolder.GetFolderFromPathAsync(folder);
            var file = await dir.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
            await encoder.FlushAsync();
        }
        await Case("shared typography roles and native font sizes", async () =>
        {
            foreach (var spec in new[] { ("BodyTextStyle",14d,22d), ("SecondaryTextStyle",13d,20d), ("NavigationTextStyle",14d,20d), ("SectionTitleStyle",16d,24d), ("PageTitleStyle",22d,30d) })
            {
                var text = new TextBlock { Style = (Style)App.Current.Resources[spec.Item1], Text = "游窗 游戏直播 PlayWin" };
                Check(text.FontSize == spec.Item2 && text.LineHeight == spec.Item3 && text.FontWeight.Weight == 400, "Incorrect text role " + spec.Item1);
                Check(text.RenderTransform is not ScaleTransform, "Text must not be scaled");
            }
            await Task.CompletedTask;
        });
        foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
        foreach (var width in new[] { 280d,480d,640d,960d,1280d })
            await Case("category alignment, wrapped platform labels and 4:3 art " + theme + " width=" + width, async () =>
            {
                var page = new Views.CategoryPage(); var root = (Grid)page.Content;
                root.Width = width; root.HorizontalAlignment = HorizontalAlignment.Left; root.RequestedTheme = theme;
                var games = (GridView)page.FindName("GamesGrid");
                var platform = (ListView)page.FindName("PlatformPicker");
                var header = (Grid)page.FindName("BrowseHeader");
                games.ItemsSource = new[] { BrowseChecks.Game(MainVM.Sites[0], "design-1", "三角洲行动"), BrowseChecks.Game(MainVM.Sites[0], "design-2", "暗区突围：无限与较长名称") };
                page.Content = null; await Show(root);
                Check(root.Padding.Left == (width < 640 ? 16 : 24), "Wrong responsive inset");
                for (var i = 0; i < 4; i++)
                {
                    var container = platform.ContainerFromIndex(i) as ListViewItem;
                    Check(container != null, "Missing platform label");
                    var label = Children(container).OfType<TextBlock>().First(t => t.Text == MainVM.Sites[i].Label);
                    var bounds = label.TransformToVisual(platform).TransformBounds(new Windows.Foundation.Rect(0,0,label.ActualWidth,label.ActualHeight));
                    Check(bounds.Right <= platform.ActualWidth + 1 && bounds.Bottom <= platform.ActualHeight + 1, "Platform label clipped");
                    Check(label.LineHeight == 20 && label.Margin.Top >= 0, "Platform baseline uses ad hoc offset");
                    if (container.IsSelected)
                    {
                        var presenter = Children(container).OfType<ListViewItemPresenter>().First();
                        Check(Contrast(((SolidColorBrush)label.Foreground).Color, ((SolidColorBrush)presenter.SelectedBackground).Color) >= 4.5, "Selected platform text contrast too low");
                    }
                }
                var card = (GridViewItem)games.ContainerFromIndex(0);
                var surface = (Border)card.ContentTemplateRoot;
                var image = Children(card).OfType<Image>().Single();
                var art = (Border)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(image));
                Check(Math.Abs(art.ActualWidth / art.ActualHeight - 4d / 3) < .02, "Game image slot lost 4:3 ratio");
                Check(image.Stretch == Stretch.Uniform && surface.CornerRadius.TopLeft == 10 && art.CornerRadius.TopLeft == 8, "Game image crop or corner hierarchy incorrect");
                Check(((SolidColorBrush)art.Background).Color.A == 0, "Game artwork still has a tinted nested frame");
                Check(card.Focus(FocusState.Keyboard) && card.FocusState == FocusState.Keyboard, "Keyboard focus feedback is missing");
                if (!new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
                    Check(((SolidColorBrush)surface.BorderBrush).Color.A == 0, "Persistent category outline remains");
                Check(Math.Abs(surface.TransformToVisual(root).TransformPoint(new Windows.Foundation.Point()).X - header.TransformToVisual(root).TransformPoint(new Windows.Foundation.Point()).X) <= 1, "Header and first card are misaligned");
                var text = Children(card).OfType<TextBlock>().First(t => t.Text == "三角洲行动");
                Check(Contrast(((SolidColorBrush)text.Foreground).Color, ((SolidColorBrush)surface.Background).Color) >= 4.5, "Body contrast too low");
                if (width == 960) await Capture(root, "category-layout-" + theme + ".png");
            });
        foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            await Case("quiet room cards keep rounded video and title details " + theme, async () =>
            {
                var home = new Views.RecomendPage();
                var style = ((GridView)home.FindName("RoomsGrid")).ItemContainerStyle;
                var grid = new Controls.MyAdaptiveGridView { Width = 640, HorizontalAlignment = HorizontalAlignment.Left,
                    RequestedTheme = theme, ItemContainerStyle = style, ItemTemplate = (DataTemplate)App.Current.Resources["RoomItemTemplate"],
                    ItemsSource = new[] { new LiveRoomItem { Title = "用于检查的游戏直播标题", UserName = "测试主播", Cover = "ms-appx:///Assets/PlayWin.png" } } };
                await Show(grid);
                var card = (GridViewItem)grid.ContainerFromIndex(0);
                Check(((SolidColorBrush)card.Background).Color.A == 0, "Room metadata retains an opaque tile background");
                var image = Children(card).OfType<Image>().Single();
                var cover = (Border)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(image));
                Check(cover.CornerRadius == new CornerRadius(10) && Math.Abs(cover.ActualWidth / cover.ActualHeight - 16d / 9) < .02, "Video cover lost its rounded shape or aspect ratio");
                var title = Children(card).OfType<TextBlock>().Single(t => t.Text == "用于检查的游戏直播标题");
                Check(ToolTipService.GetToolTip(title)?.ToString() == title.Text && title.TextTrimming == TextTrimming.CharacterEllipsis, "Full title hint is missing");
                Check(card.Focus(FocusState.Keyboard) && card.FocusState == FocusState.Keyboard, "Room card lost native keyboard focus");
            });
        foreach (var width in new[] {480d,640d,960d,1280d})
            await Case("settings centered panel and responsive actions width=" + width, async () =>
            {
                var page = new Views.SettingsPage { Width = width, HorizontalAlignment = HorizontalAlignment.Left };
                await Show(page);
                var content = (StackPanel)page.FindName("SettingsCards");
                Check(Math.Abs(content.ActualWidth - Math.Min(1040, width - 2 * (width < 640 ? 16 : 24))) <= 2, "Settings width drift");
                foreach (var row in page.SettingRows.Where(r => r.Visibility == Visibility.Visible))
                {
                    var action = (FrameworkElement)row.Children[1];
                    Check(Grid.GetRow(action) == (row.ActualWidth < 640 ? 1 : 0), "Settings action failed to reflow");
                }
                if (width == 960) await Capture(page, "settings-layout.png");
            });
        report.Add("INFO actual rasterization scale=" + window.Root.XamlRoot.RasterizationScale + "; 150/200 percent physical font rendering and OS high contrast remain manual checks.");
        File.WriteAllLines(Path.Combine(folder, "visual-checks.log"), report);
        Environment.ExitCode = failed ? 1 : 0;
    }
}
