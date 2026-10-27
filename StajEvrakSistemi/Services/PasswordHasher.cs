using System.Security.Cryptography;
using System.Text;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Şifreleri rastgele tuz (salt) + SHA-256 ile hashler.
/// Saklanan biçim: "Base64(salt):Base64(hash)".
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const char Separator = ':';

    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Şifre boş olamaz.", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = ComputeHash(password, salt);

        return $"{Convert.ToBase64String(salt)}{Separator}{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split(Separator);
        if (parts.Length != 2)
            return false;

        try
        {
            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] expected = Convert.FromBase64String(parts[1]);
            byte[] actual = ComputeHash(password, salt);

            // Süre farkından bilgi sızmasın diye sabit zamanlı karşılaştırma.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] salted = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, salted, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, salted, salt.Length, passwordBytes.Length);

        return SHA256.HashData(salted);
    }
}
