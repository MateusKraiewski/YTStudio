using System.Security.Cryptography;
using System.Text;

namespace YTStudio.Desktop.Api;

/// <summary>
/// A API escuta só em 127.0.0.1, mas outros programas e sites abertos no navegador também enxergam o localhost.
/// Por isso toda chamada a /api precisa do token aleatório que só a janela do app recebe,
/// e o cabeçalho Host precisa ser o nosso (bloqueia DNS rebinding).
/// </summary>
public sealed class LocalOnly(string token, IReadOnlySet<string> allowedHosts)
{
    public const string Header = "X-YTStudio-Token";
    private readonly byte[] _token = Encoding.UTF8.GetBytes(token);

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        if (!allowedHosts.Contains(ctx.Request.Host.Value ?? ""))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            var sent = Encoding.UTF8.GetBytes(ctx.Request.Headers[Header].ToString());
            if (!CryptographicOperations.FixedTimeEquals(sent, _token))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        ctx.Response.Headers["Cache-Control"] = "no-store";
        await next(ctx);
    }
}
