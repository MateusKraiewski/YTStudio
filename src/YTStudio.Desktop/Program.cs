using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Photino.NET;
using YTStudio.Desktop.Api;
using YTStudio.Desktop.Services;

namespace YTStudio.Desktop;

internal static class Program
{
    private const int DevPort = 5199;
    private const string DevUiUrl = "http://localhost:4200";

    [STAThread]
    private static int Main(string[] args)
    {
        // --dev: API na porta 5199 com token "dev" e janela apontando para o "ng serve" (ui/).
        // --no-window: só a API, para testar sem interface gráfica.
        bool dev = args.Contains("--dev");
        bool noWindow = args.Contains("--no-window");

        var token = dev ? "dev" : LocalOnly.NewToken();
        var dialogs = new PhotinoFileDialogs();
        var app = BuildApp(dev ? DevPort : 0, token, dialogs);
        app.StartAsync().GetAwaiter().GetResult();

        var apiUrl = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var uiUrl = $"{(dev ? DevUiUrl : apiUrl)}/?t={token}";

        if (noWindow)
        {
            Console.WriteLine($"YTStudio rodando em {uiUrl} (Ctrl+C para sair)");
            app.WaitForShutdown();
            return 0;
        }

        var window = new PhotinoWindow()
            .SetTitle("YTStudio")
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 820)
            .SetMinSize(900, 600)
            .Center()
            .SetContextMenuEnabled(dev)
            .SetDevToolsEnabled(dev)
            .Load(new Uri(uiUrl));
        dialogs.Window = window;

        window.WaitForClose();

        // Ao fechar a janela: bloqueia o arquivo (apaga a chave da memória) e para a API.
        app.Services.GetRequiredService<StudioSession>().Lock();
        app.StopAsync().GetAwaiter().GetResult();
        return 0;
    }

    internal static WebApplication BuildApp(int port, string token, IFileDialogs dialogs)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        });
        builder.WebHost.UseKestrel(k => k.Listen(IPAddress.Loopback, port));

        builder.Services.AddSingleton<StudioSession>();
        builder.Services.AddSingleton(new RecentFiles());
        builder.Services.AddSingleton(dialogs);

        var app = builder.Build();

        // A porta só é conhecida depois que o servidor sobe, então o filtro é montado no primeiro pedido.
        var guard = new Lazy<LocalOnly>(() =>
        {
            var port = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;
            return new LocalOnly(token, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $"127.0.0.1:{port}", $"localhost:{port}" });
        });
        app.Use((ctx, next) => guard.Value.InvokeAsync(ctx, next));

        app.UseDefaultFiles();
        app.UseStaticFiles();
        Endpoints.Map(app);
        app.MapFallbackToFile("index.html");
        return app;
    }
}
