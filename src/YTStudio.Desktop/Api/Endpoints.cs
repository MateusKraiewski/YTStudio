using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using YTStudio.Core.Models;
using YTStudio.Core.Vault;
using YTStudio.Desktop.Services;

namespace YTStudio.Desktop.Api;

public sealed record VaultRequest(string Path, string Password);
public sealed record ChannelInput(string Name, string? Url, string? Niche, string? Language, string? Color, string? Notes);
public sealed record OpenUrlRequest(string Url);
public sealed record SessionInfo(bool Open, string? Path, IReadOnlyList<string> Recent);

public static partial class Endpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/session", (StudioSession s, RecentFiles recent) =>
            new SessionInfo(s.OpenPath is not null, s.OpenPath, recent.List()));

        api.MapPost("/dialog/open", (IDesktopShell d) => Results.Ok(new { path = d.PickOpen() }));
        api.MapPost("/dialog/save", (IDesktopShell d) => Results.Ok(new { path = d.PickSave() }));

        api.MapGet("/update", (bool? force, UpdateService u, CancellationToken ct) => u.CheckAsync(force ?? false, ct));
        api.MapPost("/update/install", async (UpdateService u, CancellationToken ct) =>
        {
            try
            {
                await u.InstallAsync(ct);
                return Results.Accepted();
            }
            catch (UpdateException ex)
            {
                return Error(ex.Message);
            }
            catch (HttpRequestException)
            {
                return Error("Não foi possível baixar a atualização. Confira a internet e tente de novo.");
            }
        });
        api.MapPost("/open-url", (OpenUrlRequest req, IDesktopShell d) =>
        {
            // Só páginas do próprio projeto no GitHub (notas da versão).
            if (!req.Url.StartsWith($"https://github.com/{UpdateService.Repo}/", StringComparison.OrdinalIgnoreCase)) return Error("Link não permitido.");
            d.OpenUrl(req.Url);
            return Results.NoContent();
        });

        api.MapPost("/vault/create", (VaultRequest req, StudioSession s, RecentFiles recent) =>
            OpenVault(() => VaultFile.Create(req.Path, req.Password), s, recent));

        api.MapPost("/vault/open", (VaultRequest req, StudioSession s, RecentFiles recent) =>
            OpenVault(() => VaultFile.Open(req.Path, req.Password), s, recent));

        api.MapPost("/vault/lock", (StudioSession s) =>
        {
            s.Lock();
            return Results.NoContent();
        });

        api.MapDelete("/recent", (string path, RecentFiles recent) =>
        {
            recent.Remove(path);
            return Results.NoContent();
        });

        ScriptEndpoints.Map(api);
        PublicationEndpoints.Map(api);

        var channels = api.MapGroup("/channels");

        channels.MapGet("/", async (StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return Locked();
            var list = await db.Channels.AsNoTracking().ToListAsync(ct);
            return Results.Ok(list.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase));
        });

        channels.MapPost("/", async (ChannelInput input, StudioSession s, CancellationToken ct) =>
        {
            if (Validate(input) is { } error) return error;
            await using var db = s.TryCreateContext();
            if (db is null) return Locked();
            var channel = new Channel();
            Apply(channel, input);
            db.Channels.Add(channel);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/channels/{channel.Id}", channel);
        });

        channels.MapPut("/{id:guid}", async (Guid id, ChannelInput input, StudioSession s, CancellationToken ct) =>
        {
            if (Validate(input) is { } error) return error;
            await using var db = s.TryCreateContext();
            if (db is null) return Locked();
            var channel = await db.Channels.FindAsync([id], ct);
            if (channel is null) return Results.NotFound();
            Apply(channel, input);
            channel.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(channel);
        });

        channels.MapDelete("/{id:guid}", async (Guid id, StudioSession s, CancellationToken ct) =>
        {
            await using var db = s.TryCreateContext();
            if (db is null) return Locked();
            var removed = await db.Channels.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
            return removed == 0 ? Results.NotFound() : Results.NoContent();
        });
    }

    private static IResult OpenVault(Func<VaultFile> open, StudioSession s, RecentFiles recent)
    {
        try
        {
            var vault = open();
            s.Replace(vault);
            recent.Add(vault.Path);
            return Results.Ok(new SessionInfo(true, vault.Path, recent.List()));
        }
        catch (VaultException ex)
        {
            return Error(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error($"Não foi possível acessar o arquivo: {ex.Message}");
        }
    }

    private static IResult? Validate(ChannelInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return Error("Dê um nome ao canal.");
        if (input.Name.Trim().Length > 200) return Error("O nome do canal pode ter no máximo 200 caracteres.");
        if (!string.IsNullOrWhiteSpace(input.Url) && !Uri.TryCreate(input.Url.Trim(), UriKind.Absolute, out _))
            return Error("O link do canal não parece um endereço válido (ex.: https://www.youtube.com/@canal).");
        if (!string.IsNullOrWhiteSpace(input.Color) && !HexColor().IsMatch(input.Color)) return Error("Cor inválida.");
        return null;
    }

    private static void Apply(Channel c, ChannelInput input)
    {
        c.Name = input.Name.Trim();
        c.Url = Clean(input.Url, 500);
        c.Niche = Clean(input.Niche, 100);
        c.Language = Clean(input.Language, 50);
        c.Color = string.IsNullOrWhiteSpace(input.Color) ? c.Color : input.Color;
        c.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }

    private static IResult Error(string message) => ApiResults.Error(message);

    private static IResult Locked() => ApiResults.Locked();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
