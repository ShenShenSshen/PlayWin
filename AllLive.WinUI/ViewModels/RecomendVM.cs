using AllLive.Core.Helper;
using AllLive.Core.Models;
using AllLive.WinUI.Helper;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace AllLive.WinUI.ViewModels;
public class RecomendVM : LiveRoomListVM
{
    protected override string PlatformName => MainVM.BrowseSite.Name;
    protected override async Task<List<LiveGameSource>> GetSourcesAsync() => GameBrowse.OverviewSources(await GameCatalog.LoadAsync(MainVM.BrowseSite.LiveSite));
}
