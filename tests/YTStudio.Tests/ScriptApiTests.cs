using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using YTStudio.Desktop;
using YTStudio.Desktop.Api;
using YTStudio.Desktop.Services;

namespace YTStudio.Tests;

/// <summary>Sobe a API de verdade (sem janela) com um arquivo .ytv temporário.</summary>
public sealed class ScriptApiTests : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ytstudio-api-").FullName;
    private WebApplication _app = null!;
    private HttpClient _http = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task InitializeAsync()
    {
        _app = Program.BuildApp(0, "teste", new PhotinoShell(), _dir);
        await _app.StartAsync();
        var url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _http = new HttpClient { BaseAddress = new Uri(url) };
        _http.DefaultRequestHeaders.Add(LocalOnly.Header, "teste");

        var res = await _http.PostAsJsonAsync("/api/vault/create", new { path = Path.Combine(_dir, "t.ytv"), password = "senha-de-teste-123" });
        res.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private async Task<T> Send<T>(HttpMethod method, string url, object? body = null)
    {
        using var req = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body, options: Json) };
        using var res = await _http.SendAsync(req);
        Assert.True(res.IsSuccessStatusCode, $"{method} {url} → {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static object Input(string title, Guid? channelId = null, string content = "", string[]? tags = null) =>
        new { title, channelId, status = "Roteiro", tags = tags ?? [], content, description = (string?)null, thumbnailIdea = (string?)null };

    [Fact]
    public async Task Cria_edita_e_guarda_versoes()
    {
        var s = await Send<ScriptDetail>(HttpMethod.Post, "/api/scripts", Input("Review do Civic", content: "primeira versão", tags: ["#carros", " Carros ", "honda"]));
        Assert.Equal(["carros", "honda"], s.Tags);
        Assert.Equal(2, s.Words);

        await Send<ScriptDetail>(HttpMethod.Put, $"/api/scripts/{s.Id}", Input("Review do Civic", content: "segunda versão do texto"));
        await Send<ScriptDetail>(HttpMethod.Put, $"/api/scripts/{s.Id}", Input("Review do Civic 2024", content: "segunda versão do texto")); // só título: sem versão nova

        var versions = await Send<List<VersionSummary>>(HttpMethod.Get, $"/api/scripts/{s.Id}/versions");
        Assert.Equal(2, versions.Count);
        var oldest = await Send<VersionDetail>(HttpMethod.Get, $"/api/scripts/{s.Id}/versions/{versions[^1].Id}");
        Assert.Equal("primeira versão", oldest.Content);
    }

    [Fact]
    public async Task Busca_ignora_acentos_e_filtra_por_canal_e_status()
    {
        var canal = await Send<Core.Models.Channel>(HttpMethod.Post, "/api/channels", new { name = "Política Hoje" });
        await Send<ScriptDetail>(HttpMethod.Post, "/api/scripts", Input("Eleições 2026", canal.Id, "Análise da política"));
        await Send<ScriptDetail>(HttpMethod.Post, "/api/scripts", Input("Outro assunto"));

        Assert.Single(await Send<List<ScriptSummary>>(HttpMethod.Get, "/api/scripts?q=POLITICA"));
        Assert.Single(await Send<List<ScriptSummary>>(HttpMethod.Get, "/api/scripts?q=eleicoes"));
        Assert.Single(await Send<List<ScriptSummary>>(HttpMethod.Get, $"/api/scripts?channelId={canal.Id}"));
        Assert.Equal(2, (await Send<List<ScriptSummary>>(HttpMethod.Get, "/api/scripts?status=Roteiro")).Count);
        Assert.Empty(await Send<List<ScriptSummary>>(HttpMethod.Get, "/api/scripts?status=Publicado"));
    }

    [Fact]
    public async Task Excluir_canal_mantem_os_roteiros_sem_canal()
    {
        var canal = await Send<Core.Models.Channel>(HttpMethod.Post, "/api/channels", new { name = "Canal" });
        var s = await Send<ScriptDetail>(HttpMethod.Post, "/api/scripts", Input("Roteiro", canal.Id));

        (await _http.DeleteAsync($"/api/channels/{canal.Id}")).EnsureSuccessStatusCode();

        var after = await Send<ScriptDetail>(HttpMethod.Get, $"/api/scripts/{s.Id}");
        Assert.Null(after.ChannelId);
    }

    [Fact]
    public async Task Excluir_roteiro_apaga_as_versoes()
    {
        var s = await Send<ScriptDetail>(HttpMethod.Post, "/api/scripts", Input("Roteiro", content: "texto"));
        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/api/scripts/{s.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/scripts/{s.Id}")).StatusCode);
        Assert.Empty(await Send<List<VersionSummary>>(HttpMethod.Get, $"/api/scripts/{s.Id}/versions"));
    }

    [Fact]
    public async Task Titulo_vazio_e_canal_inexistente_sao_recusados()
    {
        using var r1 = await _http.PostAsJsonAsync("/api/scripts", Input(" "), Json);
        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);
        using var r2 = await _http.PostAsJsonAsync("/api/scripts", Input("Ok", Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
    }

    [Fact]
    public async Task Bloqueado_responde_423()
    {
        (await _http.PostAsync("/api/vault/lock", null)).EnsureSuccessStatusCode();
        Assert.Equal((HttpStatusCode)423, (await _http.GetAsync("/api/scripts")).StatusCode);
    }
}
