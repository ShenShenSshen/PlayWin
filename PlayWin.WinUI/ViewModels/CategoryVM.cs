using PlayWin.Core.Helper;
using PlayWin.Core.Models;
using PlayWin.WinUI.Helper;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
namespace PlayWin.WinUI.ViewModels;
public class CategoryVM : BaseViewModel
{
    public ObservableCollection<LiveGameCategory> Items { get; } = new();
    public ObservableCollection<LiveCategory> Groups { get; } = new();
    private int generation;
    private string emptyMessage = "暂无可用游戏分类";
    public string EmptyMessage { get => emptyMessage; private set { emptyMessage = value; DoPropertyChanged(nameof(EmptyMessage)); } }
    private PlayWin.Core.Interface.ILiveSite loadedSite;
    private LiveCategory selectedGroup;
    public LiveCategory SelectedGroup
    {
        get => selectedGroup;
        set
        {
            if (selectedGroup == value) return;
            selectedGroup = value;
            ShowGroup(value);
            if (value != null && loadedSite != null)
                SettingHelper.SetValue(BrowsePreferences.GroupKey(MainVM.Sites.Single(s => s.LiveSite == loadedSite)), value.ID);
            DoPropertyChanged(nameof(SelectedGroup));
        }
    }
    public async Task LoadAsync()
    {
        var current = ++generation;
        var site = MainVM.BrowseSite.LiveSite;
        Loading = true;
        EmptyMessage = "暂无可用游戏分类";
        SelectedGroup = null;
        Items.Clear();
        Groups.Clear();
        try
        {
            var categories = GameBrowse.SelectCategories(site, await site.GetCategores().WaitAsync(TimeSpan.FromSeconds(20)));
            if (current != generation) return;
            loadedSite = site;
            foreach (var category in categories) Groups.Add(category);
            SelectedGroup = BrowsePreferences.RestoreGroup(MainVM.Sites.Single(s => s.LiveSite == site), Groups);
        }
        catch (Exception ex) { if (current == generation) { EmptyMessage = RequestErrorMessage.Report(ex, site.Name); HandleError(ex, EmptyMessage); } }
        finally { if (current == generation) { IsEmpty = Items.Count == 0; Loading = false; } }
    }
    public void ShowGroup(LiveCategory group)
    {
        Items.Clear();
        if (group != null)
            foreach (var category in GameBrowse.MergeCategories(group.Children.Select(c => new LiveGameSource { Site = loadedSite, Category = c })))
            {
                category.Pic ??= "ms-appx:///Assets/PlayWin.png";
                Items.Add(category);
            }
        IsEmpty = Items.Count == 0;
    }
    public override async void Refresh() => await LoadAsync();
}
