using PlayWin.Core.Helper;
using PlayWin.WinUI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Media.Playback;

namespace PlayWin.WinUI;
internal static class WatchingChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static async Task RecoveryAsync()
    {
        var recovery = new PlaybackRecovery();
        var calls = 0;
        var result = await recovery.RunAsync(_ => { calls++; return Task.FromResult(RecoveryOutcome.Failed); }, _ => { });
        Check(result == RecoveryOutcome.Failed && calls == 3 && recovery.Attempts == 3, "Recovery exceeded or skipped its three attempts.");
        await recovery.RunAsync(_ => { calls++; return Task.FromResult(RecoveryOutcome.Failed); }, _ => { });
        Check(calls == 3, "Repeated media events restarted an exhausted retry budget.");
        recovery.Reset(); calls = 0;
        result = await recovery.RunAsync(_ => { calls++; return Task.FromResult(RecoveryOutcome.Stopped); }, _ => { });
        Check(result == RecoveryOutcome.Stopped && calls == 1, "A stopped room was retried again.");
        recovery.Reset(); calls = 0;
        var pending = recovery.RunAsync(_ => { calls++; return Task.FromResult(RecoveryOutcome.Failed); }, _ => { });
        recovery.Cancel();
        Check(await pending == RecoveryOutcome.Cancelled && calls == 0, "Leaving playback did not cancel a pending retry.");
        recovery.Reset();
        var entered = new TaskCompletionSource<bool>();
        var release = new TaskCompletionSource<RecoveryOutcome>();
        var old = recovery.RunAsync(async _ => { entered.TrySetResult(true); return await release.Task; }, _ => { });
        await entered.Task;
        recovery.Reset();
        var replacement = recovery.RunAsync(_ => Task.FromResult(RecoveryOutcome.Recovered), _ => { });
        release.SetResult(RecoveryOutcome.Recovered);
        Check(await old == RecoveryOutcome.Cancelled && recovery.IsRunning, "A stale attempt cleared the new recovery operation.");
        Check(await replacement == RecoveryOutcome.Recovered, "A replacement stream could not recover.");
    }
    internal static void LoginErrors()
    {
        Check(RequestErrorMessage.Kind(new HttpRequestException("network")) == LiveFailureKind.Network, "Network errors looked like expired login.");
        Check(RequestErrorMessage.Kind(new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden)) == LiveFailureKind.Platform, "Platform blocking looked like expired login.");
        Check(RequestErrorMessage.Kind(new TimeoutException()) == LiveFailureKind.Network, "Timeout was misclassified.");
        var cookie = "test-only-not-a-real-cookie";
        SettingHelper.SetValue(SettingHelper.BILI_COOKIE, cookie);
        BiliAccount.Instance.ApplyUserInfo(JObject.Parse("{\"code\":-412}"));
        Check(BiliAccount.Instance.Cookie == cookie && BiliAccount.Instance.StatusMessage.Contains("保留"), "Platform failure deleted the saved login.");
        BiliAccount.Instance.ApplyUserInfo(JObject.Parse("{\"code\":-101}"));
        Check(!BiliAccount.Instance.Logined && BiliAccount.Instance.StatusMessage.Contains("失效"), "Explicit authentication failure was not shown.");
        BiliAccount.Instance.Logout();
    }
    internal static async Task ManageFavoritesAsync()
    {
        DatabaseHelper.AddFavorite(new Models.FavoriteItem { SiteName = "哔哩哔哩直播", RoomID = "management-offline-test", UserName = "离线测试", Photo = "" });
        var saved = (await DatabaseHelper.GetFavorites()).Single(item => item.RoomID == "management-offline-test");
        var dialog = new Controls.FavoriteManagementDialog();
        await dialog.LoadAsync();
        var list = ((Grid)dialog.Content).Children.OfType<ListView>().Single();
        var entry = list.Items.Cast<Models.FavoriteItem>().Single(item => item.ID == saved.ID);
        list.SelectedItem = entry;
        dialog.RemoveSelected();
        Check(!(await DatabaseHelper.GetFavorites()).Any(item => item.ID == saved.ID), "All-favorites management could not remove a hidden record.");
    }
    internal static async Task WindowPlacementAsync()
    {
        var oldMain = SettingHelper.GetValue<MainWindow.WindowPlacement>("MainWindowPlacement", null);
        var oldPlayback = SettingHelper.GetValue<MainWindow.WindowPlacement>("PlaybackWindowPlacement", null);
        MainWindow first = null, next = null, playback = null;
        try
        {
            first = new MainWindow(); first.Activate(); await Task.Delay(150);
            var work = DisplayArea.GetFromWindowId(first.AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
            first.AppWindow.Move(new PointInt32(work.X + 70, work.Y + 60));
            await Task.Delay(150);
            var expected = first.AppWindow.Position;
            first.Close(); first = null;
            playback = new MainWindow(true); playback.Activate();
            playback.AppWindow.Move(new PointInt32(work.X + 140, work.Y + 100));
            await Task.Delay(150); playback.Close(); playback = null;
            next = new MainWindow(); next.Activate(); await Task.Delay(150);
            Check(next.AppWindow.Position.X == expected.X && next.AppWindow.Position.Y == expected.Y, "Playback position overwrote the main position or position did not restore.");
            next.Close(); next = null;
            SettingHelper.SetValue("MainWindowPlacement", new MainWindow.WindowPlacement { DisplayId = ulong.MaxValue, WorkX = 250000, WorkY = -250000, OffsetX = 300000, OffsetY = -300000 });
            next = new MainWindow(); next.Activate(); await Task.Delay(150);
            work = DisplayArea.GetFromWindowId(next.AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
            var position = next.AppWindow.Position; var size = next.AppWindow.Size;
            Check(position.X >= work.X && position.Y >= work.Y && position.X + size.Width <= work.X + work.Width && position.Y + size.Height <= work.Y + work.Height,
                "Disconnected display restored a window off-screen.");
        }
        finally
        {
            first?.Close(); next?.Close(); playback?.Close();
            SettingHelper.SetValue("MainWindowPlacement", oldMain);
            SettingHelper.SetValue("PlaybackWindowPlacement", oldPlayback);
        }
    }
    internal static async Task PlayerControlsAsync(MainWindow window, Views.LiveRoomPage page)
    {
        const BindingFlags methods = BindingFlags.NonPublic | BindingFlags.Instance;
        var type = typeof(Views.LiveRoomPage);
        type.GetMethod("LoadSetting", methods).Invoke(page, null);
        var slider = (Slider)page.FindName("SliderVolume");
        var initialVolume = slider.Value;
        slider.Value = .5; page.AdjustVolume(120);
        Check(Math.Abs(slider.Value - .55) < .001 && Math.Abs(SettingHelper.GetValue(SettingHelper.PLAYER_VOLUME, 0d) - .55) < .001, "Mouse volume did not persist.");
        page.AdjustVolume(-10000); Check(slider.Value == 0, "Volume went below zero.");
        page.AdjustVolume(10000); Check(slider.Value == 1, "Volume exceeded one.");
        type.GetMethod("Grid_DoubleTapped", methods).Invoke(page, new object[] { null, null });
        Check(window.AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen, "Double-click did not enter full screen.");
        type.GetMethod("Grid_DoubleTapped", methods).Invoke(page, new object[] { null, null });
        Check(window.AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped, "Double-click did not exit full screen.");
        type.GetMethod("ShowControl", methods).Invoke(page, new object[] { true });
        ((Button)page.FindName("BtnFoucs")).Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        type.GetField("showControlsFlag", methods).SetValue(page, 5);
        type.GetMethod("ControlTimer_Tick", methods).Invoke(page, new object[] { null, null });
        Check(((Grid)page.FindName("control")).Visibility == Visibility.Collapsed, "Idle player controls stayed visible.");
        var path = Path.Combine(AppPaths.EnsureDirectory(), "shortcut-test.wav");
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(16036); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(16000); writer.Write(new byte[16000]);
        }
        var media = (MediaPlayer)type.GetField("mediaPlayer", methods).GetValue(page);
        using var source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(path));
        try
        {
            media.IsMuted = true; media.IsLoopingEnabled = true; media.Source = source; media.Play();
            for (var wait = 0; wait < 30 && media.PlaybackSession.PlaybackState != MediaPlaybackState.Playing; wait++) await Task.Delay(100);
            Check(media.PlaybackSession.PlaybackState == MediaPlaybackState.Playing, "Local test media did not start.");
            page.TogglePause(); await Task.Delay(150);
            Check(media.PlaybackSession.PlaybackState == MediaPlaybackState.Paused, "Pause shortcut did not pause.");
            page.TogglePause(); await Task.Delay(150);
            Check(media.PlaybackSession.PlaybackState == MediaPlaybackState.Playing, "Pause shortcut did not resume.");
        }
        finally { media.Pause(); media.Source = null; media.IsMuted = false; media.IsLoopingEnabled = false; slider.Value = initialVolume; }
    }
}
