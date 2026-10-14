using PlayWin.WinUI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using System;
using System.IO;
using System.Linq;
using Windows.Graphics;
namespace PlayWin.WinUI;
public sealed partial class MainWindow : Window
{
    public Frame Root => RootFrame;
    public nint Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);
    public static readonly string[] WindowSizeOptions = { "960×540", "1280×720", "1600×900", "1920×1080" };
    public string SelectedWindowSize { get; private set; } = GetSavedWindowSize();
    public event EventHandler WindowModeChanged;
    private bool updatingBounds;
    private PointInt32 normalPosition;
    private FrameworkElement playbackViewport;
    private bool boundsQueued;
    private bool closed;
    private readonly string placementKey;
    private bool restoringPlacement = true;
    public MainWindow(bool playback = false)
    {
        placementKey = playback ? "PlaybackWindowPlacement" : "MainWindowPlacement";
        InitializeComponent();
        SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        Title = App.DisplayName;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "PlayWin.ico"));
        Root.ActualThemeChanged += (_, _) => UpdateCaptionTheme();
        Root.RequestedTheme = (ElementTheme)SettingHelper.GetValue(SettingHelper.THEME, 0);
        Root.SizeChanged += (_, _) => QueueWindowBounds();
        Root.Loaded += (_, _) => Root.XamlRoot.Changed += (_, _) => QueueWindowBounds();
        Closed += (_, _) => { SavePlacement(); closed = true; };
        AppWindow.Changed += (_, args) =>
        {
            if (updatingBounds) return;
            if (args.DidSizeChange || args.DidPositionChange || args.DidPresenterChange) ApplyWindowBounds();
            if (args.DidPresenterChange) WindowModeChanged?.Invoke(this, EventArgs.Empty);
            if (args.DidPositionChange) SavePlacement();
        };
        ExtendsContentIntoTitleBar = true;
        RestorePlacement();
        ApplyWindowBounds();
        restoringPlacement = false;
        UpdateCaptionTheme();
    }
    public static string ValidateWindowSize(string value) => WindowSizeOptions.Contains(value) ? value : "1280×720";
    public static string GetSavedWindowSize()
    {
        try { return ValidateWindowSize(SettingHelper.GetValue(SettingHelper.WINDOW_SIZE, "1280×720")); }
        catch (Newtonsoft.Json.JsonException) { return "1280×720"; }
    }
    internal static SizeInt32 GetPresetSize(string value) => ValidateWindowSize(value) switch
    {
        "960×540" => new(960, 540),
        "1280×720" => new(1280, 720),
        "1600×900" => new(1600, 900),
        "1920×1080" => new(1920, 1080),
        _ => new(1280, 720)
    };
    internal static RectInt32 FitBounds(SizeInt32 size, PointInt32 position, RectInt32 workArea, SizeInt32 chrome = default)
    {
        var availableWidth = Math.Max(1, workArea.Width - chrome.Width);
        var availableHeight = Math.Max(1, workArea.Height - chrome.Height);
        var scale = Math.Min(1d, Math.Min((double)availableWidth / size.Width, (double)availableHeight / size.Height));
        var videoWidth = Math.Max(1, (int)Math.Floor(size.Width * scale));
        var videoHeight = Math.Max(1, (int)Math.Round((double)videoWidth * size.Height / size.Width));
        var width = Math.Min(workArea.Width, videoWidth + chrome.Width);
        var height = Math.Min(workArea.Height, videoHeight + chrome.Height);
        return new RectInt32(Math.Clamp(position.X, workArea.X, workArea.X + workArea.Width - width),
            Math.Clamp(position.Y, workArea.Y, workArea.Y + workArea.Height - height), width, height);
    }
    internal SizeInt32 GetWindowChromeSize()
    {
        var scale = Root.XamlRoot?.RasterizationScale ?? 1;
        var horizontal = playbackViewport?.IsLoaded == true ? Math.Max(0, Root.ActualWidth - playbackViewport.ActualWidth) : 0;
        var vertical = playbackViewport?.IsLoaded == true ? Math.Max(0, Root.ActualHeight - playbackViewport.ActualHeight) : 32;
        return new SizeInt32(Math.Max(0, AppWindow.Size.Width - AppWindow.ClientSize.Width) + (int)Math.Round(horizontal * scale),
            Math.Max(0, AppWindow.Size.Height - AppWindow.ClientSize.Height) + (int)Math.Round(vertical * scale));
    }
    internal void SetPlaybackViewport(FrameworkElement viewport)
    {
        if (playbackViewport != null) playbackViewport.SizeChanged -= PlaybackViewport_SizeChanged;
        playbackViewport = viewport;
        if (playbackViewport != null) playbackViewport.SizeChanged += PlaybackViewport_SizeChanged;
        QueueWindowBounds();
    }
    private void PlaybackViewport_SizeChanged(object sender, SizeChangedEventArgs e) => QueueWindowBounds();
    private void QueueWindowBounds()
    {
        if (closed || boundsQueued) return;
        boundsQueued = true;
        DispatcherQueue.TryEnqueue(() => { boundsQueued = false; if (!closed) ApplyWindowBounds(); });
    }
    public void ApplyWindowSize(string value)
    {
        SelectedWindowSize = ValidateWindowSize(value);
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped) ApplyWindowBounds();
    }
    public void SetWindowMode(AppWindowPresenterKind kind)
    {
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped) { normalPosition = AppWindow.Position; SavePlacement(); }
        updatingBounds = true;
        try
        {
            AppWindow.SetPresenter(kind);
            if (kind == AppWindowPresenterKind.Default) AppWindow.Move(normalPosition);
        }
        finally { updatingBounds = false; }
        ApplyWindowBounds();
        WindowModeChanged?.Invoke(this, EventArgs.Empty);
    }
    private void SavePlacement()
    {
        if (restoringPlacement || closed || AppWindow.Presenter is not OverlappedPresenter presenter || presenter.State != OverlappedPresenterState.Restored) return;
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        SettingHelper.SetValue(placementKey, new WindowPlacement
        {
            DisplayId = display.DisplayId.Value, WorkX = display.WorkArea.X, WorkY = display.WorkArea.Y,
            OffsetX = AppWindow.Position.X - display.WorkArea.X, OffsetY = AppWindow.Position.Y - display.WorkArea.Y
        });
    }
    private void RestorePlacement()
    {
        try
        {
            var saved = SettingHelper.GetValue<WindowPlacement>(placementKey, null);
            if (saved == null) return;
            var displays = DisplayArea.FindAll();
            DisplayArea target = null, previousArea = null;
            for (var index = 0; index < displays.Count; index++)
            {
                var display = displays[index];
                if (display.DisplayId.Value == saved.DisplayId) target = display;
                if (display.WorkArea.X == saved.WorkX && display.WorkArea.Y == saved.WorkY) previousArea = display;
            }
            target ??= previousArea ?? DisplayArea.GetFromPoint(new PointInt32(saved.WorkX, saved.WorkY), DisplayAreaFallback.Nearest);
            var position = new PointInt32((int)Math.Clamp((long)target.WorkArea.X + saved.OffsetX, int.MinValue, int.MaxValue),
                (int)Math.Clamp((long)target.WorkArea.Y + saved.OffsetY, int.MinValue, int.MaxValue));
            AppWindow.MoveAndResize(FitBounds(GetPresetSize(SelectedWindowSize), position, target.WorkArea, GetWindowChromeSize()));
            normalPosition = AppWindow.Position;
        }
        catch (Newtonsoft.Json.JsonException) { }
    }
    internal sealed class WindowPlacement
    {
        public ulong DisplayId { get; set; }
        public int WorkX { get; set; }
        public int WorkY { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
    }
    private void ApplyWindowBounds()
    {
        if (closed || updatingBounds || AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen) return;
        if (AppWindow.Presenter is OverlappedPresenter minimized && minimized.State == OverlappedPresenterState.Minimized) return;
        updatingBounds = true;
        try
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore();
            }
            var size = AppWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay ? new SizeInt32(480, 270) : GetPresetSize(SelectedWindowSize);
            var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
            var chrome = AppWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay ? default : GetWindowChromeSize();
            var bounds = FitBounds(size, AppWindow.Position, workArea, chrome);
            if (AppWindow.Size.Width != bounds.Width || AppWindow.Size.Height != bounds.Height ||
                AppWindow.Position.X != bounds.X || AppWindow.Position.Y != bounds.Y)
                AppWindow.MoveAndResize(bounds);
        }
        finally { updatingBounds = false; }
    }
    private void UpdateCaptionTheme()
    {
        var foreground = ((Microsoft.UI.Xaml.Media.SolidColorBrush)Root.Foreground).Color;
        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
    }
}
