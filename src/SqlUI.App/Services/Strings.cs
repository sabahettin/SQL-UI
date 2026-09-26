namespace SqlUI.App.Services;

// Basit, elle yazilmis bir sozluk tabanli lokalizasyon. Tam kapsam degil (bazi derin hata
// mesajlari hala Turkce kalabilir) ama gorunur/etkilesimli UI'nin (toolbar, menuler, durum
// cubugu, ana pencere basliklari, baglanti dialogu) tamami TR/EN arasinda geciyor.
public static class Strings
{
    private static readonly Dictionary<string, (string Tr, string En)> Map = new()
    {
        // Toolbar
        ["NewQuery"] = ("Yeni Sorgu", "New Query"),
        ["Open"] = ("Aç...", "Open..."),
        ["Save"] = ("Kaydet", "Save"),
        ["Execute"] = ("Çalıştır (F5)", "Execute (F5)"),
        ["Cancel"] = ("İptal", "Cancel"),
        ["ExecutionPlan"] = ("Yürütme Planı (Ctrl+L)", "Execution Plan (Ctrl+L)"),
        ["NewDatabase"] = ("Yeni Veritabanı...", "New Database..."),
        ["Theme"] = ("Tema", "Theme"),
        ["Settings"] = ("Ayarlar", "Settings"),
        ["DatabaseLabel"] = ("Veritabanı:", "Database:"),
        ["ObjectExplorer"] = ("Nesne Gezgini", "Object Explorer"),

        // Durum cubugu / mesajlar
        ["Ready"] = ("Hazır", "Ready"),
        ["Executing"] = ("Çalıştırılıyor...", "Executing..."),
        ["QueryCompleted"] = ("Sorgu tamamlandı.", "Query completed."),
        ["QueryCancelled"] = ("Sorgu iptal edildi.", "Query cancelled."),
        ["QueryCancelledByUser"] = ("Sorgu kullanıcı tarafından iptal edildi.", "Query cancelled by user."),
        ["ErrorOccurred"] = ("Hata oluştu.", "An error occurred."),
        ["DataUpdated"] = ("Veri güncellendi.", "Data updated."),
        ["RowCountFormat"] = ("Satır: {0}", "Rows: {0}"),
        ["ElapsedFormat"] = ("Süre: {0}", "Duration: {0}"),

        // Sonuc/sekme basliklari
        ["Results"] = ("Sonuçlar", "Results"),
        ["Messages"] = ("Mesajlar", "Messages"),
        ["ExecutionPlanTab"] = ("Yürütme Planı", "Execution Plan"),
        ["ExportToExcel"] = ("Excel'e Aktar", "Export to Excel"),
        ["RowCountSuffix"] = ("satır", "rows"),

        // Object Explorer sag-tik
        ["SelectTop1000"] = ("İlk 1000 Satırı Seç", "Select Top 1000 Rows"),
        ["ScriptTableAs"] = ("Tabloyu Betikle", "Script Table as"),
        ["ScriptCreate"] = ("CREATE Olarak", "CREATE To"),
        ["ScriptSelect"] = ("SELECT Olarak", "SELECT To"),
        ["ScriptInsert"] = ("INSERT Olarak", "INSERT To"),
        ["ScriptDrop"] = ("DROP Olarak", "DROP To"),
        ["Delete"] = ("Sil", "Delete"),
        ["Refresh"] = ("Yenile", "Refresh"),
        ["Tasks"] = ("Görevler", "Tasks"),
        ["Backup"] = ("Yedekle (Backup)...", "Backup..."),
        ["Restore"] = ("Geri Yükle (Restore)...", "Restore..."),
        ["RenameMenu"] = ("Yeniden Adlandır", "Rename"),
        ["ModifySp"] = ("Düzenle (Modify)", "Modify"),
        ["ExecuteSp"] = ("Çalıştır (Execute)...", "Execute..."),

        // Sorgu sekmesi sag-tik
        ["SaveMenu"] = ("Kaydet", "Save"),
        ["SaveAsMenu"] = ("Farklı Kaydet...", "Save As..."),
        ["CloseMenu"] = ("Kapat", "Close"),
        ["CloseOthersMenu"] = ("Diğerlerini Kapat", "Close Others"),
        ["CloseAllMenu"] = ("Tümünü Kapat", "Close All"),

        // Baglanti dialogu
        ["ConnectTitle"] = ("SqlUI - Bağlan", "SqlUI - Connect"),
        ["EngineLabel"] = ("Veritabanı Motoru", "Database Engine"),
        ["ServerFileLabel"] = ("Sunucu / Dosya", "Server / File"),
        ["Browse"] = ("Gözat...", "Browse..."),
        ["DatabaseOptionalLabel"] = ("Veritabanı (opsiyonel)", "Database (optional)"),
        ["WindowsAuth"] = ("Windows Authentication", "Windows Authentication"),
        ["UserNameLabel"] = ("Kullanıcı Adı", "User Name"),
        ["PasswordLabel"] = ("Parola", "Password"),
        ["RecentConnections"] = ("Son Bağlantılar", "Recent Connections"),
        ["Connect"] = ("Bağlan", "Connect"),

        // Ayarlar penceresi
        ["SettingsTitle"] = ("Ayarlar", "Settings"),
        ["ThemeLabel"] = ("Tema", "Theme"),
        ["Light"] = ("Açık", "Light"),
        ["Dark"] = ("Koyu", "Dark"),
        ["LanguageLabel"] = ("Dil", "Language"),
        ["FontFamilyLabel"] = ("Editör Yazı Tipi", "Editor Font"),
        ["FontSizeLabel"] = ("Yazı Boyutu", "Font Size"),
        ["OkButton"] = ("Tamam", "OK"),
        ["CancelButton"] = ("İptal", "Cancel"),
    };

    public static string Get(string language, string key)
    {
        if (!Map.TryGetValue(key, out var pair)) return key;
        return language == "en" ? pair.En : pair.Tr;
    }
}
