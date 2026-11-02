namespace YTStudio.Core.Models;

/// <summary>Um canal do YouTube gerenciado no arquivo do estúdio.</summary>
public class Channel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Url { get; set; }
    public string? Niche { get; set; }
    public string? Language { get; set; }
    /// <summary>Cor do canal na interface, em hexadecimal (ex.: #E53935).</summary>
    public string Color { get; set; } = "#E53935";
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
