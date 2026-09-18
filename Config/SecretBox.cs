using System.Security.Cryptography;
using System.Text;

namespace SshTunnelTray.Config;

public sealed record EncryptedSecret(string Ciphertext, string Nonce, string Tag);

public static class SecretBox
{
    private static readonly byte[] Key = SHA256.HashData(Encoding.UTF8.GetBytes("SshTunnelTray configuration secret v1"));
    public static EncryptedSecret Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext); var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var bytes = Encoding.UTF8.GetBytes(plaintext); var cipher = new byte[bytes.Length];
        using var aes = new AesGcm(Key, 16); aes.Encrypt(nonce, bytes, cipher, tag); CryptographicOperations.ZeroMemory(bytes);
        return new(Convert.ToBase64String(cipher), Convert.ToBase64String(nonce), Convert.ToBase64String(tag));
    }
    public static string Decrypt(EncryptedSecret secret)
    {
        var cipher = Convert.FromBase64String(secret.Ciphertext); var nonce = Convert.FromBase64String(secret.Nonce); var tag = Convert.FromBase64String(secret.Tag); if (nonce.Length != 12 || tag.Length != 16) throw new FormatException("Invalid AES-GCM nonce or tag length."); var plain = new byte[cipher.Length];
        using var aes = new AesGcm(Key, 16); aes.Decrypt(nonce, cipher, tag, plain); try { return Encoding.UTF8.GetString(plain); } finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
