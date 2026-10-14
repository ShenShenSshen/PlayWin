using PlayWin.WinUI.Helper;
using PlayWin.WinUI.Models;
using PlayWin.WinUI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace PlayWin.WinUI.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class FavoritePage : Page
    {
        readonly FavoriteVM favoriteVM;
        readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(60) };
        public FavoritePage()
        {
            favoriteVM = new FavoriteVM();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            MessageCenter.UpdateFavoriteEvent += MessageCenter_UpdateFavoriteEvent; ;
            this.InitializeComponent();
            Loaded += async (_, _) => { await favoriteVM.LoadAsync(); if (IsLoaded) statusTimer.Start(); };
            Unloaded += (_, _) => { statusTimer.Stop(); favoriteVM.CancelPending(); };
            statusTimer.Tick += async (_, _) => await favoriteVM.LoadAsync();
        }

        private void MessageCenter_UpdateFavoriteEvent(object sender, EventArgs e)
        {
            if (IsLoaded) favoriteVM.Refresh();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);



        }

        private void ls_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as FavoriteItem;
            var site = MainVM.Sites.FirstOrDefault(x => x.Name == item.SiteName);
            MessageCenter.OpenLiveRoom(site.LiveSite, new Core.Models.LiveRoomItem()
            {
                RoomID = item.RoomID
            });
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuFlyoutItem).DataContext as FavoriteItem;
            favoriteVM.RemoveItem(item);
        }
        private async void ManageFavorites_Click(object sender, RoutedEventArgs e)
        {
            await new PlayWin.WinUI.Controls.FavoriteManagementDialog { XamlRoot = XamlRoot }.ShowAsync();
        }
    }
}
