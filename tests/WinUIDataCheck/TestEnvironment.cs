namespace AllLive.WinUI.ViewModels
{
    public class BaseNotifyPropertyChanged { public virtual void DoPropertyChanged(string name) { } }
}
namespace AllLive.WinUI.Helper
{
    // The production database/settings implementations run against an isolated profile.
    public static class AppPaths
    {
        public static string DataDirectory { get; } = Environment.GetEnvironmentVariable("ALLLIVE_TEST_DIR") ?? Path.Combine(Path.GetTempPath(), "AllLive-WinUI-check-" + Guid.NewGuid());
        public static string EnsureDirectory() => Directory.CreateDirectory(DataDirectory).FullName;
    }
}
