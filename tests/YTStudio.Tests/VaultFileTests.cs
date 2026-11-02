using System.Text;
using Microsoft.EntityFrameworkCore;
using YTStudio.Core.Models;
using YTStudio.Core.Vault;

namespace YTStudio.Tests;

public sealed class VaultFileTests : IDisposable
{
    private const string Password = "senha-mestra-de-teste-123";
    private readonly string _dir = Directory.CreateTempSubdirectory("ytstudio-tests-").FullName;

    private string NewPath() => Path.Combine(_dir, Guid.NewGuid().ToString("N") + VaultFile.Extension);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Dados_salvos_voltam_ao_reabrir_com_a_senha_certa()
    {
        var path = NewPath();
        using (var vault = VaultFile.Create(path, Password))
        using (var db = vault.CreateContext())
        {
            db.Channels.Add(new Channel { Name = "Canal de Carros", Niche = "Carros" });
            db.SaveChanges();
        }

        using var reopened = VaultFile.Open(path, Password);
        using var db2 = reopened.CreateContext();
        var channel = Assert.Single(db2.Channels.AsNoTracking().ToList());
        Assert.Equal("Canal de Carros", channel.Name);
    }

    [Fact]
    public void Arquivo_nao_tem_nada_em_texto_puro()
    {
        var path = NewPath();
        using (var vault = VaultFile.Create(path, Password))
        using (var db = vault.CreateContext())
        {
            db.Channels.Add(new Channel { Name = "NOME-SECRETO-DO-CANAL", Notes = "anotacao-secreta" });
            db.SaveChanges();
        }

        var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        Assert.DoesNotContain("NOME-SECRETO-DO-CANAL", text);
        Assert.DoesNotContain("anotacao-secreta", text);
        Assert.DoesNotContain("SQLite format 3", text);
        Assert.DoesNotContain("channels", text);
    }

    [Fact]
    public void Senha_errada_e_recusada()
    {
        var path = NewPath();
        VaultFile.Create(path, Password).Dispose();

        var ex = Assert.Throws<VaultException>(() => VaultFile.Open(path, Password + "x"));
        Assert.Contains("Senha incorreta", ex.Message);
    }

    [Fact]
    public void Nao_sobrescreve_arquivo_existente()
    {
        var path = NewPath();
        File.WriteAllText(path, "dados importantes");

        Assert.Throws<VaultException>(() => VaultFile.Create(path, Password));
        Assert.Equal("dados importantes", File.ReadAllText(path));
    }

    [Fact]
    public void Senha_curta_e_recusada_na_criacao()
    {
        var path = NewPath();
        Assert.Throws<VaultException>(() => VaultFile.Create(path, "curta"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Arquivo_qualquer_e_recusado()
    {
        var path = NewPath();
        File.WriteAllBytes(path, new byte[4096]);
        Assert.Throws<VaultException>(() => VaultFile.Open(path, Password));
    }

    [Fact]
    public void Depois_de_bloquear_nao_da_para_ler()
    {
        var path = NewPath();
        var vault = VaultFile.Create(path, Password);
        vault.Dispose();
        Assert.Throws<ObjectDisposedException>(() => vault.CreateContext());
    }

    [Fact]
    public void Chave_depende_do_salt_e_da_senha()
    {
        var salt = VaultKey.NewSalt();
        var a = VaultKey.Derive(Password, salt);
        Assert.Equal(VaultKey.KeySize, a.Length);
        Assert.Equal(a, VaultKey.Derive(Password, salt));
        Assert.NotEqual(a, VaultKey.Derive(Password, VaultKey.NewSalt()));
        Assert.NotEqual(a, VaultKey.Derive(Password + "!", salt));
    }
}
