using System.Net;
using System.Net.Http.Json;
using YTStudio.Core.Models;
using YTStudio.Desktop.Api;

namespace YTStudio.Tests;

// Usa o mesmo servidor de teste dos roteiros (ScriptApiTests sobe a API com um .ytv temporário).
public sealed partial class ScriptApiTests
{
    private static object Pub(string? title, DateTimeOffset at, Guid? scriptId = null, Guid? channelId = null,
        string status = "Planejado", int? reminder = 60) =>
        new { title, channelId, scriptId, scheduledAt = at, status, videoUrl = (string?)null, notes = (string?)null, reminderMinutes = reminder };

    [Fact]
    public async Task Agenda_filtra_por_periodo_e_ordena_por_data()
    {
        var baseDay = new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);
        await Send<Publication>(HttpMethod.Post, "/api/publications", Pub("Depois", baseDay.AddDays(10)));
        await Send<Publication>(HttpMethod.Post, "/api/publications", Pub("Antes", baseDay.AddDays(2)));
        await Send<Publication>(HttpMethod.Post, "/api/publications", Pub("Janeiro", baseDay.AddMonths(1)));

        var dec = await Send<List<Publication>>(HttpMethod.Get,
            $"/api/publications?from={Uri.EscapeDataString(baseDay.ToString("O"))}&to={Uri.EscapeDataString(baseDay.AddMonths(1).AddDays(-1).ToString("O"))}");
        Assert.Equal(["Antes", "Depois"], dec.Select(p => p.Title));
    }

    [Fact]
    public async Task Sem_titulo_usa_o_do_roteiro_e_publicado_atualiza_o_roteiro()
    {
        var canal = await Send<Channel>(HttpMethod.Post, "/api/channels", new { name = "Canal" });
        var s = await Send<ScriptDetail>(HttpMethod.Post, "/api/scripts", Input("Review do Civic", canal.Id));
        var p = await Send<Publication>(HttpMethod.Post, "/api/publications", Pub(null, DateTimeOffset.UtcNow.AddDays(3), s.Id));
        Assert.Equal("Review do Civic", p.Title);
        Assert.Equal(canal.Id, p.ChannelId);

        await Send<Publication>(HttpMethod.Put, $"/api/publications/{p.Id}", Pub(null, p.ScheduledAt, s.Id, status: "Publicado"));
        Assert.Equal(ScriptStatus.Publicado, (await Send<ScriptDetail>(HttpMethod.Get, $"/api/scripts/{s.Id}")).Status);
    }

    [Fact]
    public async Task Lembrete_sai_uma_vez_so_e_volta_se_mudar_o_horario()
    {
        var soon = DateTimeOffset.UtcNow.AddMinutes(30); // lembrete de 60 min → já venceu
        var later = DateTimeOffset.UtcNow.AddDays(2);    // ainda não venceu
        var p = await Send<Publication>(HttpMethod.Post, "/api/publications", Pub("Hoje", soon));
        await Send<Publication>(HttpMethod.Post, "/api/publications", Pub("Depois", later));
        await Send<Publication>(HttpMethod.Post, "/api/publications", Pub("Sem lembrete", soon, reminder: null));
        await Send<Publication>(HttpMethod.Post, "/api/publications", Pub("Já saiu", soon, status: "Publicado"));

        var first = await Send<List<Publication>>(HttpMethod.Post, "/api/publications/reminders/take");
        Assert.Equal(["Hoje"], first.Select(x => x.Title));
        Assert.Empty(await Send<List<Publication>>(HttpMethod.Post, "/api/publications/reminders/take"));

        await Send<Publication>(HttpMethod.Put, $"/api/publications/{p.Id}", Pub("Hoje", soon.AddMinutes(10)));
        Assert.Single(await Send<List<Publication>>(HttpMethod.Post, "/api/publications/reminders/take"));
    }

    [Fact]
    public async Task Agenda_recusa_sem_titulo_nem_roteiro()
    {
        using var res = await _http.PostAsJsonAsync("/api/publications", Pub(" ", DateTimeOffset.UtcNow), Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Excluir_roteiro_mantem_o_item_da_agenda()
    {
        var s = await Send<ScriptDetail>(HttpMethod.Post, "/api/scripts", Input("Roteiro"));
        var p = await Send<Publication>(HttpMethod.Post, "/api/publications", Pub(null, DateTimeOffset.UtcNow.AddDays(1), s.Id));
        (await _http.DeleteAsync($"/api/scripts/{s.Id}")).EnsureSuccessStatusCode();
        var list = await Send<List<Publication>>(HttpMethod.Get, "/api/publications");
        Assert.Null(Assert.Single(list, x => x.Id == p.Id).ScriptId);
    }

    [Theory]
    [InlineData(-5, "horário de publicar já chegou")]
    [InlineData(20, "publica em 20 min")]
    [InlineData(180, "publica em 3 h")]
    [InlineData(60 * 48, "publica em 2 dia(s)")]
    public void Texto_do_lembrete(int minutes, string expected) =>
        Assert.Equal(expected, PublicationEndpoints.Describe(TimeSpan.FromMinutes(minutes)));
}
