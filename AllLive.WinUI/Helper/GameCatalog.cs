using AllLive.Core.Helper;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AllLive.WinUI.Helper;
internal static class GameCatalog
{
    public static async Task<List<LiveGameSource>> LoadAsync(AllLive.Core.Interface.ILiveSite selectedSite)
    {
        ArgumentNullException.ThrowIfNull(selectedSite);
        var categories = await selectedSite.GetCategores().WaitAsync(TimeSpan.FromSeconds(20));
        var result = GameBrowse.SelectCategories(selectedSite, categories).SelectMany(c => c.Children)
            .Select(c => new LiveGameSource { Site = selectedSite, Category = c }).ToList();
        if (result.Count == 0) throw new InvalidOperationException("暂无可用游戏分类");
        return result;
    }
}
