using System.Security.Cryptography;
using System.Text;

public static class TokenHelper
{
    public static string GenerateSecureToken(int byteLength = 64)
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength));
    }

    public static string ComputeSha256(string value)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}