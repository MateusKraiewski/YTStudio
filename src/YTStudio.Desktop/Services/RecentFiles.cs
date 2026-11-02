using System.Text.Json;

namespace YTStudio.Desktop.Services;

/// <summary>
/// Lista dos últimos arquivos .ytv abertos. Guarda só os caminhos (nada secreto),
/// em %APPDATA%\YTStudio no Windows, ~/Library/Application Support/YTStudio no Mac e ~/.config/YTStudio no Linux.
/// </summary>
public sealed class RecentFiles(string? settingsDir = null)
{
    private const int Max = 8;
    private readonly string _file = Path.Combine(
        settingsDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YTStudio"),
        "recent.json");
    private readonly Lock _gate = new();

    public IReadOnlyList<string> List()
    {
        lock (_gate) return Load().Where(File.Exists).ToList();
    }

    public void Add(string path)
    {
        lock (_gate)
        {
            var list = Load();
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            Save(list.Take(Max).ToList());
        }
    }

    public void Remove(string path)
    {
        lock (_gate)
        {
            var list = Load();
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            Save(list);
        }
    }

    private List<string> Load()
    {
        try
        {
            return File.Exists(_file) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_file)) ?? [] : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(list));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Lista de recentes é conveniência; falhar aqui não pode travar o app.
        }
    }
}
