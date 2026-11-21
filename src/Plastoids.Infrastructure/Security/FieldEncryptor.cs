using System.Security.Cryptography;
using System.Text;
using Plastoids.Infrastructure.Secrets;

namespace Plastoids.Infrastructure.Security;

/// <summary>
/// Column-level encryption for personal data (blueprint §16/§19: NIN, DOB, phone). AES-256-GCM with a key from the
/// secret provider, plus an HMAC-SHA256 blind index so encrypted columns can still be looked up for equality.
/// Ciphertext format: base64( keyId(1) | nonce(12) | tag(16) | ciphertext ).
/// </summary>
public interface IFieldEncryptor
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);

    /// <summary>Deterministic keyed hash of the normalised value, for unique indexes and lookups.</summary>
    string BlindIndex(string value);
}

internal sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    public const string EncryptionKeyName = "FieldEncryptionKey";
    public const string BlindIndexKeyName = "BlindIndexKey";
    private const byte KeyId = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;
    private readonly byte[] _indexKey;

    public AesGcmFieldEncryptor(ISecretProvider secrets)
    {
        _key = Convert.FromBase64String(secrets.GetRequired(EncryptionKeyName));
        _indexKey = Convert.FromBase64String(secrets.GetRequired(BlindIndexKeyName));
        if (_key.Length != 32)
        {
            throw new InvalidOperationException($"Secrets:{EncryptionKeyName} must be 32 bytes (base64).");
        }
    }

    public string Encrypt(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[1 + NonceSize + TagSize + plain.Length];
        output[0] = KeyId;
        var nonce = output.AsSpan(1, NonceSize);
        var tag = output.AsSpan(1 + NonceSize, TagSize);
        var cipher = output.AsSpan(1 + NonceSize + TagSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        var input = Convert.FromBase64String(ciphertext);
        if (input.Length < 1 + NonceSize + TagSize || input[0] != KeyId)
        {
            throw new CryptographicException("Unknown field-encryption key id or malformed ciphertext.");
        }

        var nonce = input.AsSpan(1, NonceSize);
        var tag = input.AsSpan(1 + NonceSize, TagSize);
        var cipher = input.AsSpan(1 + NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    public string BlindIndex(string value)
        => Convert.ToHexString(HMACSHA256.HashData(_indexKey, Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant())));
}
