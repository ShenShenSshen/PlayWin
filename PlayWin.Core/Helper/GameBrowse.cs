using PlayWin.Core.Interface;
using PlayWin.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayWin.Core.Helper
{
    public static class GameBrowse
    {
        public static List<LiveCategory> SelectCategories(ILiveSite site, IEnumerable<LiveCategory> categories)
        {
            var result = new List<LiveCategory>();
            foreach (var category in categories)
            {
                if ((site is BiliBili && new[] { "2", "3", "6" }.Contains(category.ID)) ||
                    (site is Douyin && category.ID == "103,4") ||
                    (site is Huya && new[] { "1", "2", "3" }.Contains(category.ID)) ||
                    (site is Douyu && new[] { "1", "15", "9", "22" }.Contains(category.ID))) result.Add(category);
                else if (site is BiliBili && category.ID == "13")
                {
                    var games = category.Children.Where(c => c.ID == "561").ToList();
                    if (games.Count > 0) result.Add(new LiveCategory { ID = category.ID, Name = "游戏赛事", Children = games });
                }
            }
            return result;
        }
        public static List<LiveGameCategory> MergeCategories(IEnumerable<LiveGameSource> sources) => sources
            .Where(s => !string.IsNullOrWhiteSpace(s.Category.Name))
            .GroupBy(s => s.Category.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new LiveGameCategory
            {
                Name = g.Key,
                Pic = g.Select(s => s.Category.Pic).FirstOrDefault(p => !string.IsNullOrEmpty(p)),
                Sources = g.GroupBy(s => s.Site.Name + ":" + s.Category.ParentID + ":" + s.Category.ID).Select(s => s.First()).ToList()
            }).OrderBy(c => c.Name).ToList();
        public static List<LiveGameSource> OverviewSources(IEnumerable<LiveGameSource> sources) => sources.Where(s =>
            (s.Site is BiliBili && ((new[] { "2", "3", "6" }.Contains(s.Category.ParentID) && s.Category.ID == "0") ||
                (s.Category.ParentID == "13" && s.Category.ID == "561"))) ||
            (s.Site is Douyin && s.Category.ID == "103,4") ||
            ((s.Site is Huya || s.Site is Douyu) && s.Category.ID == "0" && s.Category.ParentID == "1")).ToList();
    }
}
