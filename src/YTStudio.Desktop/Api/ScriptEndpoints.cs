using Microsoft.EntityFrameworkCore;
using YTStudio.Core.Models;
using YTStudio.Desktop.Services;

namespace YTStudio.Desktop.Api;

public sealed record ScriptInput(
    string Title, Guid? ChannelId, ScriptStatus Status, string[]? Tags,
    string? Content, string? Description, string? ThumbnailIdea);

public sealed record ScriptSummary(
    Guid Id, string Title, Guid? ChannelId, ScriptStatus Status, string[] Tags,
    int Words, int ReadingSeconds, DateTimeOffset UpdatedAt);

public sealed record ScriptDetail(
    Guid Id, string Title, Guid? ChannelId, ScriptStatus Status, string[] Tags,
    string Content, string? Description, string? ThumbnailIdea,
    int Words, int ReadingSeconds, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record VersionSummary(Guid Id, DateTimeOffset SavedAt, int Words);
public sealed record VersionDetail(Guid Id, DateTimeOffset SavedAt, string Content);

public static class ScriptEndpoints
{
    /// <summary>Quantas versões antigas cada roteiro guarda.</summary>
    public const int MaxVersions = 50;

    public static void Map(RouteGroupBuilder api)
    {
        var scripts = api.MapGroup("/scripts");

        // Lista com filtros. A busca ignora maiúsculas e acentos e olha título, tags e texto.
        scripts.MapGet("/", async (Guid? channelId, ScriptStatus? status, string? q, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();

            var query = db.Scripts.AsNoTracking();
            if (channelId is { } cid) query = query.Where(x => x.ChannelId == cid);
            if (status is { } st) query = query.Where(x => x.Status == st);
            var list = await query.ToListAsync(ct);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = Text.Fold(q.Trim());
                list = list.Where(x => Text.Fold($"{x.Title} {x.Tags} {x.Content}").Contains(needle, StringComparison.Ordinal)).ToList();
            }

            return Results.Ok(list.OrderByDescending(x => x.UpdatedAt).Select(Summary));
        });

        scripts.MapGet("/{id:guid}", async (Guid id, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var script = await db.Scripts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return script is null ? Results.NotFound() : Results.Ok(Detail(script));
        });

        scripts.MapPost("/", async (ScriptInput input, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            if (await Validate(input, db, ct) is { } error) return error;

            var script = new Script();
            Apply(script, input);
            db.Scripts.Add(script);
            if (script.Content.Length > 0) db.ScriptVersions.Add(new ScriptVersion { ScriptId = script.Id, Content = script.Content });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/scripts/{script.Id}", Detail(script));
        });

        scripts.MapPut("/{id:guid}", async (Guid id, ScriptInput input, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            if (await Validate(input, db, ct) is { } error) return error;
            var script = await db.Scripts.FindAsync([id], ct);
            if (script is null) return Results.NotFound();

            var oldContent = script.Content;
            Apply(script, input);
            script.UpdatedAt = DateTimeOffset.UtcNow;

            // Cada vez que o texto muda e é salvo, guarda uma versão (no máximo MaxVersions por roteiro).
            if (script.Content != oldContent)
            {
                db.ScriptVersions.Add(new ScriptVersion { ScriptId = script.Id, Content = script.Content });
                var excess = await db.ScriptVersions.Where(v => v.ScriptId == id)
                    .OrderByDescending(v => v.SavedAt).Skip(MaxVersions - 1).ToListAsync(ct);
                db.ScriptVersions.RemoveRange(excess);
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(Detail(script));
        });

        scripts.MapDelete("/{id:guid}", async (Guid id, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var script = await db.Scripts.FindAsync([id], ct);
            if (script is null) return Results.NotFound();
            db.Scripts.Remove(script); // as versões vão junto (cascade)
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        scripts.MapGet("/{id:guid}/versions", async (Guid id, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var versions = await db.ScriptVersions.AsNoTracking().Where(v => v.ScriptId == id).ToListAsync(ct);
            return Results.Ok(versions.OrderByDescending(v => v.SavedAt)
                .Select(v => new VersionSummary(v.Id, v.SavedAt, Script.CountWords(v.Content))));
        });

        scripts.MapGet("/{id:guid}/versions/{versionId:guid}", async (Guid id, Guid versionId, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return ApiResults.Locked();
            var v = await db.ScriptVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && v.ScriptId == id, ct);
            return v is null ? Results.NotFound() : Results.Ok(new VersionDetail(v.Id, v.SavedAt, v.Content));
        });
    }

    private static async Task<IResult?> Validate(ScriptInput input, Core.Data.StudioDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) return ApiResults.Error("Dê um título ao roteiro.");
        if (input.Title.Trim().Length > 300) return ApiResults.Error("O título pode ter no máximo 300 caracteres.");
        if (!Enum.IsDefined(input.Status)) return ApiResults.Error("Status inválido.");
        if (input.ChannelId is { } cid && !await db.Channels.AnyAsync(c => c.Id == cid, ct))
            return ApiResults.Error("Esse canal não existe mais.");
        return null;
    }

    private static void Apply(Script s, ScriptInput input)
    {
        s.Title = input.Title.Trim();
        s.ChannelId = input.ChannelId;
        s.Status = input.Status;
        s.Tags = Script.NormalizeTags(input.Tags);
        s.Content = input.Content ?? "";
        s.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description;
        s.ThumbnailIdea = string.IsNullOrWhiteSpace(input.ThumbnailIdea) ? null : input.ThumbnailIdea.Trim();
    }

    private static ScriptSummary Summary(Script s)
    {
        var words = Script.CountWords(s.Content);
        return new(s.Id, s.Title, s.ChannelId, s.Status, Script.SplitTags(s.Tags), words, Script.ReadingSeconds(words), s.UpdatedAt);
    }

    private static ScriptDetail Detail(Script s)
    {
        var words = Script.CountWords(s.Content);
        return new(s.Id, s.Title, s.ChannelId, s.Status, Script.SplitTags(s.Tags), s.Content, s.Description, s.ThumbnailIdea,
            words, Script.ReadingSeconds(words), s.CreatedAt, s.UpdatedAt);
    }
}
