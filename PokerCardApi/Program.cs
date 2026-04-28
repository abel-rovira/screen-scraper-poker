using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "PokerCardApi",
    status = "ok",
    endpoints = new[] { "POST /detect" }
}));

app.MapPost("/detect", (IFormFile image) =>
{
    if (image.Length == 0)
    {
        return Results.BadRequest(new DetectionResponse(
            Array.Empty<string>(),
            Array.Empty<string>(),
            0,
            "Imagen vacia.",
            "local-offline"));
    }

    var detected = LocalCardDetector.Detect(image.FileName, image.Length);
    return Results.Ok(detected);
})
.DisableAntiforgery();

app.Run("http://127.0.0.1:5055");

internal sealed record DetectionResponse(
    IReadOnlyList<string> PlayerCards,
    IReadOnlyList<string> BoardCards,
    double Confidence,
    string? Message,
    string Mode);

internal static class LocalCardDetector
{
    private static readonly Regex CardToken = new(
        @"(?<![A-Za-z0-9])([2-9TJQKA])\s*([HDSC])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static DetectionResponse Detect(string fileName, long fileSize)
    {
        // This local API is intentionally offline and deterministic.
        // It does not perform live table assistance; it only provides a safe
        // endpoint shape for educational image-analysis experiments.
        var embeddedTextCards = TryReadCardsFromFileName(fileName);
        if (embeddedTextCards.Count > 0)
        {
            return new DetectionResponse(
                embeddedTextCards.Take(2).ToArray(),
                embeddedTextCards.Skip(2).Take(5).ToArray(),
                0.5,
                "Cartas leidas desde metadatos/nombre de prueba.",
                "local-offline-demo");
        }

        return new DetectionResponse(
            Array.Empty<string>(),
            Array.Empty<string>(),
            0,
            $"Imagen recibida ({fileSize} bytes). No detectado. Usa imagenes offline etiquetadas o introduce cartas manualmente.",
            "local-offline");
    }

    private static List<string> TryReadCardsFromFileName(string fileName)
    {
        var cards = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matches = CardToken.Matches(fileName.Replace("10", "T", StringComparison.OrdinalIgnoreCase));

        foreach (Match match in matches)
        {
            var card = $"{match.Groups[1].Value.ToUpperInvariant()}{match.Groups[2].Value.ToLowerInvariant()}";
            if (seen.Add(card))
            {
                cards.Add(card);
            }
        }

        return cards;
    }
}
