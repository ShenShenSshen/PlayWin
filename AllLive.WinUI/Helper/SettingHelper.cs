using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
namespace AllLive.WinUI.Helper
{
    public class SettingHelper
    {
        private static readonly object Gate = new();
        private static readonly string FilePath = Path.Combine(AppPaths.EnsureDirectory(), "settings.json");
        private static readonly Dictionary<string, JToken> Values = File.Exists(FilePath)
            ? JsonConvert.DeserializeObject<Dictionary<string, JToken>>(File.ReadAllText(FilePath)) ?? new()
            : new();
        public static T GetValue<T>(string key, T fallback)
        {
            lock (Gate) return Values.TryGetValue(key, out var value) && value.Type != JTokenType.Null ? value.ToObject<T>() : fallback;
        }
        public static void SetValue<T>(string key, T value)
        {
            lock (Gate)
            {
                Values[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                File.WriteAllText(FilePath + ".tmp", JsonConvert.SerializeObject(Values));
                File.Move(FilePath + ".tmp", FilePath, true);
            }
        }
        /// <summary>
        /// 主题,0为默认，1为浅色，2为深色
        /// </summary>
        public const string THEME = "theme";
        public const string WINDOW_SIZE = "WindowSize";
        public const string DOUYIN_COOKIE = "DouyinProtectedCookie";
        public const string DOUYIN_USER_AGENT = "DouyinUserAgent";
        public const string PREFERRED_QUALITY = "PreferredQuality:";
        public const string BILI_LOGIN_EXPIRED = "BiliLoginExpired";
        public const string DOUYIN_LOGIN_EXPIRED = "DouyinLoginExpired";

        /// <summary>
        /// 新窗口打开直播间
        /// </summary>
        public const string NEW_WINDOW_LIVEROOM = "newWindowLiveRoom";
        /// <summary>
        /// 鼠标功能键返回、关闭页面
        /// </summary>
        public const string MOUSE_BACK = "MouseBack";

        /// <summary>
        /// 视频解码
        /// </summary>
        public const string VIDEO_DECODER = "VideoDecoder";

        /// <summary>
        /// 音量
        /// </summary>
        public const string PLAYER_VOLUME = "PlayerVolume";
        /// <summary>
        /// 亮度
        /// </summary>
        public const string PLAYER_BRIGHTNESS = "PlayeBrightness";

        /// <summary>
        /// 哔哩哔哩Cookie
        /// </summary>
        public const string BILI_COOKIE = "BiliCookie";

        /// <summary>
        /// 哔哩哔哩用户ID
        /// </summary>
        public const string BILI_USER_ID = "BiliUserId";

        /// <summary>
        /// NavigationView导航栏显示模式
        /// </summary>
        public const string PANE_DISPLAY_MODE = "PaneDisplayMode";

        /// <summary>
        /// 忽略哔哩哔哩登录提醒
        /// </summary>
        public const string IGNORE_BILI_LOGIN_TIP = "IgnoreBiliLoginTip";
    }
}
