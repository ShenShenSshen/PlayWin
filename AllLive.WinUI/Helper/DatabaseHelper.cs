using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

using System.IO;
using AllLive.WinUI.Models;
using AllLive.WinUI.ViewModels;

namespace AllLive.WinUI.Helper
{

    public static class DatabaseHelper
    {
        static SqliteConnection db;
        public static Task InitializeDatabase(string dataDirectory = null, string legacyDatabase = null)
        {
            var folder = Directory.CreateDirectory(dataDirectory ?? AppPaths.DataDirectory).FullName;
            string dbPath = Path.Combine(folder, "alllive.db");
            if (!File.Exists(dbPath))
            {
                if (legacyDatabase == null && dataDirectory == null)
                {
                    var packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
                    if (Directory.Exists(packages))
                        legacyDatabase = Directory.GetDirectories(packages, "5421.24244EC421563_*")
                            .Select(path => Path.Combine(path, "LocalState", "alllive.db")).FirstOrDefault(File.Exists);
                }
                if (legacyDatabase != null && File.Exists(legacyDatabase))
                    ImportLegacyDatabase(legacyDatabase, dbPath);
            }
            db?.Dispose();
            db = new SqliteConnection($"Filename={dbPath}");
            db.Open();
            string tableCommand = @"CREATE TABLE IF NOT EXISTS Favorite (
id INTEGER PRIMARY KEY AUTOINCREMENT, 
user_name TEXT,
site_name TEXT,
photo TEXT,
room_id TEXT);

CREATE TABLE IF NOT EXISTS History (
id INTEGER PRIMARY KEY AUTOINCREMENT, 
user_name TEXT,
site_name TEXT,
photo TEXT,
room_id TEXT,
watch_time DATETIME);
";
            SqliteCommand createTable = new SqliteCommand(tableCommand, db);
            createTable.ExecuteNonQuery();

            using (var cleanup = new SqliteCommand(@"DELETE FROM Favorite WHERE site_name IN ('虎牙直播', '斗鱼直播');
DELETE FROM History WHERE site_name IN ('虎牙直播', '斗鱼直播');", db))
            {
                cleanup.ExecuteNonQuery();
            }
            return Task.CompletedTask;


        }

        private static void ImportLegacyDatabase(string sourcePath, string targetPath)
        {
            var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".import";
            try
            {
                using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
                using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporaryPath, Pooling = false }.ToString()))
                {
                    source.Open();
                    target.Open();
                    source.BackupDatabase(target);
                }
                File.Move(temporaryPath, targetPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }


        public static void AddFavorite(FavoriteItem item)
        {
            if (!MainVM.Sites.Any(x => x.Name == item.SiteName)) { return; }
            if (CheckFavorite(item.RoomID, item.SiteName)!=null) { return; }
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "INSERT INTO Favorite VALUES (NULL,@user_name,@site_name, @photo, @room_id);";
            command.Parameters.AddWithValue("@user_name", item.UserName);
            command.Parameters.AddWithValue("@site_name", item.SiteName);
            command.Parameters.AddWithValue("@photo", item.Photo);
            command.Parameters.AddWithValue("@room_id", item.RoomID);
            command.ExecuteReader();
        }
        public static long? CheckFavorite(string roomId, string siteName)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "SELECT * FROM Favorite WHERE room_id=@room_id and site_name=@site_name";
            command.Parameters.AddWithValue("@site_name", siteName);
            command.Parameters.AddWithValue("@room_id", roomId);
            var result = command.ExecuteScalar();
            if (result==null)
            {
                return null;
            }
            return (long)result;
        }
        public static void DeleteFavorite(long id)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "DELETE FROM Favorite WHERE id=@id";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();

        }

        public static void DeleteFavorite()
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "DELETE FROM Favorite";
            command.ExecuteNonQuery();

        }

        public async static Task<List<FavoriteItem>> GetFavorites()
        {
            List<FavoriteItem> favoriteItems = new List<FavoriteItem>();
            SqliteCommand command = new SqliteCommand("SELECT * FROM Favorite", db);
            var reader =await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                favoriteItems.Add(new FavoriteItem()
                {
                    ID= reader.GetInt32(0),
                    RoomID = reader.GetString(4),
                    Photo = reader.GetString(3),
                    SiteName = reader.GetString(2),
                    UserName = reader.GetString(1)
                });
            }
            return favoriteItems;
        }


        public static void AddHistory(HistoryItem item)
        {
            if (!MainVM.Sites.Any(x => x.Name == item.SiteName)) { return; }
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            var hisId = CheckHistory(item.RoomID, item.SiteName);
            if (hisId != null)
            {  
                //更新时间
                command.CommandText = "UPDATE History SET watch_time=@time WHERE room_id=@room_id and site_name=@site_name";
                command.Parameters.AddWithValue("@site_name", item.SiteName);
                command.Parameters.AddWithValue("@room_id", item.RoomID);
                command.Parameters.AddWithValue("@time", DateTime.Now);
                command.ExecuteReader();
              
                return;
            }
          
            command.CommandText = "INSERT INTO History VALUES (NULL,@user_name,@site_name, @photo, @room_id,@time);";
            command.Parameters.AddWithValue("@user_name", item.UserName);
            command.Parameters.AddWithValue("@site_name", item.SiteName);
            command.Parameters.AddWithValue("@photo", item.Photo);
            command.Parameters.AddWithValue("@room_id", item.RoomID);
            command.Parameters.AddWithValue("@time", DateTime.Now);
            command.ExecuteReader();
        }
        public static long? CheckHistory(string roomId, string siteName)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "SELECT * FROM History WHERE room_id=@room_id and site_name=@site_name";
            command.Parameters.AddWithValue("@site_name", siteName);
            command.Parameters.AddWithValue("@room_id", roomId);
            var result = command.ExecuteScalar();
            if (result == null)
            {
                return null;
            }
            return (long)result;
        }
        public static void DeleteHistory(long id)
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "DELETE FROM History WHERE id=@id";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
          
        }
        public static void DeleteHistory()
        {
            SqliteCommand command = new SqliteCommand();
            command.Connection = db;
            command.CommandText = "DELETE FROM History";
            command.ExecuteNonQuery();

        }
        public async static Task<List<HistoryItem>> GetHistory()
        {
            List<HistoryItem> favoriteItems = new List<HistoryItem>();
            SqliteCommand command = new SqliteCommand("SELECT * FROM History ORDER BY watch_time DESC", db);
            var reader =await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                favoriteItems.Add(new HistoryItem()
                {
                    ID= reader.GetInt32(0),
                    RoomID = reader.GetString(4),
                    Photo = reader.GetString(3),
                    SiteName = reader.GetString(2),
                    UserName = reader.GetString(1),
                    WatchTime= reader.GetDateTime(5)
                });
            }
            return favoriteItems;
        }

    }


}
