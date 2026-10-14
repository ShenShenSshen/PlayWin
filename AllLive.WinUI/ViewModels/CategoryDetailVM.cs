using AllLive.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace AllLive.WinUI.ViewModels;
public class CategoryDetailVM : LiveRoomListVM
{
    public LiveGameCategory Category { get; private set; }
    public async Task LoadCategoryAsync(LiveGameCategory category)
    {
        Reset();
        Category = category;
        DoPropertyChanged(nameof(Category));
        await LoadAsync();
    }
    protected override Task<List<LiveGameSource>> GetSourcesAsync() => Task.FromResult(Category.Sources);
}
