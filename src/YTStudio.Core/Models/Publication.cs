namespace YTStudio.Core.Models;

public enum PublicationStatus
{
    Planejado = 0,
    Pronto = 1,
    Publicado = 2,
    Cancelado = 3,
}

/// <summary>Um vídeo na agenda: quando sai, em qual canal e de qual roteiro.</summary>
public class Publication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ChannelId { get; set; }
    public Guid? ScriptId { get; set; }
    public string Title { get; set; } = "";
    /// <summary>Data e hora da publicação (guardada em UTC).</summary>
    public DateTimeOffset ScheduledAt { get; set; }
    public PublicationStatus Status { get; set; } = PublicationStatus.Planejado;
    public string? VideoUrl { get; set; }
    public string? Notes { get; set; }
    /// <summary>Minutos antes da publicação para o lembrete; <c>null</c> = sem lembrete.</summary>
    public int? ReminderMinutes { get; set; } = 60;
    /// <summary>Quando o lembrete foi mostrado (para não repetir).</summary>
    public DateTimeOffset? RemindedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReminderAt => ReminderMinutes is { } m ? ScheduledAt.AddMinutes(-m) : null;
}
