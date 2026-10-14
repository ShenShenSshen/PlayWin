using PlayWin.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayWin.Core.Helper;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace PlayWin.Core.Helper
{
    public enum LiveSite
    {
        Bilibili=0,
        Douyin=3,
        Unknown=99,
    }
    public class SiteParser
    {
       
        public static async Task<(LiveSite, string)> ParseUrl(string url)
        {
            LiveSite site= LiveSite.Unknown;
            var roomId = "";
            if (url.Contains("bilibili.com"))
            {
                roomId = url.MatchText(@"bilibili\.com/([\d|\w]+)", "");
                site = LiveSite.Bilibili;
            }
            if (url.Contains("b23.tv"))
            {
                var btvReg = new Regex("https?:\\/\\/b23.tv\\/[0-9a-z-A-Z]+");
                var u = btvReg.Match(url)?.Value;
                var location = await GetLocation(u);
                return await ParseUrl(location);
            }

            if (url.Contains("live.douyin.com"))
            {
                roomId = url.MatchText(@"live\.douyin\.com/([\d|\w]+)", "");
                site = LiveSite.Douyin;
            }
            if (url.Contains("webcast.amemv.com"))
            {
                roomId = url.MatchText(@"reflow/(\d+)", "");
                site = LiveSite.Douyin;
            }
            if (url.Contains("v.douyin.com"))
            {
                var regex = new Regex("http.?://v.douyin.com/[\\d\\w]+/");
                var u = regex.Match(url)?.Value;
                var location = await GetLocation(u);

                return await ParseUrl(location);
            }


            return (site, roomId);
        }


        private static async Task<string> GetLocation(string url)
        {
            try
            {
                var headResp = await HttpUtil.Head(url);
                if (headResp.Headers.Location != null)
                {
                    return headResp.Headers.Location.ToString();
                }
              
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return "";
        }

    }
}
