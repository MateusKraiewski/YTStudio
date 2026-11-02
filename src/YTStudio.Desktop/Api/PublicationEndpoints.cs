using Microsoft.EntityFrameworkCore;
using YTStudio.Core.Models;
using YTStudio.Desktop.Services;

namespace YTStudio.Desktop.Api;

public sealed record PublicationInput(
    string? Title, Guid? ChannelId, Guid? ScriptId, DateTimeOffset ScheduledAt,
    PublicationStatus Status, string? VideoUrl, string? Notes, int? ReminderMinutes);

public static class PublicationEndpoints
{
    private static readonly PublicationStatus[] Pending = [PublicationStatus.Planejado, PublicationStatus.Pronto];

    public static void Map(RouteGroupBuilder api)
    {
        var pubs = api.MapGroup("/publications");

        pubs.MapGet("/", async (DateTimeOffset? from, DateTimeOffset? to, Guid? channelId, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var q = db.Publications.AsNoTracking();
            if (from is { } f) q = q.Where(p => p.ScheduledAt >= f);
            if (to is { } t) q = q.Where(p => p.ScheduledAt < t);
            if (channelId is { } cid) q = q.Where(p => p.ChannelId == cid);
            return Results.Ok(await q.OrderBy(p => p.ScheduledAt).ToListAsync(ct));
        });

        pubs.MapPost("/", async (PublicationInput input, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var p = new Publication();
            if (await Apply(p, input, db, ct) is { } error) return error;
            db.Publications.Add(p);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/publications/{p.Id}", p);
        });

        pubs.MapPut("/{id:guid}", async (Guid id, PublicationInput input, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var p = await db.Publications.FindAsync([id], ct);
            if (p is null) return Results.NotFound();
            if (await Apply(p, input, db, ct) is { } error) return error;
            p.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(p);
        });

        pubs.MapDelete("/{id:guid}", async (Guid id, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var n = await db.Publications.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
            return n == 0 ? Results.NotFound() : Results.NoContent();
        });

        // A interface chama a cada 30 s: devolve os lembretes que venceram (uma vez só) e mostra a notificação do sistema.
        pubs.MapPost("/reminders/take", async (StudioSession s, IDesktopShell shell, ILoggerFactory logs, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var now = DateTimeOffset.UtcNow;
            var candidates = await db.Publications
                .Where(p => p.ReminderMinutes != null && p.RemindedAt == null && Pending.Contains(p.Status) && p.ScheduledAt > now.AddHours(-12))
                .ToListAsync(ct);
            var due = candidates.Where(p => p.ReminderAt <= now).OrderBy(p => p.ScheduledAt).ToList();
            foreach (var p in due) p.RemindedAt = now;
            await db.SaveChangesAsync(ct);

            foreach (var p in due)
            {
                try
                {
                    shell.Notify("YTStudio: vídeo agendado", $"{p.Title} — {Describe(p.ScheduledAt - now)}");
                }
                catch (Exception ex)
                {
                    // Notificação do sistema é um extra; a faixa dentro do app já avisa.
                    logs.CreateLogger("Reminders").LogInformation(ex, "Notificação do sistema indisponível");
                }
            }
            return Results.Ok(due);
        });
    }

    internal static string Describe(TimeSpan until)
    {
        if (until <= TimeSpan.Zero) return "horário de publicar já chegou";
        if (until < TimeSpan.FromHours(1)) return $"publica em {Math.Max(1, (int)Math.Round(until.TotalMinutes))} min";
        if (until < TimeSpan.FromDays(1)) return $"publica em {(int)Math.Round(until.TotalHours)} h";
        return $"publica em {(int)Math.Round(until.TotalDays)} dia(s)";
    }

    private static async Task<IResult?> Apply(Publication p, PublicationInput input, Core.Data.StudioDbContext db, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.Status)) return ApiResults.Error("Status inválido.");
        if (input.ScheduledAt == default) return ApiResults.Error("Escolha a data e a hora.");
        if (input.ReminderMinutes is < 0 or > 60 * 24 * 7) return ApiResults.Error("O lembrete pode ser de até 7 dias antes.");
        if (!string.IsNullOrWhiteSpace(input.VideoUrl) && !Uri.TryCreate(input.VideoUrl.Trim(), UriKind.Absolute, out _))
            return ApiResults.Error("O link do vídeo não parece um endereço válido.");
        if (input.ChannelId is { } cid && !await db.Channels.AnyAsync(c => c.Id == cid, ct))
            return ApiResults.Error("Esse canal não existe mais.");

        Script? script = null;
        if (input.ScriptId is { } sid && (script = await db.Scripts.FindAsync([sid], ct)) is null)
            return ApiResults.Error("Esse roteiro não existe mais.");

        // Sem título, usa o do roteiro.
        var title = string.IsNullOrWhiteSpace(input.Title) ? script?.Title : input.Title.Trim();
        if (string.IsNullOrWhiteSpace(title)) return ApiResults.Error("Dê um título ou escolha um roteiro.");
        if (title.Length > 300) return ApiResults.Error("O título pode ter no máximo 300 caracteres.");

        var scheduled = input.ScheduledAt.ToUniversalTime();
        if (scheduled != p.ScheduledAt || input.ReminderMinutes != p.ReminderMinutes) p.RemindedAt = null; // novo horário, novo lembrete

        p.Title = title;
        p.ChannelId = input.ChannelId ?? script?.ChannelId;
        p.ScriptId = input.ScriptId;
        p.ScheduledAt = scheduled;
        p.Status = input.Status;
        p.VideoUrl = string.IsNullOrWhiteSpace(input.VideoUrl) ? null : input.VideoUrl.Trim();
        p.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes;
        p.ReminderMinutes = input.ReminderMinutes;

        // Marcou como publicado: o roteiro acompanha.
        if (p.Status == PublicationStatus.Publicado && script is { Status: not ScriptStatus.Publicado })
        {
            script.Status = ScriptStatus.Publicado;
            script.UpdatedAt = DateTimeOffset.UtcNow;
        }
        return null;
    }
}
