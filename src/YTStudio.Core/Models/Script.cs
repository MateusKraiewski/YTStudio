namespace YTStudio.Core.Models;

/// <summary>Etapas de um vídeo, da ideia até a publicação.</summary>
public enum ScriptStatus
{
    Ideia = 0,
    Roteiro = 1,
    Gravado = 2,
    Editado = 3,
    Publicado = 4,
}

/// <summary>Um roteiro de vídeo. O texto é Markdown.</summary>
public class Script
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ChannelId { get; set; }
    public Channel? Channel { get; set; }
    public string Title { get; set; } = "";
    public ScriptStatus Status { get; set; } = ScriptStatus.Ideia;
    /// <summary>Tags separadas por vírgula, já normalizadas (sem espaços extras nem repetidas).</summary>
    public string Tags { get; set; } = "";
    public string Content { get; set; } = "";
    /// <summary>Texto da descrição do vídeo no YouTube.</summary>
    public string? Description { get; set; }
    public string? ThumbnailIdea { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ScriptVersion> Versions { get; set; } = [];

    /// <summary>Contagem de palavras do texto (ignora marcação Markdown simples).</summary>
    public static int CountWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Count(w => w.Any(char.IsLetterOrDigit));

    /// <summary>Tempo de leitura em voz alta, a ~150 palavras por minuto.</summary>
    public static int ReadingSeconds(int words) => (int)Math.Round(words / 150.0 * 60);

    public static string NormalizeTags(IEnumerable<string>? tags) => string.Join(",",
        (tags ?? []).Select(t => t.Trim().TrimStart('#').Trim())
                    .Where(t => t.Length > 0)
                    .Select(t => t.Length > 50 ? t[..50] : t)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .Take(30));

    public static string[] SplitTags(string tags) => tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>Cópia do texto de um roteiro num momento em que ele foi salvo.</summary>
public class ScriptVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScriptId { get; set; }
    public string Content { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
