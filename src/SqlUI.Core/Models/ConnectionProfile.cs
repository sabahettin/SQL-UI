namespace SqlUI.Core.Models;

// Bir bağlantı profili diske kaydedilir (%AppData%\SqlUI\connections.json).
// Parola asla düz metin tutulmaz; EncryptedSecret alanı DPAPI ile şifrelenmiş baytları taşır.
public class ConnectionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string ProviderKey { get; set; } = ""; // "sqlserver", "sqlite", "postgres", "mysql"

    // SQL Server/Postgres/MySQL icin sunucu adresi; SQLite icin .db dosya yolu.
    public string Server { get; set; } = "";
    public string? Database { get; set; }

    public bool IntegratedSecurity { get; set; } = true;
    public string? UserName { get; set; }
    public byte[]? EncryptedSecret { get; set; }

    public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
}
