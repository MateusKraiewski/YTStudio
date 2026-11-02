using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YTStudio.Core.Data;

namespace YTStudio.Core.Vault;

/// <summary>
/// Um arquivo .ytv aberto: um banco SQLite inteiro cifrado (AES-256, esquema SQLCipher 4)
/// com a chave derivada da senha mestra por Argon2id. Os 16 primeiros bytes do arquivo são o salt
/// (o próprio SQLCipher guarda ali o salt que recebe na criação), então não existe arquivo extra.
/// </summary>
public sealed class VaultFile : IDisposable
{
    public const string Extension = ".ytv";

    /// <summary>"YTST" — marca o arquivo como do YTStudio (PRAGMA application_id).</summary>
    internal const int ApplicationId = 0x59545354;

    private const int SqliteNotADatabase = 26;

    private byte[]? _key;
    private readonly DbContextOptions<StudioDbContext> _options;

    public string Path { get; }

    static VaultFile() => SQLitePCL.Batteries_V2.Init();

    private VaultFile(string path, byte[] key)
    {
        Path = path;
        _key = key;
        _options = new DbContextOptionsBuilder<StudioDbContext>()
            .UseSqlite(ConnectionString(path))
            .AddInterceptors(new KeyInterceptor(this))
            .Options;
    }

    /// <summary>Cria um arquivo novo. Falha se o caminho já existir, para nunca sobrescrever dados.</summary>
    public static VaultFile Create(string path, string password)
    {
        path = System.IO.Path.GetFullPath(path);
        if (password.Length < VaultKey.MinPasswordLength)
            throw new VaultException($"A senha mestra precisa ter pelo menos {VaultKey.MinPasswordLength} caracteres.");
        if (File.Exists(path))
            throw new VaultException("Já existe um arquivo com esse nome. Escolha outro nome ou abra o arquivo existente.");

        var salt = VaultKey.NewSalt();
        var key = VaultKey.Derive(password, salt);
        try
        {
            // Na criação a chave vai junto com o salt, para o SQLCipher gravar o NOSSO salt no cabeçalho.
            using (var conn = new SqliteConnection(ConnectionString(path)))
            {
                conn.Open();
                ApplyKey(conn, Convert.ToHexString(key) + Convert.ToHexString(salt));
                Exec(conn, $"PRAGMA application_id = {ApplicationId};");
            }

            var vault = new VaultFile(path, key);
            using (var db = vault.CreateContext()) db.Database.Migrate();
            return vault;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            TryDelete(path);
            throw;
        }
    }

    /// <summary>Abre um arquivo existente com a senha mestra.</summary>
    public static VaultFile Open(string path, string password)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!File.Exists(path)) throw new VaultException("Arquivo não encontrado.");
        if (string.IsNullOrEmpty(password)) throw new VaultException("Digite a senha mestra.");

        var salt = new byte[VaultKey.SaltSize];
        using (var fs = File.OpenRead(path))
        {
            if (fs.Length < 1024 || fs.ReadAtLeast(salt, salt.Length, throwOnEndOfStream: false) != salt.Length)
                throw new VaultException("Esse arquivo não é um arquivo do YTStudio.");
        }

        var key = VaultKey.Derive(password, salt);
        try
        {
            using (var conn = new SqliteConnection(ConnectionString(path)))
            {
                conn.Open();
                ApplyKey(conn, Convert.ToHexString(key));
                long appId;
                try
                {
                    appId = Convert.ToInt64(Scalar(conn, "PRAGMA application_id;"));
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteNotADatabase)
                {
                    throw new VaultException("Senha incorreta, ou o arquivo não é do YTStudio.", ex);
                }
                if (appId != ApplicationId) throw new VaultException("Esse arquivo não é um arquivo do YTStudio.");
            }

            var vault = new VaultFile(path, key);
            using (var db = vault.CreateContext()) db.Database.Migrate(); // atualiza arquivos de versões antigas
            return vault;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    public StudioDbContext CreateContext()
    {
        ObjectDisposedException.ThrowIf(_key is null, this);
        return new StudioDbContext(_options);
    }

    /// <summary>Fecha o arquivo e apaga a chave da memória.</summary>
    public void Dispose()
    {
        var key = Interlocked.Exchange(ref _key, null);
        if (key is not null) CryptographicOperations.ZeroMemory(key);
    }

    private static string ConnectionString(string path) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadWriteCreate,
        // Sem pool: cada conexão recebe a chave ao abrir e nenhuma conexão destravada sobra depois do bloqueio.
        Pooling = false,
    }.ToString();

    private static void ApplyKey(DbConnection conn, string hexKey)
    {
        // A ordem importa: escolher o esquema antes de passar a chave.
        Exec(conn, "PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4;");
        Exec(conn, $"PRAGMA key = \"x'{hexKey}'\";");
    }

    private static void Exec(DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); File.Delete(path + "-journal"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class KeyInterceptor(VaultFile vault) : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Apply(connection);

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Apply(connection);
            return Task.CompletedTask;
        }

        private void Apply(DbConnection connection)
        {
            var key = vault._key ?? throw new VaultException("O arquivo está bloqueado. Abra de novo com a senha mestra.");
            ApplyKey(connection, Convert.ToHexString(key));
        }
    }
}
