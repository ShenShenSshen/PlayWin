namespace PlayWin.WinUI.ViewModels
{
    public class BaseNotifyPropertyChanged { public virtual void DoPropertyChanged(string name) { } }
}
namespace PlayWin.WinUI.Helper
{
    // The production database/settings implementations run against an isolated profile.
    public static class AppPaths
    {
        public static string DataDirectory { get; } = Environment.GetEnvironmentVariable("PLAYWIN_TEST_DIR") ?? Path.Combine(Path.GetTempPath(), "PlayWin-WinUI-check-" + Guid.NewGuid());
        public static string EnsureDirectory() => Directory.CreateDirectory(DataDirectory).FullName;
    }
}

namespace PlayWin.WinUI.Helper
{
    public enum LogType { INFO, DEBUG, ERROR, FATAL }
    public static class LogHelper { public static void Log(string message, LogType type, Exception ex = null) => System.Diagnostics.Debug.WriteLine(message); }
}
