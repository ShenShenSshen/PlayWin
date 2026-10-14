using AllLive.Core.Models;
using AllLive.WinUI.Helper;
using AllLive.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
namespace AllLive.WinUI.Views;
public sealed partial class CategoryDetailPage : Page
{
    readonly CategoryDetailVM categoryDetailVM = new();
    readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    public CategoryDetailPage()
    {
        InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Enabled;
        Loaded += (_, _) => statusTimer.Start();
        Unloaded += (_, _) => { statusTimer.Stop(); categoryDetailVM.CancelPending(); };
        statusTimer.Tick += async (_, _) => await categoryDetailVM.HideStoppedAsync();
    }
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.NavigationMode == NavigationMode.New && e.Parameter is LiveGameCategory category)
            await categoryDetailVM.LoadCategoryAsync(category);
        else if (categoryDetailVM.Category != null) await categoryDetailVM.HideStoppedAsync();
        if (categoryDetailVM.Category != null) MessageCenter.ChangeTitle(categoryDetailVM.Category.Name);
    }
    private void MyAdaptiveGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LiveRoomItem room) MessageCenter.OpenLiveRoom(room.Source, room);
    }
}
