using PlayWin.WinUI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PlayWin.WinUI.Controls;
public sealed partial class DouyinLoginDialog : ContentDialog
{
    private readonly WebView2 browser = new();
    private readonly TextBlock message = new() { Text = "在抖音官方网页完成登录，然后点击保存登录。", TextWrapping = TextWrapping.Wrap };
    private bool closed;
    internal WebView2 Browser => browser;
    private static readonly string ProfilePath = Path.Combine(AppPaths.EnsureDirectory(), "DouyinWebView");
    public DouyinLoginDialog()
    {
        var availableWidth = App.ActiveWindow?.Root.XamlRoot?.Size.Width ?? 800;
        Resources["ContentDialogMaxWidth"] = Math.Max(280, Math.Min(980, availableWidth - 32));
        Title = "登录抖音";
        PrimaryButtonText = "保存登录";
        CloseButtonText = "取消";
        IsPrimaryButtonEnabled = false;
        var panel = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
        panel.Children.Add(message);
        Grid.SetRow(browser, 1);
        panel.Children.Add(browser);
        Content = panel;
        Opened += async (_, _) => await InitializeAsync();
        Closed += (_, _) => { closed = true; browser.Close(); };
        PrimaryButtonClick += SaveLogin;
    }
    internal static bool IsTrusted(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        (uri.Host == "douyin.com" || uri.Host.EndsWith(".douyin.com", StringComparison.OrdinalIgnoreCase));
    private async Task InitializeAsync()
    {
        try
        {
            browser.Width = Math.Max(240, Math.Min(900, XamlRoot.Size.Width - 80));
            browser.Height = Math.Max(80, Math.Min(520, XamlRoot.Size.Height - 200));
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, ProfilePath, null);
            await browser.EnsureCoreWebView2Async(environment);
            if (closed) return;
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.IsMuted = true;
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            browser.CoreWebView2.NavigationStarting += (_, args) => { if (!IsTrusted(args.Uri)) args.Cancel = true; };
            browser.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (IsTrusted(args.Uri)) browser.CoreWebView2.Navigate(args.Uri);
            };
            browser.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess) message.Text = "网页加载失败，请关闭后重试";
            };
            browser.CoreWebView2.Navigate("https://live.douyin.com/");
            IsPrimaryButtonEnabled = true;
        }
        catch { if (!closed) message.Text = "无法打开抖音登录，请检查网络及 WebView2 Runtime 是否可用"; }
    }
    private async void SaveLogin(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await SaveSessionAsync();
        }
        catch (Exception ex) { args.Cancel = true; message.Text = ex is InvalidOperationException ? ex.Message : "登录保存失败，请重试"; }
        finally { deferral.Complete(); }
    }
    internal async Task SaveSessionAsync()
    {
        var cookies = await browser.CoreWebView2.CookieManager.GetCookiesAsync("https://live.douyin.com/");
        var cookie = string.Join("; ", cookies.GroupBy(c => c.Name).Select(c => c.First()).Select(c => c.Name + "=" + c.Value));
        await DouyinAccount.Instance.SaveAsync(cookie, browser.CoreWebView2.Settings.UserAgent);
    }
    public static async Task ClearCookiesAsync()
    {
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, ProfilePath, null);
        var controller = await environment.CreateCoreWebView2ControllerAsync(CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)App.ActiveWindow.Handle));
        try { controller.IsVisible = false; controller.CoreWebView2.CookieManager.DeleteAllCookies(); }
        finally { controller.Close(); }
    }
}
