using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace YTStudio.Core.Vault;

/// <summary>
/// Deriva a chave de 256 bits do arquivo a partir da senha mestra com Argon2id.
/// Os parâmetros são fixos na versão 1 do formato; se mudarem, o formato ganha uma versão nova.
/// </summary>
public static class VaultKey
{
    public const int KeySize = 32;
    public const int SaltSize = 16;
    public const int MemoryKiB = 64 * 1024;
    public const int Iterations = 3;
    public const int Parallelism = 4;
    public const int MinPasswordLength = 12;

    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    public static byte[] Derive(string password, byte[] salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (salt.Length != SaltSize) throw new ArgumentException($"O salt precisa ter {SaltSize} bytes.", nameof(salt));

        var pwd = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon = new Argon2id(pwd)
            {
                Salt = salt,
                MemorySize = MemoryKiB,
                Iterations = Iterations,
                DegreeOfParallelism = Parallelism,
            };
            return argon.GetBytes(KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pwd);
        }
    }
}
