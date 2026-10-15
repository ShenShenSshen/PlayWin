using PlayWin.WinUI.Helper;
using PlayWin.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
namespace PlayWin.WinUI;
public sealed partial class BaseFramePage : Page
{
    private MainWindow owner;
    public BaseFramePage()
    {
        InitializeComponent();

        PointerPressed += BaseFramePage_PointerPressed;
        KeyDown += Page_KeyDown;
        BtnBack.Click += BtnBack_Click;
        MainFrame.Navigated += MainFrame_Navigated;
        Loaded += (_, _) =>
        {
            Detach();
            MessageCenter.NavigatePageEvent += MessageCenter_NavigatePageEvent;
            MessageCenter.ChangeTitleEvent += MessageCenter_ChangeTitleEvent;
            MessageCenter.HideTitlebarEvent += MessageCenter_HideTitlebarEvent;
            owner = App.GetWindowFor(this); owner.Closed += OwnerClosed; owner.SetTitleBar(TitleBar);
        };
        Unloaded += (_, _) => Detach();
    }
    private void OwnerClosed(object sender, WindowEventArgs e) => Detach();
    private void Detach()
    {
        MessageCenter.NavigatePageEvent -= MessageCenter_NavigatePageEvent;
        MessageCenter.ChangeTitleEvent -= MessageCenter_ChangeTitleEvent;
        MessageCenter.HideTitlebarEvent -= MessageCenter_HideTitlebarEvent;
        if (owner != null) owner.Closed -= OwnerClosed;
        owner = null;
    }
    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (MainFrame.Content is LiveRoomPage) return;
        if ((e.Key == Windows.System.VirtualKey.GoBack || e.Key == Windows.System.VirtualKey.GamepadB) && MainFrame.CanGoBack)
        { MainFrame.GoBack(); e.Handled = true; }
    }
    private void MessageCenter_HideTitlebarEvent(object sender, bool hide)
    {
        Grid.SetRow(MainFrame, hide ? 0 : 1);
        Grid.SetRowSpan(MainFrame, hide ? 2 : 1);
        TitleBarGrid.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        App.GetWindowFor(this)?.SetTitleBar(hide ? null : TitleBar);
    }
    private void MessageCenter_ChangeTitleEvent(string title, string logo)
    {
        if (MainFrame.Content is not LiveRoomPage) return;
        Title.Text = string.IsNullOrEmpty(title) ? App.DisplayName : title;
        AppIcon.Source = new BitmapImage(new Uri(logo));
    }
    private void MessageCenter_NavigatePageEvent(Type page, object data) => MainFrame.Navigate(page, data);
    private void BtnBack_Click(object sender, RoutedEventArgs e) { if (MainFrame.CanGoBack) MainFrame.GoBack(); }
    private void MainFrame_Navigated(object sender, NavigationEventArgs e)
    {
        Title.Text = App.DisplayName;
        var playing = MainFrame.Content is LiveRoomPage;
        Title.FontSize = (double)App.Current.Resources[playing ? "BodyTextSize" : "BrandTextSize"];
        Title.LineHeight = (double)App.Current.Resources[playing ? "SecondaryLineHeight" : "BrandLineHeight"];
        Title.FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)App.Current.Resources[playing ? "ContentControlThemeFontFamily" : "BrandFontFamily"];
        Title.CharacterSpacing = playing ? 0 : 35;
        Title.FontWeight = new Windows.UI.Text.FontWeight { Weight = 400 };
        AppIcon.Width = AppIcon.Height = playing ? 18 : 24;
        AppIcon.Source = new SvgImageSource(new Uri("ms-appx:///Assets/PlayWin.svg"));
        BtnBack.Visibility = MainFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
    }
    private void BaseFramePage_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!SettingHelper.GetValue(SettingHelper.MOUSE_BACK, true)) return;
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (kind == Microsoft.UI.Input.PointerUpdateKind.XButton1Pressed && MainFrame.CanGoBack)
        { MainFrame.GoBack(); e.Handled = true; }
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        MainFrame.Navigate(typeof(MainPage));
    }
}
