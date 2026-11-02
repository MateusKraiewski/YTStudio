using System.Diagnostics;
using Photino.NET;
using YTStudio.Core.Vault;

namespace YTStudio.Desktop.Services;

/// <summary>O que a API pede à janela e ao sistema: escolher arquivos, abrir links, fechar o app.</summary>
public interface IDesktopShell
{
    string? PickOpen();
    string? PickSave();
    void OpenUrl(string url);
    void Notify(string title, string body);
    void Quit();
}

public sealed class PhotinoShell : IDesktopShell
{
    private static readonly (string Name, string[] Extensions)[] Filters = [("Arquivo do YTStudio", [VaultFile.Extension])];

    public PhotinoWindow? Window { get; set; }

    /// <summary>Usado em --no-window, quando não há janela para fechar.</summary>
    public Action? OnQuit { get; set; }

    public string? PickOpen()
    {
        var files = Window?.ShowOpenFile("Abrir arquivo do YTStudio", null, false, Filters);
        return files is { Length: > 0 } ? files[0] : null;
    }

    public string? PickSave()
    {
        var path = Window?.ShowSaveFile("Criar arquivo do YTStudio", null, Filters);
        if (string.IsNullOrWhiteSpace(path)) return null;
        return path.EndsWith(VaultFile.Extension, StringComparison.OrdinalIgnoreCase) ? path : path + VaultFile.Extension;
    }

    public void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Só links https podem ser abertos.", nameof(url));

        if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        else if (OperatingSystem.IsMacOS()) Process.Start("open", uri.AbsoluteUri);
        else Process.Start("xdg-open", uri.AbsoluteUri);
    }

    public void Notify(string title, string body) => Window?.SendNotification(title, body);

    public void Quit()
    {
        if (Window is { } w) w.Invoke(w.Close);
        else OnQuit?.Invoke();
    }
}
