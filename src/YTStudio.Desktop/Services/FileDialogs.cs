using Photino.NET;
using YTStudio.Core.Vault;

namespace YTStudio.Desktop.Services;

/// <summary>Janelas nativas de abrir/salvar arquivo.</summary>
public interface IFileDialogs
{
    string? PickOpen();
    string? PickSave();
}

public sealed class PhotinoFileDialogs : IFileDialogs
{
    private static readonly (string Name, string[] Extensions)[] Filters = [("Arquivo do YTStudio", [VaultFile.Extension])];

    public PhotinoWindow? Window { get; set; }

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
}
