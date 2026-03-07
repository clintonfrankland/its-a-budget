using System.Security.Cryptography;
using System.Text;

namespace ClintonFrankland.Services;

public static class PasswordUtility
{
    public static string GenerateStrongPassword(int length = 16)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        foreach (var b in bytes)
            sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    public static string CreateSalt(int size = 16)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(size));

    public static string HashPassword(string password, string salt)
    {
        var saltBytes = TryGetSaltBytes(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string salt, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        var computed = HashPassword(password, salt);
        if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(storedHash)))
            return true;

        // Legacy fallback: allow plain-text match if existing data is not hashed yet.
        return string.Equals(password, storedHash, StringComparison.Ordinal);
    }

    private static byte[] TryGetSaltBytes(string salt)
    {
        try
        {
            return Convert.FromBase64String(salt);
        }
        catch
        {
            return Encoding.UTF8.GetBytes(salt);
        }
    }
}
