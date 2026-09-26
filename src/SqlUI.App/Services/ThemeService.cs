using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SqlUI.App.Services;

public static class ThemeService
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    public static string Current { get; private set; } = Light;

    public static void Apply(string theme)
    {
        Current = theme;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeDict = dictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));
        if (themeDict != null) dictionaries.Remove(themeDict);

        string file = theme == Dark ? "DarkTheme.xaml" : "LightTheme.xaml";
        dictionaries.Insert(0, new ResourceDictionary { Source = new Uri($"Themes/{file}", UriKind.Relative) });

        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
    }

    // Pencere basligi (OS'un cizdigi kisim) WPF kaynaklarindan etkilenmez; koyu yapmak icin
    // Windows 10 20H1+/11'deki DWMWA_USE_IMMERSIVE_DARK_MODE ozniteligi kullanilir.
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int useDark = Current == Dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
