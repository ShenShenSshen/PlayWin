using AllLive.Core.Interface;
using AllLive.Core.Models;

namespace AllLive.WinUI.Models;

internal sealed class SearchRoomItem : LiveRoomItem
{
    internal SearchRoomItem(LiveRoomItem room, ILiveSite site)
    {
        Source = site;
        RoomID = room.RoomID;
        Title = room.Title;
        Cover = room.Cover;
        UserName = room.UserName;
        Online = room.Online;
        IsLive = room.IsLive;
    }
    internal string PlatformLabel => Source is AllLive.Core.BiliBili ? "哔哩哔哩" : "抖音";
    internal string StatusLabel => IsLive.HasValue ? IsLive.Value ? "直播中" : "未开播" : null;
}
