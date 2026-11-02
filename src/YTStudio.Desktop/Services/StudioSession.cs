using YTStudio.Core.Data;
using YTStudio.Core.Vault;

namespace YTStudio.Desktop.Services;

/// <summary>O arquivo .ytv aberto agora (no máximo um por vez).</summary>
public sealed class StudioSession : IDisposable
{
    private readonly Lock _gate = new();
    private VaultFile? _vault;

    public string? OpenPath
    {
        get { lock (_gate) return _vault?.Path; }
    }

    public void Replace(VaultFile vault)
    {
        VaultFile? old;
        lock (_gate)
        {
            old = _vault;
            _vault = vault;
        }
        old?.Dispose();
    }

    public void Lock()
    {
        VaultFile? old;
        lock (_gate)
        {
            old = _vault;
            _vault = null;
        }
        old?.Dispose();
    }

    /// <summary>Contexto do banco do arquivo aberto, ou <c>null</c> se estiver bloqueado.</summary>
    public StudioDbContext? TryCreateContext()
    {
        lock (_gate)
        {
            try { return _vault?.CreateContext(); }
            catch (ObjectDisposedException) { return null; }
        }
    }

    public void Dispose() => Lock();
}
