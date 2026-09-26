using System.Globalization;
using System.Windows;
using SqlUI.App.Services;

namespace SqlUI.App.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettingsStore _settingsStore;

    public SettingsWindow(AppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        InitializeComponent();

        var settings = _settingsStore.Load();
        // Kayitli degil, o an AKTIF tema (toolbar'daki Tema butonu kaydetmeden degistirebiliyor).
        ThemeCombo.SelectedIndex = ThemeService.Current == ThemeService.Dark ? 1 : 0;
        LanguageCombo.SelectedIndex = settings.Language == "en" ? 1 : 0;
        FontFamilyCombo.Text = settings.EditorFontFamily;
        FontSizeBox.Text = settings.EditorFontSize.ToString(CultureInfo.InvariantCulture);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string theme = ThemeCombo.SelectedIndex == 1 ? ThemeService.Dark : ThemeService.Light;
        string language = LanguageCombo.SelectedIndex == 1 ? "en" : "tr";
        string fontFamily = string.IsNullOrWhiteSpace(FontFamilyCombo.Text) ? "Consolas" : FontFamilyCombo.Text;
        double fontSize = double.TryParse(FontSizeBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var fs) && fs is >= 8 and <= 40 ? fs : 13;

        var settings = _settingsStore.Load();
        settings.Theme = theme;
        settings.Language = language;
        settings.EditorFontFamily = fontFamily;
        settings.EditorFontSize = fontSize;
        _settingsStore.Save(settings);

        // Aninda uygula - pencereyi yeniden baslatmaya gerek yok.
        ThemeService.Apply(theme);
        LocalizationService.Instance.Language = language;
        EditorSettings.Instance.FontFamily = fontFamily;
        EditorSettings.Instance.FontSize = fontSize;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
