using PlayWin.Core.Models;
using PlayWin.WinUI.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlayWin.WinUI.ViewModels;
public abstract class LiveRoomListVM : BaseViewModel
{
    private const int MaxCachedRooms = 300;
    public ObservableCollection<LiveRoomItem> Items { get; } = new();
    private List<LiveGameSource> sources;
    private readonly Dictionary<LiveGameSource, int> pages = new();
    private readonly HashSet<LiveGameSource> finished = new();
    private int generation;
    private bool checkingStatus;
    public bool HasLoaded { get; private set; }
    private string emptyMessage = "暂无已开播的直播";
    public string EmptyMessage { get => emptyMessage; private set { emptyMessage = value; DoPropertyChanged(nameof(EmptyMessage)); } }
    protected virtual bool LiveOnly => true;
    protected virtual string PlatformName => "直播平台";
    protected abstract Task<List<LiveGameSource>> GetSourcesAsync();
    protected virtual Task<LiveCategoryResult> FetchAsync(LiveGameSource source, int page) => source.Site.GetCategoryRooms(source.Category, page);
    public async Task LoadAsync()
    {
        if (Loading || Items.Count >= MaxCachedRooms) return;
        var current = generation;
        Loading = true;
        try
        {
            if (sources == null)
            {
                var loaded = await GetSourcesAsync();
                if (current != generation) return;
                sources = loaded;
            }
            if (current != generation) return;
            var batches = await Task.WhenAll(sources.Where(s => !finished.Contains(s)).Select(async source =>
            {
                try
                {
                    var page = pages.TryGetValue(source, out var next) ? next : 1;
                    var result = await FetchAsync(source, page).WaitAsync(TimeSpan.FromSeconds(20));
                    return (source, page, result, error: (Exception)null);
                }
                catch (Exception ex) { return (source, page: 0, result: (LiveCategoryResult)null, error: ex); }
            }));
            if (current != generation) return;
            var rooms = new List<LiveRoomItem>();
            foreach (var batch in batches)
            {
                if (batch.error != null)
                {
                    finished.Add(batch.source);
                    LogHelper.Log(batch.source.Site.Name + "直播列表读取失败", LogType.ERROR, batch.error);
                    continue;
                }
                pages[batch.source] = batch.page + 1;
                if (!batch.result.HasMore) finished.Add(batch.source);
                foreach (var room in batch.result.Rooms)
                {
                    if (LiveOnly && room.IsLive != true) continue;
                    room.Source = batch.source.Site;
                    rooms.Add(room);
                }
            }
            var keys = new HashSet<string>(Items.Select(r => r.Source.Name + ":" + r.RoomID));
            foreach (var room in rooms.OrderByDescending(r => r.Online))
            {
                if (Items.Count >= MaxCachedRooms) break;
                if (keys.Add(room.Source.Name + ":" + room.RoomID)) Items.Add(room);
            }
            CanLoadMore = Items.Count < MaxCachedRooms && sources.Any(s => !finished.Contains(s));
            if (Items.Count >= MaxCachedRooms)
                Utils.ShowMessageToast("已加载 300 个直播间，可刷新列表或缩小分类、搜索范围");
            var failedBatch = batches.FirstOrDefault(b => b.error != null || b.result?.Error != null);
            var error = failedBatch.error ?? failedBatch.result?.Error;
            EmptyMessage = error != null ? RequestErrorMessage.Report(error, failedBatch.source.Site.Name) :
                LiveOnly && batches.Any(b => b.result.Rooms.Any(r => r.IsLive == null)) ? "部分开播状态查询失败，请稍后刷新" : LiveOnly ? "当前没有已开播的直播" : "没有找到相关直播";
            if (error != null) Utils.ShowMessageToast(EmptyMessage);
        }
        catch (Exception ex)
        {
            if (current == generation) { CanLoadMore = false; EmptyMessage = RequestErrorMessage.Report(ex, PlatformName); HandleError(ex, EmptyMessage); }
        }
        finally
        {
            if (current == generation) { IsEmpty = Items.Count == 0; Loading = false; HasLoaded = true; }
        }
    }
    public async Task HideStoppedAsync()
    {
        if (!LiveOnly || checkingStatus || Loading) return;
        checkingStatus = true;
        var current = generation;
        try
        {
            using var limit = new SemaphoreSlim(4);
            var checks = await Task.WhenAll(Items.ToArray().Select(async room =>
            {
                await limit.WaitAsync();
                if (current != generation) { limit.Release(); return (room, live: (bool?)true, error: (Exception)null); }
                try { return (room, live: (bool?)await room.Source.GetLiveStatus(room.RoomID).WaitAsync(TimeSpan.FromSeconds(10)), error: (Exception)null); }
                catch (Exception ex) { return (room, live: (bool?)null, error: ex); }
                finally { limit.Release(); }
            }));
            if (current != generation) return;
            foreach (var check in checks.Where(c => c.live != true)) Items.Remove(check.room);
            if (checks.Length > 0)
            {
                var failed = checks.FirstOrDefault(c => c.error != null);
                EmptyMessage = failed.error != null ? RequestErrorMessage.Report(failed.error, failed.room.Source.Name) : "当前没有已开播的直播";
            }
            IsEmpty = Items.Count == 0;
        }
        finally { checkingStatus = false; }
    }
    public void Reset()
    {
        generation++;
        Loading = false;
        sources = null;
        pages.Clear();
        finished.Clear();
        Items.Clear();
        CanLoadMore = false;
        IsEmpty = false;
        HasLoaded = false;
    }
    public void CancelPending() { generation++; Loading = false; }
    public override async void Refresh()
    {
        foreach (var site in MainVM.Sites.Select(s => s.LiveSite).OfType<PlayWin.Core.Douyin>()) site.ClearLiveStatusCache();
        Reset(); await LoadAsync();
    }
    public override async void LoadMore() { if (CanLoadMore) await LoadAsync(); }
}
