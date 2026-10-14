using PlayWin.Core.Helper;
using PlayWin.Core.Models;
using PlayWin.WinUI.Helper;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace PlayWin.WinUI.ViewModels;
public class RecomendVM : LiveRoomListVM
{
    protected override string PlatformName => MainVM.BrowseSite.Name;
    protected override async Task<List<LiveGameSource>> GetSourcesAsync() => GameBrowse.OverviewSources(await GameCatalog.LoadAsync(MainVM.BrowseSite.LiveSite));
}
