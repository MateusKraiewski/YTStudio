using YTStudio.Desktop.Services;

namespace YTStudio.Tests;

public class UpdateTests
{
    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("0.10.3", "0.10.3")]
    [InlineData("v1.2", "1.2.0")]
    [InlineData("v1.2.3-beta.1", "1.2.3")]
    [InlineData("1.2.3+abc123", "1.2.3")]
    public void Le_versao_da_tag(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateService.ParseVersion(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void Tag_invalida_nao_vira_versao(string? tag) => Assert.Null(UpdateService.ParseVersion(tag));

    [Fact]
    public void Versao_maior_e_detectada_mesmo_com_dois_digitos() =>
        Assert.True(UpdateService.ParseVersion("v0.10.0") > UpdateService.ParseVersion("v0.9.0"));

    [Fact]
    public void Escolhe_o_instalador_do_sistema_atual()
    {
        var assets = new List<UpdateService.Asset>
        {
            new("YTStudio-0.2.0-windows-x64-setup.exe", 1, null, "https://x/w"),
            new("YTStudio-0.2.0-windows-x64-portable.zip", 1, null, "https://x/z"),
            new("YTStudio-0.2.0-macos-arm64.dmg", 1, null, "https://x/ma"),
            new("YTStudio-0.2.0-macos-x64.dmg", 1, null, "https://x/mx"),
            new("ytstudio_0.2.0_amd64.deb", 1, null, "https://x/d"),
            new("YTStudio-0.2.0-linux-x64.tar.gz", 1, null, "https://x/t"),
        };
        var picked = UpdateService.PickAsset(new UpdateService.Release("v0.2.0", "https://x", null, assets));
        Assert.NotNull(picked);
        if (OperatingSystem.IsWindows()) Assert.EndsWith("setup.exe", picked.Name);
        else if (OperatingSystem.IsMacOS()) Assert.EndsWith(".dmg", picked.Name);
        else Assert.EndsWith(".deb", picked.Name);
    }
}
