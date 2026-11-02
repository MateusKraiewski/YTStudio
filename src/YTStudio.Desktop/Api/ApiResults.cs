using System.Globalization;
using System.Text;

namespace YTStudio.Desktop.Api;

internal static class ApiResults
{
    /// <summary>Erro com mensagem para mostrar ao usuário: <c>{ "error": "..." }</c>.</summary>
    public static IResult Error(string message) => Results.BadRequest(new { error = message });

    public static IResult Locked() =>
        Results.Json(new { error = "O arquivo está bloqueado." }, statusCode: StatusCodes.Status423Locked);
}

internal static class Text
{
    /// <summary>Minúsculas e sem acentos, para buscas ("Política" acha "politica").</summary>
    public static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }
}
