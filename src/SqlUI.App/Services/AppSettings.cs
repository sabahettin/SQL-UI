namespace SqlUI.App.Services;

public class AppSettings
{
    public string Theme { get; set; } = ThemeService.Light;
    public string Language { get; set; } = "tr"; // "tr" | "en"
    public string EditorFontFamily { get; set; } = "Consolas";
    public double EditorFontSize { get; set; } = 13;
    public double WindowWidth { get; set; } = 1300;
    public double WindowHeight { get; set; } = 750;

    // Sadece diskteki bir .sql dosyasindan acilmis sekmelerin yollari saklanir; icerigi
    // henuz kaydedilmemis "SQLQuery1.sql" gibi gecici sekmeler oturumlar arasi tasinmaz.
    public List<string> LastOpenFiles { get; set; } = new();
}
