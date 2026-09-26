using System.Security.Cryptography;

namespace SqlUI.App.Services;

// SQL Authentication parolalari diske hic duz metin yazilmaz; DPAPI ile bu Windows
// kullanicisina/makineye bagli olarak sifrelenir. Baska bir kullanici/makinede
// connections.json dosyasi kopyalansa bile parolalar cozulemez.
public static class SecretProtector
{
    public static byte[] Protect(string plainText)
        => ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);

    public static string Unprotect(byte[] encrypted)
        => System.Text.Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser));
}
