using AllLive.Core.Interface;
using AllLive.WinUI.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllLive.WinUI.ViewModels
{
    public class MainVM
    {

        public static List<Site> Sites = new List<Site>() {
            new Site()
            {
                SiteType=LiveSite.Bilibili,
                Name="哔哩哔哩直播",
                Logo="ms-appx:///Assets/Logo/bilibili.png",
                LiveSite=new AllLive.Core.BiliBili(),
            },
            new Site()
            {
                SiteType=LiveSite.Douyin,
                Name="抖音直播",
                Logo="ms-appx:///Assets/Logo/douyin.png",
                LiveSite=new AllLive.Core.Douyin(),
            },
        };
        private static Site browseSite = BrowsePreferences.ReadPlatform();
        public static Site BrowseSite
        {
            get => browseSite;
            set
            {
                if (!Sites.Contains(value)) throw new ArgumentException("Unknown browse platform.");
                if (browseSite == value) return;
                browseSite = value;
                SettingHelper.SetValue(BrowsePreferences.PlatformKey, value.SiteType.ToString());
            }
        }

    }
    public class Site
    {
        public LiveSite SiteType { get; set; }
        public string Name { get; set; }
        public ILiveSite LiveSite { get; set; }
        public string Logo { get; set; }
    }
}
