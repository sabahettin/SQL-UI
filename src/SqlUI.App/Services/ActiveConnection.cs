using SqlUI.Core;

namespace SqlUI.App.Services;

// Uygulamanin o an bagli oldugu sunucuyu tutan tekil (singleton) durum nesnesi.
// ConnectionDialog basarili baglanti sonrasi bunu doldurur, MainWindow burayi okur.
public class ActiveConnection
{
    public IDbProvider Provider { get; set; } = null!;
    public string ConnectionString { get; set; } = "";
    public string? Database { get; set; }
    public string ServerLabel { get; set; } = "";
}
