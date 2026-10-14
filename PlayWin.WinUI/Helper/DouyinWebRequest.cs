using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlayWin.WinUI.Helper;
internal sealed class DouyinWebRequest
{
    private readonly SemaphoreSlim gate = new(1);
    private CoreWebView2Controller controller;
    private MainWindow owner;

    public async Task<string> GetAsync(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "https" || uri.Host != "live.douyin.com" || !uri.AbsolutePath.StartsWith("/webcast/", StringComparison.Ordinal))
            throw new InvalidOperationException("无效的抖音接口地址");
        await gate.WaitAsync();
        try
        {
            await EnsureBrowserAsync();
            var browser = controller.CoreWebView2;
            var id = Guid.NewGuid().ToString("N");
            var result = new TaskCompletionSource<string>();
            void Received(object sender, CoreWebView2WebMessageReceivedEventArgs args)
            {
                if (!Uri.TryCreate(args.Source, UriKind.Absolute, out var source) || source.Scheme != "https" || source.Host != "live.douyin.com") return;
                try
                {
                    var message = JObject.Parse(args.WebMessageAsJson);
                    if ((string)message["id"] != id) return;
                    if (message["error"] != null)
                    {
                        var status = (int?)message["status"] ?? 0;
                        var kind = status == 401 ? PlayWin.Core.Helper.LiveFailureKind.Authentication : status == 0 ? PlayWin.Core.Helper.LiveFailureKind.Network : PlayWin.Core.Helper.LiveFailureKind.Platform;
                        result.TrySetException(new PlayWin.Core.Helper.LiveRequestException(kind, "抖音官网请求失败"));
                    }
                    else result.TrySetResult((string)message["body"] ?? "");
                }
                catch (JsonException) { }
            }
            browser.WebMessageReceived += Received;
            try
            {
                // Use the official page's fetch hook so its SDK signs each request with the current browser session.
                var requestId = JsonConvert.SerializeObject(id);
                await browser.ExecuteScriptAsync("fetch(" + JsonConvert.SerializeObject(uri.PathAndQuery) + ",{credentials:'include'}).then(r=>{if(!r.ok){chrome.webview.postMessage({id:" + requestId + ",error:true,status:r.status});return null;}return r.text()}).then(body=>{if(body!==null)chrome.webview.postMessage({id:" + requestId + ",body})}).catch(()=>chrome.webview.postMessage({id:" + requestId + ",error:true,status:0}))");
                return await result.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            finally { browser.WebMessageReceived -= Received; }
        }
        finally { gate.Release(); }
    }

    private async Task EnsureBrowserAsync()
    {
        if (controller != null) return;
        owner = App.ActiveWindow ?? throw new InvalidOperationException("抖音浏览器尚未就绪");
        var dataDirectory = Environment.GetCommandLineArgs().Contains("--use-browser-login")
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayWin") : AppPaths.DataDirectory;
        var profile = Path.Combine(dataDirectory, "DouyinWebView");
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, profile, null);
        controller = await environment.CreateCoreWebView2ControllerAsync(CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)owner.Handle));
        controller.IsVisible = false;
        owner.Closed += OwnerClosed;
        var browser = controller.CoreWebView2;
        browser.IsMuted = true;
        browser.Settings.AreDevToolsEnabled = false;
        browser.Settings.AreDefaultContextMenusEnabled = false;
        // The page supplies signing scripts only; its preview streams must never be downloaded or played.
        browser.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Media);
        browser.WebResourceRequested += (_, args) =>
            args.Response = environment.CreateWebResourceResponse(null, 403, "Background media disabled", "Content-Type: text/plain");
        browser.NavigationStarting += (_, args) =>
        {
            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var destination) || destination.Scheme != "https" || destination.Host != "live.douyin.com") args.Cancel = true;
        };
        var loaded = new TaskCompletionSource<bool>();
        void Completed(object sender, CoreWebView2NavigationCompletedEventArgs args) => loaded.TrySetResult(args.IsSuccess);
        browser.NavigationCompleted += Completed;
        try
        {
            await browser.AddScriptToExecuteOnDocumentCreatedAsync(@"
                const mediaUrl = input => /\.(flv|m3u8|mp4|m4s|ts)$/i.test(new URL(input instanceof Request ? input.url : String(input), location.href).pathname);
                const mediaError = () => new DOMException('Background media disabled', 'NotAllowedError');
                const originalFetch = window.fetch;
                window.fetch = function(input) { if (mediaUrl(input)) return Promise.reject(mediaError()); return originalFetch.apply(this, arguments); };
                const originalOpen = XMLHttpRequest.prototype.open;
                XMLHttpRequest.prototype.open = function(method, url) { if (mediaUrl(url)) throw mediaError(); return originalOpen.apply(this, arguments); };
                HTMLMediaElement.prototype.play = function() { this.pause(); return Promise.reject(mediaError()); };
                document.addEventListener('play', event => { if (event.target instanceof HTMLMediaElement) event.target.pause(); }, true);
            ");
            browser.Navigate("https://live.douyin.com/");
            if (!await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15))) throw new InvalidOperationException("抖音官网加载失败，请检查网络");
            for (var attempt = 0; attempt < 50; attempt++)
            {
                if (await browser.ExecuteScriptAsync("typeof window.byted_acrawler?.frontierSign === 'function'") == "true") return;
                await Task.Delay(100);
            }
            throw new InvalidOperationException("抖音官网签名组件尚未就绪，请刷新重试");
        }
        catch { Close(); throw; }
        finally { browser.NavigationCompleted -= Completed; }
    }
    private void OwnerClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args) => Close();
    private void Close()
    {
        if (owner != null) owner.Closed -= OwnerClosed;
        controller?.Close();
        controller = null;
        owner = null;
    }
}
