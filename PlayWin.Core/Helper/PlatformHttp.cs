using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
namespace PlayWin.Core.Helper;
internal static class PlatformHttp
{
    internal static async Task<string> Send(string url, string body, string cookie, string userAgent, string referer)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        client.DefaultRequestHeaders.Referrer = new Uri(referer);
        if (!string.IsNullOrEmpty(cookie)) client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        using var response = body == null ? await client.GetAsync(url) : await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"));
        if (!response.IsSuccessStatusCode) throw new LiveRequestException(response.StatusCode == HttpStatusCode.Unauthorized ? LiveFailureKind.Authentication : LiveFailureKind.Platform, "平台请求失败：" + (int)response.StatusCode);
        return Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
    }
    internal static JObject Json(string text, string code = "error")
    {
        var json = JObject.Parse(text);
        LiveRequestException.EnsureSuccess(json, code);
        return json;
    }
    internal static int Hot(JToken value)
    {
        var text = value?.ToString() ?? "0";
        if (!double.TryParse(text.Replace("万", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)) return 0;
        return (int)Math.Min(int.MaxValue, number * (text.Contains("万") ? 10000 : 1));
    }
    internal static string Room(object value)
    {
        var id = value?.ToString() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9_]{1,64}$")) throw new ArgumentException("直播间地址无效");
        return id;
    }
}
