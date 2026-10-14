using PlayWin.WinUI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlayWin.WinUI;
public partial class App : Application
{
    public static string DisplayName => (string)Current.Resources["AppDisplayName"];
    public static List<MainWindow> Windows { get; } = new();
    public static MainWindow ActiveWindow { get; private set; }
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            if (Environment.GetCommandLineArgs().Contains("--category-repro")) CategoryRepro.RecordError(e.Exception);
            LogHelper.Log("应用错误", LogType.ERROR, e.Exception);
            Utils.ShowMessageToast(e.Message);
            e.Handled = true;
        };
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Environment.GetCommandLineArgs().Contains("--smoke-test") || Environment.GetCommandLineArgs().Contains("--category-repro"))
        {
            var window = new MainWindow();
            RegisterWindow(window);
            try
            {
                if (Environment.GetCommandLineArgs().Contains("--category-repro")) await CategoryRepro.RunAsync(window);
                else if (Environment.GetCommandLineArgs().Contains("--browse-preferences-check")) BrowseChecks.VerifyRestart();
                else if (Environment.GetCommandLineArgs().Contains("--update-download-check")) await UpdateChecks.LiveAsync();
                else await SmokeTests.RunAsync();
            }
            catch (Exception ex)
            {
                if (!Environment.GetCommandLineArgs().Contains("--smoke-test")) throw;
                var reports = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayWin"));
                File.WriteAllText(Path.Combine(reports.FullName, "smoke-test.log"), "FAIL Smoke test interrupted\n" + ex);
                Environment.ExitCode = 1;
            }
            finally { window.Close(); }
            return;
        }
        try
        {
            await DouyinAccount.Instance.InitLoginInfo();
            await DatabaseHelper.InitializeDatabase();
            OpenWindow(typeof(BaseFramePage), args.Arguments);
        }
        catch (Exception ex)
        {
            LogHelper.Log("启动失败", LogType.FATAL, ex);
            var window = new MainWindow();
            RegisterWindow(window);
            window.Root.Content = new TextBlock { Text = "启动失败：" + ex.Message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
            window.Activate();
        }
    }
    public static MainWindow OpenWindow(Type page, object data = null)
    {
        var window = new MainWindow(page == typeof(Views.LiveRoomPage));
        RegisterWindow(window);
        window.Root.Navigate(page, data);
        window.Activate();
        return window;
    }
    private static void RegisterWindow(MainWindow window)
    {
        Windows.Add(window);
        ActiveWindow = window;
        if (!Environment.GetCommandLineArgs().Contains("--smoke-test"))
        {
            var douyin = (PlayWin.Core.Douyin)ViewModels.MainVM.Sites.First(s => s.LiveSite is PlayWin.Core.Douyin).LiveSite;
            douyin.WebRequestAsync ??= new DouyinWebRequest().GetAsync;
        }
        window.Activated += (_, _) => ActiveWindow = window;
        window.Closed += (_, _) =>
        {
            Windows.Remove(window);
            ActiveWindow = Windows.LastOrDefault();
        };
    }
    public static MainWindow GetWindowFor(UIElement element) => Windows.FirstOrDefault(w => w.Root.XamlRoot != null && w.Root.XamlRoot == element.XamlRoot) ?? ActiveWindow;
    public static void SetTitleBar()
    {
        foreach (var window in Windows)
            window.Root.RequestedTheme = (ElementTheme)SettingHelper.GetValue(SettingHelper.THEME, 0);
    }
    public static void SetWindowSize(string value)
    {
        value = MainWindow.ValidateWindowSize(value);
        SettingHelper.SetValue(SettingHelper.WINDOW_SIZE, value);
        foreach (var window in Windows) window.ApplyWindowSize(value);
    }
}
