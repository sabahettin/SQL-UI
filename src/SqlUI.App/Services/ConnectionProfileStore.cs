using System.IO;
using System.Text.Json;
using SqlUI.Core.Models;

namespace SqlUI.App.Services;

// Baglanti profillerini %AppData%\SqlUI\connections.json'da tutar. En son kullanilanlar
// en basta, en fazla 10 profil saklanir (SSMS'in "Recent Connections" davranisi).
public class ConnectionProfileStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SqlUI", "connections.json");

    public List<ConnectionProfile> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<ConnectionProfile>();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<ConnectionProfile>>(json) ?? new List<ConnectionProfile>();
        }
        catch
        {
            return new List<ConnectionProfile>();
        }
    }

    public void SaveRecent(ConnectionProfile profile, string? plainSecret)
    {
        var profiles = Load();
        profiles.RemoveAll(p => p.ProviderKey == profile.ProviderKey
            && string.Equals(p.Server, profile.Server, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.UserName, profile.UserName, StringComparison.OrdinalIgnoreCase));

        profile.LastUsedUtc = DateTime.UtcNow;
        profile.EncryptedSecret = plainSecret is null ? null : SecretProtector.Protect(plainSecret);
        profiles.Insert(0, profile);
        if (profiles.Count > 10) profiles.RemoveRange(10, profiles.Count - 10);

        Persist(profiles);
    }

    public void Delete(string id)
    {
        var profiles = Load();
        profiles.RemoveAll(p => p.Id == id);
        Persist(profiles);
    }

    private static void Persist(List<ConnectionProfile> profiles)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string? DecryptSecret(ConnectionProfile profile)
        => profile.EncryptedSecret is null ? null : SecretProtector.Unprotect(profile.EncryptedSecret);
}
