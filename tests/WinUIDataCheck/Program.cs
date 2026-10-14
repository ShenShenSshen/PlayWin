using PlayWin.WinUI.Helper;
using PlayWin.WinUI.Models;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Security.Cryptography;

void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
var categoryParser = typeof(PlayWin.Core.Douyin).GetMethod("ParseEmbeddedCategoryData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
var categoryFixture = "{\"pathname\":\"/\",\"categoryData\":[{\"title\":\"test\"}]}]";
var parsedCategories = (Newtonsoft.Json.Linq.JObject)categoryParser.Invoke(null, new object[] { categoryFixture });
Check(parsedCategories["categoryData"].Count() == 1, "Embedded category record delimiter was not handled.");
var biliFixture = new List<PlayWin.Core.Models.LiveCategory> {
    new() { ID = "2", Name = "网游", Children = new() { new() { ID = "86", ParentID = "2", Name = "英雄联盟" } } },
    new() { ID = "1", Name = "娱乐游戏秀", Children = new() { new() { ID = "999", ParentID = "1", Name = "英雄联盟" } } },
    new() { ID = "13", Name = "赛事", Children = new() { new() { ID = "561", ParentID = "13", Name = "游戏赛事" }, new() { ID = "563", ParentID = "13", Name = "赛事综合" } } }
};
var biliSite = new PlayWin.Core.BiliBili();
var dySite = new PlayWin.Core.Douyin();
var selected = PlayWin.Core.Helper.GameBrowse.SelectCategories(biliSite, biliFixture);
Check(selected.Count == 2 && selected.Last().Children.Count == 1 && selected.Last().Children[0].ID == "561", "Non-game categories survived or game events were lost.");
var merged = PlayWin.Core.Helper.GameBrowse.MergeCategories(new[] {
    new PlayWin.Core.Models.LiveGameSource { Site = biliSite, Category = biliFixture[0].Children[0] },
    new PlayWin.Core.Models.LiveGameSource { Site = dySite, Category = new() { ID = "test", ParentID = "103,4", Name = " 英雄联盟 " } },
    new PlayWin.Core.Models.LiveGameSource { Site = dySite, Category = new() { ID = "1,1", ParentID = "103,4", Name = "射击游戏" } }
});
Check(merged.Count == 2 && merged.Single(c => c.Name == "英雄联盟").Sources.Count == 2, "Same game names did not merge with sources preserved.");
var dyFixture = new[] { new PlayWin.Core.Models.LiveCategory { ID = "103,4", Name = "游戏", Children = new() }, new PlayWin.Core.Models.LiveCategory { ID = "101,4", Name = "聊天", Children = new() } };
Check(PlayWin.Core.Helper.GameBrowse.SelectCategories(dySite, dyFixture).Count == 1, "Douyin non-game category survived.");
var ownerFixture = Newtonsoft.Json.Linq.JObject.Parse("{\"nickname\":\"test\",\"avatar_thumb\":{\"url_list\":[\"https://example.invalid/avatar\"]}}");
Newtonsoft.Json.Linq.JObject RoomFixture(int status) => new()
{
    ["status"] = status, ["title"] = "test", ["owner"] = ownerFixture.DeepClone(),
    ["cover"] = Newtonsoft.Json.Linq.JObject.Parse("{\"url_list\":[\"https://example.invalid/cover\"]}"),
    ["stream_url"] = new Newtonsoft.Json.Linq.JObject()
};
var detailChecks = 0;
var fixtureRoomStatus = 2;
var statusSite = new PlayWin.Core.Douyin
{
    WebRequestAsync = url =>
    {
        if (new Uri(url).AbsolutePath.Contains("/partition/detail/room/"))
        {
            var rooms = new Newtonsoft.Json.Linq.JArray(new[] { 2, 4, 0 }.Select((status, index) => new Newtonsoft.Json.Linq.JObject { ["web_rid"] = (100 + index).ToString(), ["room"] = RoomFixture(status) }));
            return Task.FromResult(new Newtonsoft.Json.Linq.JObject { ["data"] = new Newtonsoft.Json.Linq.JObject { ["data"] = rooms } }.ToString());
        }
        Check(new Uri(url).AbsolutePath == "/webcast/room/web/enter/", "Unexpected room status request.");
        detailChecks++;
        return Task.FromResult(new Newtonsoft.Json.Linq.JObject { ["data"] = new Newtonsoft.Json.Linq.JObject { ["data"] = new Newtonsoft.Json.Linq.JArray(RoomFixture(fixtureRoomStatus)), ["user"] = ownerFixture.DeepClone() } }.ToString());
    }
};
var statusRooms = await statusSite.GetCategoryRooms(new() { ID = "103,4" });
Check(statusRooms.Rooms.Select(r => r.IsLive).SequenceEqual(new bool?[] { true, false, true }) && detailChecks == 1,
    "Unspecified partition status was treated as stopped or known status was needlessly queried.");
fixtureRoomStatus = 4;
Check(await statusSite.GetLiveStatus("102") && detailChecks == 1, "Repeated live status queries did not reuse the short cache.");
statusSite.Cookie = "test-only-not-a-real-session";
Check(!await statusSite.GetLiveStatus("102") && detailChecks == 2, "Changing login state did not invalidate the live status cache.");
var cachedStatuses = (System.Collections.IDictionary)typeof(PlayWin.Core.Douyin).GetField("liveStatusCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(statusSite);
cachedStatuses["102"] = (DateTime.UtcNow.AddMinutes(-1), false);
fixtureRoomStatus = 2;
Check(await statusSite.GetLiveStatus("102") && detailChecks == 3, "An expired cache hid a room that had started broadcasting.");
if (args.Contains("--live-game-api"))
{
    foreach (var site in new PlayWin.Core.Interface.ILiveSite[] { biliSite, dySite })
    {
        var categories = await site.GetCategores().WaitAsync(TimeSpan.FromSeconds(30));
        var games = PlayWin.Core.Helper.GameBrowse.SelectCategories(site, categories);
        Check(games.Count > 0, site.Name + " has no recognized game partitions.");
        var sources = games.SelectMany(c => c.Children).Select(c => new PlayWin.Core.Models.LiveGameSource { Site = site, Category = c });
        var overview = PlayWin.Core.Helper.GameBrowse.OverviewSources(sources);
        foreach (var source in overview)
        {
            var rooms = await site.GetCategoryRooms(source.Category).WaitAsync(TimeSpan.FromSeconds(30));
            Check(rooms.Rooms.Count > 0 && rooms.Rooms.Any(r => r.IsLive == true), "No confirmed live rooms in " + source.Category.Name);
            Console.WriteLine($"PASS {site.Name}: {source.Category.Name}, live={rooms.Rooms.Count(r => r.IsLive == true)}, total={rooms.Rooms.Count}");
        }
    }
    return;
}
if (args.Contains("--live-api"))
{
    var categories = await new PlayWin.Core.Douyin().GetCategores();
    Check(categories.Count > 0, "Live Douyin category endpoint returned no categories.");
    Console.WriteLine("PASS: live Douyin category API.");
    return;
}
if (args.Contains("--settings-reload"))
{
    Check(SettingHelper.GetValue("theme", 0) == 2, "Theme did not persist.");
    Check(SettingHelper.GetValue(SettingHelper.WINDOW_SIZE, "") == "1600×900", "Window size did not persist.");
    Check(SettingHelper.GetValue("flag", false), "Boolean did not persist.");
    Check(SettingHelper.GetValue("cookie", "") == "test-not-a-real-cookie", "Text did not persist.");
    return;
}
var folder = AppPaths.EnsureDirectory();
Environment.SetEnvironmentVariable("PLAYWIN_TEST_DIR", folder);
SettingHelper.SetValue("theme", 2);
SettingHelper.SetValue(SettingHelper.WINDOW_SIZE, "1600×900");
SettingHelper.SetValue("flag", true);
SettingHelper.SetValue("cookie", "test-not-a-real-cookie");
using (var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath, "--settings-reload") { UseShellExecute = false }))
{
    await child.WaitForExitAsync();
    Check(child.ExitCode == 0, "Settings reload failed in a fresh process.");
}

var target = Path.Combine(folder, "desktop");
await DatabaseHelper.InitializeDatabase(target);
var sites = new[] { "哔哩哔哩直播", "抖音直播", "虎牙直播", "斗鱼直播" };
var database = Path.Combine(target, "PlayWin.db");
Check(File.Exists(database) && Directory.GetFiles(target, "*.db").Length == 1, "PlayWin database filename did not apply.");
using (var seed = new SqliteConnection("Filename=" + database))
{
    seed.Open();
    foreach (var site in sites)
    foreach (var table in new[] { "Favorite", "History" })
    {
        using var command = seed.CreateCommand();
        command.CommandText = $"INSERT INTO {table} (user_name,site_name,photo,room_id) VALUES ('user',@site,'photo','old');";
        command.Parameters.AddWithValue("@site", site);
        command.ExecuteNonQuery();
    }
    using var dates = seed.CreateCommand();
    dates.CommandText = "UPDATE History SET watch_time='2026-01-01 00:00:00';";
    dates.ExecuteNonQuery();
}

await DatabaseHelper.InitializeDatabase(target);
var favorites = await DatabaseHelper.GetFavorites();
var history = await DatabaseHelper.GetHistory();
Check(favorites.Count == 4 && history.Count == 4, "Restored platform data was deleted.");
Check(favorites.All(x => sites.Contains(x.SiteName)), "Unknown platform data survived.");
foreach (var site in sites.Append("未知"))
    DatabaseHelper.AddFavorite(new FavoriteItem { RoomID = "imported", SiteName = site, UserName = "user", Photo = "photo" });
Check((await DatabaseHelper.GetFavorites()).Count == 8, "Import filtering failed.");
DatabaseHelper.DeleteFavorite(favorites[0].ID);
await DatabaseHelper.InitializeDatabase(target);
Check((await DatabaseHelper.GetFavorites()).Count == 7, "Repeated startup overwrote desktop data.");
Console.WriteLine("PASS: PlayWin storage names, persisted settings, four-platform preservation, import filtering and repeated startup.");
