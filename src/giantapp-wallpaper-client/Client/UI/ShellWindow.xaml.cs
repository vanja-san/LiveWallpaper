﻿using Client.Apps.Configs;
using Client.Libs;
using Client.UI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace GiantappWallpaper;

class ShellConfig
{
    public double Width { get; set; }
    public double Height { get; set; }
}

public partial class ShellWindow : Window
{
    private static Logger _logger = LogManager.GetCurrentClassLogger();

    #region properties
    public static ShellWindow? Instance { get; private set; }
    public static object? ClientApi { get; set; }

    public static bool DarkBackground { get; set; }

    public static Dictionary<string, string> CustomFolderMapping { get; set; } = new();

    #endregion

    public ShellWindow()
    {
        var appearance = Configer.Get<Appearance>() ?? new();
        if (appearance.Mode == "system")
        {
            //监控系统主题变化
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General)
                {
                    Debouncer.Shared.Delay(() =>
                    {
                        SetTheme(appearance.Theme, appearance.Mode);
                    }, 1000);
                }
            };
        }

        InitializeComponent();
        SizeChanged += ShellWindow_SizeChanged;
        if (DarkBackground)
        {
            webview2.DefaultBackgroundColor = Color.FromKnownColor(KnownColor.Black);
        }
        webview2.CoreWebView2InitializationCompleted += Webview2_CoreWebView2InitializationCompleted;
        var config = Configer.Get<ShellConfig>();
        const float defaultWidth = 1024;
        const float defaultHeight = 680;
        if (config != null)
        {
            if (config.Width <= 800 || config.Height <= 482)
            {
                Width = defaultWidth;
                Height = defaultHeight;
            }
            else
            {
                Width = config.Width;
                Height = config.Height;
            }
        }
        else
        {
            Width = defaultWidth;
            Height = defaultHeight;
        }
    }

    #region public

    public static bool ShouldAppsUseDarkMode()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                int appsUseLightTheme = (int)key.GetValue("AppsUseLightTheme", -1);
                if (appsUseLightTheme == 0)
                {
                    // 当前使用暗色主题
                    return true;
                }
                else if (appsUseLightTheme == 1)
                {
                    // 当前使用亮色主题
                    return false;
                }
                else
                {
                    // 无法确定当前主题
                }
                key.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.Info(ex);
        }
        return true;
    }


    public static void SetTheme(string theme, string mode)
    {
        //首字母大写
        theme = theme.First().ToString().ToUpper() + theme[1..];
        if (mode == "system")
        {
            bool darkMode = ShouldAppsUseDarkMode();
            if (!darkMode)
            {
                mode = "light";
            }
            else
            {
                mode = "dark";
            }
        }
        ResourceDictionary appResources = Application.Current.Resources;
        var old = appResources.MergedDictionaries.FirstOrDefault(x => x.Source?.ToString().Contains("/LiveWallpaper3;component/UI/Themes") == true);
        if (old != null)
        {
            appResources.MergedDictionaries.Remove(old);
        }
        ResourceDictionary themeDict = new()
        {
            Source = new Uri($"/LiveWallpaper3;component/UI/Themes/{mode}/{theme}.xaml", UriKind.RelativeOrAbsolute)
        };
        DarkBackground = mode == "dark";
        appResources.MergedDictionaries.Add(themeDict);
    }

    public static async void ShowShell(string? url)
    {
        _logger.Info($"ShowShell {url}");
        Instance ??= new ShellWindow();

        bool ok = await Task.Run(CheckWebView2);
        if (!ok)
        {
            //没装webview2
            Instance.loading.Visibility = Visibility.Collapsed;
            Instance.tips.Visibility = Visibility.Visible;

            LoopCheckWebView2(url);
        }
        else
        {
            Instance.loading.Visibility = Visibility.Visible;
        }

        if (Instance.WindowState == WindowState.Minimized)
            Instance.WindowState = WindowState.Normal;

        if (Instance.Visibility == Visibility.Visible && !Instance.IsActive)
            Instance.Activate();

        Instance.webview2.Source = new Uri(url);
        Instance.webview2.NavigationCompleted += NavigationCompleted;
        Instance.Show();
    }

    #endregion

    #region private
    //每秒检查1次，直到成功或者窗口关闭
    private static void LoopCheckWebView2(string? url)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);
                if (Instance == null)
                {
                    break;
                }
                bool ok = await Task.Run(CheckWebView2);
                if (ok)
                {
                    Instance.Dispatcher.Invoke(() =>
                    {
                        ShowShell(url);
                    });
                    break;
                }
            }
        });
    }

    static bool CheckWebView2()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return true;
        }
        catch (WebView2RuntimeNotFoundException e)
        {
            Debug.WriteLine(e);
        }
        return false;
    }
    #endregion

    #region callback

    private void ShellWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            return;
        }
        //记录窗口大小
        ShellConfig config = new()
        {
            Width = Width,
            Height = Height
        };
        Configer.Set(config);
    }

    private static void NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (Instance != null)
        {
            Instance.webview2.NavigationCompleted -= NavigationCompleted;
            Instance.loading.Visibility = Visibility.Collapsed;
        }
    }

    private void DownloadHyperlink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
            var setupPath = Path.Combine(dir, "Assets/MicrosoftEdgeWebview2Setup.exe");
            Process.Start(new ProcessStartInfo(setupPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SizeChanged -= ShellWindow_SizeChanged;
        webview2.CoreWebView2InitializationCompleted -= Webview2_CoreWebView2InitializationCompleted;
        //webview2.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
        Instance = null;
        base.OnClosed(e);
        Configer.Save();
    }


    private void Webview2_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (ClientApi != null)
        {
            webview2.CoreWebView2?.AddHostObjectToScript("api", new Client.Apps.ApiObject());
            webview2.CoreWebView2?.AddHostObjectToScript("shell", new ShellApiObject());
        }
        //webview2.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

        foreach (var item in CustomFolderMapping)
        {
            if (!Directory.Exists(item.Value))
                continue;
            webview2.CoreWebView2?.SetVirtualHostNameToFolderMapping(item.Key, item.Value, CoreWebView2HostResourceAccessKind.DenyCors);
        }

#if !DEBUG
        if (webview2.CoreWebView2 != null)
        {
            //禁用F12
            webview2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            //禁用右键菜单
            webview2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            //左下角提示
            webview2.CoreWebView2.Settings.IsStatusBarEnabled = false;
        }
#endif
    }

    //private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    //{
    //    var webView = sender as Microsoft.Web.WebView2.Wpf.WebView2;
    //    var msg = e.TryGetWebMessageAsString();
    //}

    #endregion

}
