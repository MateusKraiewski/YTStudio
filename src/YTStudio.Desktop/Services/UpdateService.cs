using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace YTStudio.Desktop.Services;

public sealed record UpdateInfo(string Current, string? Latest, bool Available, string? Notes, string? PageUrl, bool CanInstall);

/// <summary>
/// Procura versões novas nos Releases do GitHub (repositório público, sem login).
/// Quando há versão nova, o app baixa e instala sozinho (ver <see cref="InstallAsync"/>).
/// </summary>
public sealed class UpdateService(HttpClient http, IDesktopShell shell, ILogger<UpdateService> log)
{
    public const string Repo = "MateusKraiewski/YTStudio";
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private (DateTimeOffset At, Release? Release)? _cache;

    public static Version CurrentVersion { get; } = ParseVersion(
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion) ?? new Version(0, 0, 0);

    public async Task<UpdateInfo> CheckAsync(bool force, CancellationToken ct)
    {
        var release = await LatestAsync(force, ct);
        var latest = ParseVersion(release?.TagName);
        var available = latest is not null && latest > CurrentVersion;
        var asset = available ? PickAsset(release!) : null;
        return new UpdateInfo(
            Format(CurrentVersion), latest is null ? null : Format(latest), available,
            release?.Body, release?.HtmlUrl, asset is not null);
    }

    /// <summary>
    /// Baixa a versão nova, confere tamanho e SHA-256 e instala por cima, sem o usuário baixar nada:
    /// Windows roda o instalador em modo silencioso; Mac troca o YTStudio.app; Linux instala o .deb
    /// (pede a senha do sistema). Em todos o app fecha e abre de novo já atualizado.
    /// </summary>
    public async Task InstallAsync(CancellationToken ct)
    {
        var release = await LatestAsync(force: true, ct) ?? throw new UpdateException("Não foi possível consultar as versões agora.");
        var latest = ParseVersion(release.TagName);
        if (latest is null || latest <= CurrentVersion) throw new UpdateException("Você já está na versão mais recente.");
        var asset = PickAsset(release) ?? throw new UpdateException("Essa versão não tem instalador para o seu sistema.");

        var dir = Directory.CreateTempSubdirectory("ytstudio-update-").FullName;
        var file = Path.Combine(dir, asset.Name);
        await DownloadAsync(asset, file, ct);

        if (OperatingSystem.IsWindows())
        {
            // O instalador (Inno Setup) fecha o app, atualiza e abre de novo (ver packaging/windows).
            Process.Start(new ProcessStartInfo(file, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS") { UseShellExecute = true });
        }
        else if (OperatingSystem.IsMacOS())
        {
            var app = MacAppBundle() ?? throw new UpdateException("Instale o YTStudio pela imagem .dmg (arrastando para Aplicativos) para poder atualizar pelo app.");
            RunDetached(Path.Combine(dir, "update.sh"), MacScript(file, app));
        }
        else
        {
            if (!Path.Exists("/usr/bin/pkexec") || !Path.Exists("/usr/bin/dpkg"))
                throw new UpdateException("A atualização automática no Linux precisa do pkexec e do dpkg (Ubuntu/Debian).");
            RunDetached(Path.Combine(dir, "update.sh"), LinuxScript(file));
        }

        shell.Quit();
    }

    private async Task DownloadAsync(Asset asset, string file, CancellationToken ct)
    {
        await using (var stream = await http.GetStreamAsync(asset.DownloadUrl, ct))
        await using (var fs = File.Create(file))
            await stream.CopyToAsync(fs, ct);

        if (new FileInfo(file).Length != asset.Size) throw new UpdateException("O download veio incompleto. Tente de novo.");
        if (asset.Digest is { } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            await using var fs = File.OpenRead(file);
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(fs, ct));
            if (!string.Equals(hash, digest["sha256:".Length..], StringComparison.OrdinalIgnoreCase))
                throw new UpdateException("O arquivo baixado não confere com o publicado. A atualização foi cancelada.");
        }
    }

    /// <summary>Caminho do YTStudio.app quando o app roda de dentro de um bundle do Mac.</summary>
    private static string? MacAppBundle()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd('/'));
        return dir is { Name: "MacOS", Parent: { Name: "Contents", Parent: { } app } } && app.Name.EndsWith(".app") ? app.FullName : null;
    }

    private static string Quote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    // Os scripts esperam este processo terminar antes de mexer nos arquivos do app.
    private static string MacScript(string dmg, string app) => $"""
        #!/bin/bash
        while kill -0 {Environment.ProcessId} 2>/dev/null; do sleep 0.5; done
        mnt=$(mktemp -d)
        hdiutil attach -nobrowse -readonly -mountpoint "$mnt" {Quote(dmg)} || exit 1
        rm -rf {Quote(app + ".new")}
        if ditto "$mnt/YTStudio.app" {Quote(app + ".new")}; then
          rm -rf {Quote(app + ".old")}
          mv {Quote(app)} {Quote(app + ".old")} && mv {Quote(app + ".new")} {Quote(app)} && rm -rf {Quote(app + ".old")}
        fi
        hdiutil detach "$mnt" -quiet
        open {Quote(app)}
        """;

    private static string LinuxScript(string deb) => $"""
        #!/bin/bash
        while kill -0 {Environment.ProcessId} 2>/dev/null; do sleep 0.5; done
        pkexec dpkg -i {Quote(deb)}
        setsid ytstudio >/dev/null 2>&1 &
        """;

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static void RunDetached(string scriptPath, string script)
    {
        File.WriteAllText(scriptPath, script.Replace("\r\n", "\n"));
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Process.Start(new ProcessStartInfo("/bin/bash", ["-c", $"nohup {Quote(scriptPath)} >/dev/null 2>&1 &"]) { UseShellExecute = false });
    }

    private async Task<Release?> LatestAsync(bool force, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!force && _cache is { } c && DateTimeOffset.UtcNow - c.At < CacheFor) return c.Release;
            Release? release = null;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
                req.Headers.UserAgent.ParseAdd($"YTStudio/{Format(CurrentVersion)}");
                req.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var res = await http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode) release = await res.Content.ReadFromJsonAsync<Release>(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                log.LogInformation(ex, "Sem acesso aos releases agora");
                return _cache?.Release; // sem internet: não é erro, só não avisa
            }
            _cache = (DateTimeOffset.UtcNow, release);
            return release;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static Asset? PickAsset(Release release)
    {
        string suffix;
        if (OperatingSystem.IsWindows()) suffix = "-windows-x64-setup.exe";
        else if (OperatingSystem.IsMacOS()) suffix = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "-macos-arm64.dmg" : "-macos-x64.dmg";
        else suffix = "_amd64.deb";
        return release.Assets.FirstOrDefault(a => a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    internal static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().TrimStart('v', 'V');
        var cut = text.IndexOfAny(['-', '+']); // ignora "-beta" e "+commit"
        if (cut >= 0) text = text[..cut];
        return Version.TryParse(text, out var v) ? new Version(v.Major, v.Minor, Math.Max(v.Build, 0)) : null;
    }

    private static string Format(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";

    internal sealed record Release(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("assets")] List<Asset> Assets);

    internal sealed record Asset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("digest")] string? Digest,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
}

public sealed class UpdateException(string message) : Exception(message);
