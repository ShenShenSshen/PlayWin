using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using PlayWin.Core.Helper;
using PlayWin.WinUI.Controls;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace PlayWin.WinUI.Helper;
internal sealed class DouyuScriptBrowser
{
    private readonly SemaphoreSlim gate = new(1);
    private CoreWebView2Controller controller;
    private MainWindow owner;
    internal async Task<string> SignAsync(string room, string script)
    {
        if (!Regex.IsMatch(room, "^[0-9]+$") || script.Length > 1000000) throw new InvalidOperationException("斗鱼签名参数无效");
        await gate.WaitAsync();
        try
        {
            await EnsureAsync();
            var unpacker = Regex.Replace(script, @"eval.*?;}", "strc;}", RegexOptions.Singleline);
            var unpacked = await controller.CoreWebView2.ExecuteScriptAsync("(()=>{" + unpacker + ";return ub98484234();})()").AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var code = JsonConvert.DeserializeObject<string>(unpacked) ?? throw new LiveRequestException(LiveFailureKind.Platform, "斗鱼签名脚本未返回结果");
            var version = Regex.Match(code, @"v=(\d+)").Groups[1].Value;
            if (version.Length == 0) throw new LiveRequestException(LiveFailureKind.Platform, "斗鱼签名格式已变化");
            var did = Guid.NewGuid().ToString("N"); var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var hash = PlayWin.Core.Helper.Utils.ToMD5(room + did + time + version);
            code = Regex.Replace(code, @"return rt;}\);?", "return rt;}");
            code = Regex.Replace(code, @"\(function\s*\(", "function sign(");
            code = Regex.Replace(code, @"CryptoJS\.MD5\(cb\)\.toString\(\)", JsonConvert.SerializeObject(hash));
            var signed = await controller.CoreWebView2.ExecuteScriptAsync("(()=>{" + code + ";return sign(" + JsonConvert.SerializeObject(room) + "," + JsonConvert.SerializeObject(did) + "," + JsonConvert.SerializeObject(time) + ");})()").AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var args = JsonConvert.DeserializeObject<string>(signed);
            if (string.IsNullOrWhiteSpace(args) || args.Length > 20000 || !args.Contains("sign=")) throw new LiveRequestException(LiveFailureKind.Platform, "斗鱼播放签名失败");
            return args;
        }
        catch (TimeoutException) { Close(); throw; }
        finally { gate.Release(); }
    }
    private async Task EnsureAsync()
    {
        if (controller != null) return;
        owner = App.ActiveWindow ?? throw new InvalidOperationException("播放窗口尚未就绪");
        var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, WebLoginDialog.Profile(LiveSite.Douyu), null);
        controller = await env.CreateCoreWebView2ControllerAsync(CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)owner.Handle));
        owner.Closed += OwnerClosed; controller.IsVisible = false;
        var browser = controller.CoreWebView2; browser.Settings.AreDevToolsEnabled = false; browser.Settings.AreDefaultContextMenusEnabled = false;
        await BrowserMediaBlock.ApplyAsync(browser, env);
        browser.NavigationStarting += (_, e) => { if (!WebLoginDialog.IsTrusted(LiveSite.Douyu, e.Uri)) e.Cancel = true; };
        browser.NewWindowRequested += (_, e) => e.Handled = true;
        var loaded = new TaskCompletionSource<bool>();
        void Completed(object sender, CoreWebView2NavigationCompletedEventArgs e) => loaded.TrySetResult(e.IsSuccess);
        browser.NavigationCompleted += Completed;
        try { browser.Navigate("https://www.douyu.com/"); if (!await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15))) throw new LiveRequestException(LiveFailureKind.Network, "斗鱼官网加载失败"); }
        catch { Close(); throw; }
        finally { browser.NavigationCompleted -= Completed; }
    }
    internal bool IsMuted => controller?.CoreWebView2.IsMuted ?? true;
    private void OwnerClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs e) => Close();
    private void Close() { if(owner != null) owner.Closed -= OwnerClosed; controller?.Close(); controller = null; owner = null; }
}
