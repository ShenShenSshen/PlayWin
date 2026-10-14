using PlayWin.WinUI.Models;
using PlayWin.WinUI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PlayWin.Core.Models;
using PlayWin.WinUI.Helper;
using Windows.UI.ViewManagement;
using Windows.UI.Popups;
using Windows.System.Display;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.ApplicationModel.Core;
using System.Diagnostics;
using Newtonsoft.Json;
using Windows.UI.Core;
using FFmpegInteropX;
using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using Windows.Media.Playback;
using System.Text;
using System.Runtime;
using System.Threading;
// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace PlayWin.WinUI.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class LiveRoomPage : Page
    {
        readonly LiveRoomVM liveRoomVM;

        FFmpegInteropX.FFmpegMediaSource interopMSS;
        readonly MediaPlayer mediaPlayer;

        DisplayRequest dispRequest;
        private bool displayActive;
        PageArgs pageArgs;
        //当前处于小窗
        private bool isMini = false;
        private bool isFullWindow;
        private MainWindow playbackWindow;
        private bool closingWindow;
        private readonly PlaybackRecovery recovery = new();
        private int playGeneration;
        private bool switchingLine;
        private DateTime healthySince;
        DispatcherTimer controlTimer;

        public LiveRoomPage()
        {
            this.InitializeComponent();

            liveRoomVM = new LiveRoomVM();
            dispRequest = new DisplayRequest();

            KeyDown += Page_KeyDown;
          
            liveRoomVM.ChangedPlayUrl += LiveRoomVM_ChangedPlayUrl;
            controlTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
            controlTimer.Tick += ControlTimer_Tick;
            mediaPlayer = new MediaPlayer();
            mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            mediaPlayer.PlaybackSession.BufferingStarted += PlaybackSession_BufferingStarted;
            mediaPlayer.PlaybackSession.BufferingProgressChanged += PlaybackSession_BufferingProgressChanged;
            mediaPlayer.PlaybackSession.BufferingEnded += PlaybackSession_BufferingEnded;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

            Unloaded += LiveRoomPage_Unloaded;
            Loaded += (_, _) =>
            {
                controlTimer.Start();
                playbackWindow = App.GetWindowFor(this);
                playbackWindow.WindowModeChanged += PlaybackWindow_ModeChanged;
                playbackWindow.SetPlaybackViewport(player);
                UpdateWindowModeUI();
                if (SettingHelper.GetValue(SettingHelper.NEW_WINDOW_LIVEROOM, false))
                {
                    TitleBar.Visibility = Visibility.Visible;
                    App.GetWindowFor(this).SetTitleBar(TitleBar);
                    App.GetWindowFor(this).Closed += (_, _) =>
                    {
                        closingWindow = true;
                        StopPlay();
                    };
                }
            };
        }
        private void LiveRoomPage_Unloaded(object sender, RoutedEventArgs e)
        {
            recovery.Cancel();
            playGeneration++;
            liveRoomVM.CancelPendingPlayback();
            controlTimer.Stop();
            if (playbackWindow != null)
            {
                playbackWindow.WindowModeChanged -= PlaybackWindow_ModeChanged;
                playbackWindow.SetPlaybackViewport(null);
            }
            playbackWindow = null;
        }
        private void PlaybackWindow_ModeChanged(object sender, EventArgs e) => UpdateWindowModeUI();
        private void HideTitleBar(bool hide)
        {
            if (SettingHelper.GetValue(SettingHelper.NEW_WINDOW_LIVEROOM, false))
            {
                Grid.SetRow(GridContent, hide ? 0 : 1);
                Grid.SetRowSpan(GridContent, hide ? 2 : 1);
                TitleBarGrid.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
                App.GetWindowFor(this)?.SetTitleBar(hide ? null : TitleBar);
            }
            else MessageCenter.HideTitlebar(hide);
        }

        #region 播放器事件
        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(PlayError);
        }

        private async void PlaybackSession_BufferingEnded(MediaPlaybackSession sender, object args)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                PlayerLoading.Visibility = Visibility.Collapsed;
            });

        }

        private async void PlaybackSession_BufferingProgressChanged(MediaPlaybackSession sender, object args)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                PlayerLoadText.Text = sender.BufferingProgress.ToString("p");
            });
        }

        private async void PlaybackSession_BufferingStarted(MediaPlaybackSession sender, object args)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                PlayerLoading.Visibility = Visibility.Visible;
                PlayerLoadText.Text = "缓冲中";
            });
        }

        private async void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {

                PlayError();
            });

        }

        private async void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (!IsLoaded || closingWindow || mediaPlayer.Source == null) return;
                //保持屏幕常亮
                SetKeepAwake(mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing);
                PlayerLoading.Visibility = Visibility.Collapsed;
                SetMediaInfo();
            });
        }

        private async void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (!IsLoaded || closingWindow || mediaPlayer.Source == null) return;
                switch (sender.PlaybackState)
                {
                    case MediaPlaybackState.None:
                        break;
                    case MediaPlaybackState.Opening:
                        PlayerLoading.Visibility = Visibility.Visible;
                        PlayerLoadText.Text = "加载中";
                        break;
                    case MediaPlaybackState.Buffering:
                        PlayerLoading.Visibility = Visibility.Visible;
                        PlayerLoadText.Text = "缓冲中";
                        break;
                    case MediaPlaybackState.Playing:
                        healthySince = DateTime.UtcNow;
                        PlayerLoading.Visibility = Visibility.Collapsed;
                        PlayBtnPlay.Visibility = Visibility.Collapsed;
                        PlayBtnPause.Visibility = Visibility.Visible;
                        SetKeepAwake(true);
                        liveRoomVM.Living = true;
                        SetMediaInfo();
                        break;
                    case MediaPlaybackState.Paused:
                        SetKeepAwake(false);
                        PlayerLoading.Visibility = Visibility.Collapsed;
                        PlayBtnPlay.Visibility = Visibility.Visible;
                        PlayBtnPause.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        break;
                }
            });
        }

        private void SetMediaInfo()
        {
            try
            {

                var str = $"Url: {liveRoomVM.CurrentLine?.Url ?? ""}\r\n";
                str += $"Quality: {liveRoomVM.CurrentQuality?.Quality ?? ""}\r\n";
                str += $"Video Codec: {interopMSS.CurrentVideoStream.CodecName}\r\nAudio Codec:{interopMSS.AudioStreams[0].CodecName}\r\n";
                str += $"Resolution: {interopMSS.CurrentVideoStream.PixelWidth} x {interopMSS.CurrentVideoStream.PixelHeight}\r\n";
                str += $"Video Bitrate: {interopMSS.CurrentVideoStream.Bitrate / 1024} Kbps\r\n";
                str += $"Audio Bitrate: {interopMSS.AudioStreams[0].Bitrate / 1024} Kbps\r\n";
                str += $"Decoder Engine: {interopMSS.CurrentVideoStream.DecoderEngine.ToString()}";
                txtInfo.Text = str;
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"读取信息失败\r\n{ex.Message}";
            }

        }

        #endregion

        private async void Page_KeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (!IsLoaded || XamlRoot == null) return;
            var elent = FocusManager.GetFocusedElement(XamlRoot);
            if (elent is TextBox || elent is AutoSuggestBox)
            {
                args.Handled = false;
                return;
            }
            if (args.Key == Windows.System.VirtualKey.Space && !ReferenceEquals(elent, BtnFoucs) && (elent is ButtonBase || elent is Slider || elent is ComboBox)) return;

            args.Handled = true;
            switch (args.Key)
            {
                case Windows.System.VirtualKey.Space:
                    TogglePause();
                    break;

                case Windows.System.VirtualKey.Up:
                    if (mediaPlayer.Volume + 0.1 > 1)
                    {
                        mediaPlayer.Volume = 1;
                    }
                    else
                    {
                        mediaPlayer.Volume += 0.1;
                    }

                    TxtToolTip.Text = "音量:" + mediaPlayer.Volume.ToString("P");
                    ToolTip.Visibility = Visibility.Visible;
                    await Task.Delay(2000);
                    ToolTip.Visibility = Visibility.Collapsed;
                    break;

                case Windows.System.VirtualKey.Down:
                    if (mediaPlayer.Volume - 0.1 < 0)
                    {
                        mediaPlayer.Volume = 0;
                    }
                    else
                    {
                        mediaPlayer.Volume -= 0.1;
                    }

                    if (mediaPlayer.Volume == 0)
                    {
                        TxtToolTip.Text = "静音";
                    }
                    else
                    {
                        TxtToolTip.Text = "音量:" + mediaPlayer.Volume.ToString("P");
                    }
                    ToolTip.Visibility = Visibility.Visible;
                    await Task.Delay(2000);
                    ToolTip.Visibility = Visibility.Collapsed;
                    break;
                case Windows.System.VirtualKey.Escape:
                    SetFullScreen(false);

                    break;
                case Windows.System.VirtualKey.F8:
                case Windows.System.VirtualKey.T:
                    //小窗播放
                    MiniWidnows(BottomBtnExitMiniWindows.Visibility == Visibility.Visible);

                    break;
                case Windows.System.VirtualKey.F12:
                case Windows.System.VirtualKey.W:
                    SetFullWindow(PlayBtnFullWindow.Visibility == Visibility.Visible);
                    break;
                case Windows.System.VirtualKey.F11:
                case Windows.System.VirtualKey.F:
                case Windows.System.VirtualKey.Enter:
                    SetFullScreen(PlayBtnFullScreen.Visibility == Visibility.Visible);
                    break;
                case Windows.System.VirtualKey.F10:
                    await CaptureVideo();
                    break;
                case Windows.System.VirtualKey.GamepadA:
                    ShowControl(control.Visibility == Visibility.Collapsed);
                    break;

                case Windows.System.VirtualKey.GamepadLeftTrigger:
                    //刷新直播间
                    BottomBtnRefresh_Click(this, null);
                    break;
                case Windows.System.VirtualKey.GamepadB:
                    //退出直播间
                    this.Frame.GoBack();
                    break;
                case Windows.System.VirtualKey.GamepadRightTrigger:
                    //关注/取消关注
                    if (liveRoomVM.IsFavorite)
                    {
                        liveRoomVM.RemoveFavoriteCommand.Execute(null);
                        Utils.ShowMessageToast("已取消关注");
                    }
                    else
                    {
                        liveRoomVM.AddFavoriteCommand.Execute(null);
                        Utils.ShowMessageToast("已添加关注");
                    }

                    break;
                default:
                    break;
            }
        }

        private void LiveRoomVM_ChangedPlayUrl(object sender, string e)
        {
            if (!switchingLine) recovery.Reset();
            else recovery.Cancel();
            _ = SetPlayer(e);
        }
        private async Task<bool> SetPlayer(string url, bool recovering = false)
        {
            var generation = ++playGeneration;
            try
            {
                PlaybackFailure.Visibility = Visibility.Collapsed;
                PlayerLoading.Visibility = Visibility.Visible;
                PlayerLoadText.Text = "加载中";
                if (mediaPlayer != null)
                {
                    mediaPlayer.Pause();
                    mediaPlayer.Source = null;
                }
                if (interopMSS != null)
                {
                    interopMSS.Dispose();
                    interopMSS = null;
                }

                var config = new MediaSourceConfig();
                config.FFmpegOptions.Add("rtsp_transport", "tcp");
                config.FFmpegOptions.Add("rw_timeout", "15000000");
                var decoder = SettingHelper.GetValue<int>(SettingHelper.VIDEO_DECODER, 0);
                switch (decoder)
                {
                    case 1:
                        config.Video.VideoDecoderMode = VideoDecoderMode.ForceSystemDecoder;
                        break;
                    case 2:
                        config.Video.VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder;
                        break;
                    default:
                        config.Video.VideoDecoderMode = VideoDecoderMode.Automatic;
                        break;
                }
                if (liveRoomVM.SiteName == "哔哩哔哩直播")
                {
                    config.FFmpegOptions.Add("user_agent", "Mozilla/5.0 BiliDroid/1.12.0 (bbcallen@gmail.com)");
                    config.FFmpegOptions.Add("referer", "https://live.bilibili.com/");
                }
                try
                {
                    var source = await FFmpegMediaSource.CreateFromUriAsync(url, config);
                    if (generation != playGeneration || closingWindow || !IsLoaded) { source.Dispose(); return false; }
                    interopMSS = source;
                }
                catch (Exception ex)
                {
                    LogHelper.Log("播放器初始化失败", LogType.ERROR, ex);
                    if (!recovering && generation == playGeneration) PlayError();
                    return false;
                }

                mediaPlayer.AutoPlay = true;
                mediaPlayer.Volume = SliderVolume.Value;
                mediaPlayer.Source = interopMSS.CreateMediaPlaybackItem();
                player.SetMediaPlayer(mediaPlayer);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Log("播放器设置失败", LogType.ERROR, ex);
                if (!recovering && generation == playGeneration) PlayError();
                return false;
            }

        }

        private async void PlayError()
        {
            if (!IsLoaded || closingWindow || recovery.IsRunning || liveRoomVM.CurrentLine == null)
            {
                return;
            }
            // 当前线路播放失败，尝试下一个线路
            var index = liveRoomVM.Lines.IndexOf(liveRoomVM.CurrentLine);
            healthySince = default;
            if (index >= 0 && index < liveRoomVM.Lines.Count - 1)
            {
                switchingLine = true;
                try { liveRoomVM.CurrentLine = liveRoomVM.Lines[index + 1]; }
                finally { switchingLine = false; }
                return;
            }
            var outcome = await recovery.RunAsync(async cancellation =>
            {
                var url = await liveRoomVM.RefreshStreamAsync(cancellation);
                if (url == null) return RecoveryOutcome.Stopped;
                return await SetPlayer(url, true) ? RecoveryOutcome.Recovered : RecoveryOutcome.Failed;
            }, attempt =>
            {
                PlayerLoading.Visibility = Visibility.Visible;
                PlayerLoadText.Text = "正在恢复直播（" + attempt + "/3）";
            });
            if (!IsLoaded || closingWindow || outcome == RecoveryOutcome.Cancelled) return;
            if (outcome == RecoveryOutcome.Stopped)
            {
                playGeneration++;
                mediaPlayer.Pause();
                mediaPlayer.Source = null;
                liveRoomVM.Living = false;
                SetKeepAwake(false);
                interopMSS?.Dispose();
                interopMSS = null;
                PlayerLoading.Visibility = Visibility.Collapsed;
            }
            else if (outcome == RecoveryOutcome.Failed)
            {
                SetKeepAwake(false);
                PlayerLoading.Visibility = Visibility.Collapsed;
                PlaybackFailure.Visibility = Visibility.Visible;
                PlaybackFailureText.Text = recovery.LastError != null ? RequestErrorMessage.Report(recovery.LastError, liveRoomVM.SiteName) : "自动恢复未成功，请手动重试，或在设置中调整解码器。";
            }
        }

        private void StopPlay()
        {
            recovery.Cancel();
            playGeneration++;
            liveRoomVM.CancelPendingPlayback();
            if (mediaPlayer != null)
            {
                mediaPlayer.Pause();
                mediaPlayer.Source = null;
            }
            if (interopMSS != null)
            {
                interopMSS.Dispose();
                interopMSS = null;
            }

            controlTimer.Stop();
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);

            SetFullScreen(false);
            MiniWidnows(false);
            //取消屏幕常亮
            if (dispRequest != null)
            {
                try
                {
                    SetKeepAwake(false);
                }
                catch (Exception)
                {
                }

                dispRequest = null;
            }
        }
        private void SetKeepAwake(bool enabled)
        {
            if (displayActive == enabled) return;
            if (enabled) { dispRequest ??= new DisplayRequest(); dispRequest.RequestActive(); }
            else dispRequest?.RequestRelease();
            displayActive = enabled;
        }
        private void ControlTimer_Tick(object sender, object e)
        {
            if (!IsLoaded || XamlRoot == null) return;
            if (recovery.Attempts > 0 && healthySince != default && mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing && DateTime.UtcNow - healthySince >= TimeSpan.FromSeconds(30)) recovery.Reset();
            if (showControlsFlag != -1)
            {
                if (showControlsFlag >= 5)
                {
                    var elent = FocusManager.GetFocusedElement(XamlRoot);
                    if (!IsControlInteractionActive(elent as DependencyObject))
                    {
                        ShowControl(false);
                        showControlsFlag = -1;
                    }
                }
                else
                {
                    showControlsFlag++;
                }
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {

            StopPlay();

            KeyDown -= Page_KeyDown;

            base.OnNavigatingFrom(e);
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == Microsoft.UI.Xaml.Navigation.NavigationMode.New)
            {
                pageArgs = e.Parameter as PageArgs;
                LoadSetting();

                var siteInfo = MainVM.Sites.FirstOrDefault(x => x.LiveSite.Equals(pageArgs.Site));

                liveRoomVM.SiteLogo = siteInfo.Logo;
                liveRoomVM.SiteName = siteInfo.Name;

                var data = pageArgs.Data as LiveRoomItem;
                MessageCenter.ChangeTitle("", pageArgs.Site);

                liveRoomVM.LoadData(pageArgs.Site, data.RoomID);

            }
        }

        private async Task CaptureVideo()
        {
            try
            {
                string fileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".png";
                StorageFolder applicationFolder = await StorageFolder.GetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
                StorageFolder folder = await applicationFolder.CreateFolderAsync("直播截图", CreationCollisionOption.OpenIfExists);
                StorageFile saveFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
                RenderTargetBitmap bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(player);
                var pixelBuffer = await bitmap.GetPixelsAsync();
                using (var fileStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Ignore,
                         (uint)bitmap.PixelWidth,
                         (uint)bitmap.PixelHeight,
                         XamlRoot.RasterizationScale * 96,
                         XamlRoot.RasterizationScale * 96,
                         pixelBuffer.ToArray());
                    await encoder.FlushAsync();
                }
                Utils.ShowMessageToast("截图已经保存至图片库");
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("截图失败");
            }
        }

        private void LoadSetting()
        {
            //音量
            var volume = SettingHelper.GetValue<double>(SettingHelper.PLAYER_VOLUME, 1.0);
            mediaPlayer.Volume = volume;
            SliderVolume.Value = volume;
            SliderVolume.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                mediaPlayer.Volume = SliderVolume.Value;
                SettingHelper.SetValue<double>(SettingHelper.PLAYER_VOLUME, SliderVolume.Value);
            });
            //亮度
            _brightness = SettingHelper.GetValue<double>(SettingHelper.PLAYER_BRIGHTNESS, 0);
            BrightnessShield.Opacity = _brightness;

        }

        #region 手势
        int showControlsFlag = 0;
        bool pointer_in_player = false;

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (IsInteractiveSource(e?.OriginalSource as DependencyObject)) return;
            BtnFoucs.Focus(FocusState.Programmatic);
            ShowControl(control.Visibility == Visibility.Collapsed);

        }
        private void ShowControl(bool show)
        {
            showControlsFlag = show ? 0 : -1;
            control.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ProtectedCursor = show || !pointer_in_player ? InputSystemCursor.Create(InputSystemCursorShape.Arrow) : null;
        }
        private void Grid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (IsInteractiveSource(e?.OriginalSource as DependencyObject)) return;
            if (e != null) e.Handled = true;
            if (isMini)
            {
                MiniWidnows(false);
                return;
            }

            if (PlayBtnFullScreen.Visibility == Visibility.Visible)
            {
                PlayBtnFullScreen_Click(sender, null);
            }
            else
            {

                PlayBtnExitFullScreen_Click(sender, null);
            }
        }
        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            pointer_in_player = true;
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!IsLoaded || XamlRoot == null) return;
            if (e != null && sender is FrameworkElement surface)
            {
                var point = e.GetCurrentPoint(surface).Position;
                if (point.X >= 0 && point.Y >= 0 && point.X < surface.ActualWidth && point.Y < surface.ActualHeight) return;
            }
            pointer_in_player = false;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            if (!IsControlInteractionActive(FocusManager.GetFocusedElement(XamlRoot) as DependencyObject)) ShowControl(false);
        }

        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            ShowControl(true);
            if (ProtectedCursor == null)
            {
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            }

        }
        private static bool IsInteractiveSource(DependencyObject source)
        {
            while (source != null)
            {
                if (source is ButtonBase || source is Slider || source is ComboBox || source is TextBox || source is MenuFlyoutItem) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        private static bool IsControlInteractionActive(DependencyObject source) => source is TextBox || source is AutoSuggestBox || source is MenuFlyoutItem ||
            (source is ComboBox combo && combo.IsDropDownOpen) || (source is Slider slider && slider.PointerCaptures.Count > 0);
        internal void TogglePause()
        {
            if (mediaPlayer.Source == null) return;
            if (mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused) mediaPlayer.Play();
            else mediaPlayer.Pause();
        }
        internal void AdjustVolume(int delta) => SliderVolume.Value = Math.Clamp(SliderVolume.Value + delta / 120d * .05, 0, 1);
        private void Grid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (IsInteractiveSource(e.OriginalSource as DependencyObject)) return;
            var point = e.GetCurrentPoint((UIElement)sender);
            if (point.Properties.IsHorizontalMouseWheel) return;
            AdjustVolume(point.Properties.MouseWheelDelta);
            e.Handled = true;
        }

        bool ManipulatingBrightness = false;
        private void Grid_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            e.Handled = true;
            //progress.Visibility = Visibility.Visible;
            if (ManipulatingBrightness)
                HandleSlideBrightnessDelta(e.Delta.Translation.Y);
            else
                HandleSlideVolumeDelta(e.Delta.Translation.Y);
        }

        private void HandleSlideVolumeDelta(double delta)
        {
            if (delta > 0)
            {
                double dd = delta / (this.ActualHeight * 0.8);

                //slider_V.Value -= d;
                var volume = mediaPlayer.Volume - dd;
                if (volume < 0) volume = 0;
                SliderVolume.Value = volume;

            }
            else
            {
                double dd = Math.Abs(delta) / (this.ActualHeight * 0.8);
                var volume = mediaPlayer.Volume + dd;
                if (volume > 1) volume = 1;
                SliderVolume.Value = volume;
                //slider_V.Value += d;
            }
            TxtToolTip.Text = "音量:" + mediaPlayer.Volume.ToString("P");

            //Utils.ShowMessageToast("音量:" +  mediaElement.MediaPlayer.Volume.ToString("P"), 3000);
        }
        private void HandleSlideBrightnessDelta(double delta)
        {
            double dd = Math.Abs(delta) / (this.ActualHeight * 0.8);
            if (delta > 0)
            {
                Brightness = Math.Min(Brightness + dd, 1);
            }
            else
            {
                Brightness = Math.Max(Brightness - dd, 0);
            }
            TxtToolTip.Text = "亮度:" + Math.Abs(Brightness - 1).ToString("P");
        }
        private void Grid_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            e.Handled = true;
            TxtToolTip.Text = "";
            ToolTip.Visibility = Visibility.Visible;

            if (e.Position.X < this.ActualWidth / 2)
                ManipulatingBrightness = true;
            else
                ManipulatingBrightness = false;

        }

        double _brightness;
        double Brightness
        {
            get => _brightness;
            set
            {
                _brightness = value;
                BrightnessShield.Opacity = value;
                SettingHelper.SetValue<double>(SettingHelper.PLAYER_BRIGHTNESS, _brightness);
            }
        }

        private void Grid_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            e.Handled = true;
            ToolTip.Visibility = Visibility.Collapsed;
        }
        #endregion
        #region 窗口操作
        private void PlayBtnFullScreen_Click(object sender, RoutedEventArgs e)
        {
            SetFullScreen(true);
        }

        private void PlayBtnExitFullScreen_Click(object sender, RoutedEventArgs e)
        {
            SetFullScreen(false);
        }

        private void PlayBtnExitFullWindow_Click(object sender, RoutedEventArgs e)
        {
            SetFullWindow(false);
        }

        private void PlayBtnFullWindow_Click(object sender, RoutedEventArgs e)
        {
            SetFullWindow(true);
        }

        private void PlayBtnMinWindow_Click(object sender, RoutedEventArgs e)
        {
            MiniWidnows(true);
        }
        private void SetFullWindow(bool full)
        {
            isFullWindow = full;
            UpdateWindowModeUI();
        }
        private void SetFullScreen(bool full) => SetWindowMode(full ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default);
        private void MiniWidnows(bool mini) => SetWindowMode(mini ? AppWindowPresenterKind.CompactOverlay : AppWindowPresenterKind.Default);
        private void SetWindowMode(AppWindowPresenterKind kind)
        {
            if (closingWindow) return;
            App.GetWindowFor(this).SetWindowMode(kind);
            UpdateWindowModeUI();
        }
        private void UpdateWindowModeUI()
        {
            var window = playbackWindow ?? App.GetWindowFor(this);
            if (window == null) return;
            var full = window.AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
            isMini = window.AppWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay;
            HideTitleBar(full || isMini);
            PlayBtnFullScreen.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
            PlayBtnExitFullScreen.Visibility = full ? Visibility.Visible : Visibility.Collapsed;
            PlayBtnFullWindow.Visibility = isFullWindow ? Visibility.Collapsed : Visibility.Visible;
            PlayBtnExitFullWindow.Visibility = isFullWindow ? Visibility.Visible : Visibility.Collapsed;
            BottomInfo.Height = full || isMini || isFullWindow ? new GridLength(0) : GridLength.Auto;
            StandardControl.Visibility = isMini ? Visibility.Collapsed : Visibility.Visible;
            MiniControl.Visibility = isMini ? Visibility.Visible : Visibility.Collapsed;
        }
        private void BottomBtnExitMiniWindows_Click(object sender, RoutedEventArgs e)
        {
            MiniWidnows(false);
        }

        private async void PlayTopBtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            await CaptureVideo();
        }

        private void PlayBtnPlay_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Play();
        }

        private void PlayBtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
        }

        #endregion

        private void BottomBtnShare_Click(object sender, RoutedEventArgs e)
        {
            if (liveRoomVM.detail == null)
            {
                return;
            }
            Utils.SetClipboard(liveRoomVM.detail.Url);
            Utils.ShowMessageToast("已复制链接到剪切板");
        }

        private async void BottomBtnOpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (liveRoomVM.detail == null)
            {
                return;
            }
            await Windows.System.Launcher.LaunchUriAsync(new Uri(liveRoomVM.detail.Url));
        }

        private void BottomBtnPlayUrl_Click(object sender, RoutedEventArgs e)
        {
            if (liveRoomVM.CurrentLine == null)
            {
                return;
            }
            Utils.SetClipboard(liveRoomVM.CurrentLine.Url);
            Utils.ShowMessageToast("已复制链接到剪切板");
        }

        private void BottomBtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            recovery.Reset();
            PlaybackFailure.Visibility = Visibility.Collapsed;
            if (liveRoomVM.Loading) return;
            if (mediaPlayer != null)
            {
                mediaPlayer.Pause();
                mediaPlayer.Source = null;
            }
            if (interopMSS != null)
            {
                interopMSS.Dispose();
                interopMSS = null;
            }

            liveRoomVM.LoadData(pageArgs.Site, liveRoomVM.RoomID);
        }

        private void PlayerGrid_SizeChanged(object sender, SizeChangedEventArgs args)
        {
            ((Grid)sender).Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, args.NewSize.Width, args.NewSize.Height) };
        }
    }
}
