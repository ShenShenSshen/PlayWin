using AllLive.Core.Models;
using AllLive.WinUI.Helper;
using AllLive.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;
namespace AllLive.WinUI.Views;
public sealed partial class CategoryPage : Page
{
    readonly CategoryVM categoryVM = new();
    public CategoryPage()
    {
        InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Enabled;
        PlatformPicker.SelectedItem = PlatformPicker.Items[MainVM.Sites.IndexOf(MainVM.BrowseSite)];
        Loaded += (_, _) =>
        {
            if (PlatformPicker.SelectedItem?.Tag != MainVM.BrowseSite) PlatformPicker.SelectedItem = PlatformPicker.Items.Single(i => i.Tag == MainVM.BrowseSite);
            else if (categoryVM.Items.Count == 0) _ = LoadCategoriesAsync();
        };
    }
    private async System.Threading.Tasks.Task LoadCategoriesAsync()
    {
        await categoryVM.LoadAsync();
    }
    private async void PlatformPicker_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (PlatformPicker.SelectedItem?.Tag is not Site site) return;
        MainVM.BrowseSite = site;
        await LoadCategoriesAsync();
    }
    private void GamesGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        args.ItemContainer.ContextFlyout = null;
        if (args.InRecycleQueue || args.Item is not LiveGameCategory game || game.Sources == null || game.Sources.Count == 0) return;
        var site = MainVM.Sites.Single(s => s.LiveSite == game.Sources[0].Site);
        var action = new MenuFlyoutItem();
        var menu = new MenuFlyout();
        menu.Items.Add(action);
        menu.Opening += (_, _) => action.Text = BrowsePreferences.IsPinned(site, game) ? "取消固定" : "固定到首页";
        action.Click += (_, _) =>
        {
            if (!BrowsePreferences.TogglePin(site, game)) Utils.ShowMessageToast("每个平台最多固定 6 个游戏，请先移除已有入口");
        };
        args.ItemContainer.ContextFlyout = menu;
    }
    private void GridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LiveGameCategory category) MessageCenter.NavigatePage(typeof(CategoryDetailPage), category);
    }
}
