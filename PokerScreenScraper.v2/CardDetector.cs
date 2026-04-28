using System.Net.Http.Headers;
using System.Text.Json;

namespace PokerScreenScraper;

internal static class CardDetector
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static async Task<CardDetectionResult> DetectarCartasAsync(Bitmap imagen)
    {
        try
        {
            await using var stream = new MemoryStream();
            imagen.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;

            using var contenido = new MultipartFormDataContent();
            using var imagenContenido = new ByteArrayContent(stream.ToArray());
            imagenContenido.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            contenido.Add(imagenContenido, "image", "captura.png");

            using var response = await Http.PostAsync("http://127.0.0.1:5055/detect", contenido);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var api = await JsonSerializer.DeserializeAsync<ApiDetectionResponse>(
                responseStream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (api is null)
            {
                return Error("La API local no devolvio datos.");
            }

            return new CardDetectionResult(
                api.PlayerCards?.ToList() ?? new List<string>(),
                api.BoardCards?.ToList() ?? new List<string>(),
                api.Message ?? string.Empty,
                null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            return Error($"API local no disponible: {ex.Message}");
        }
    }

    private static CardDetectionResult Error(string message)
    {
        return new CardDetectionResult(new List<string>(), new List<string>(), string.Empty, message);
    }

    private sealed record ApiDetectionResponse(
        string[]? PlayerCards,
        string[]? BoardCards,
        double Confidence,
        string? Message,
        string? Mode);
}

internal sealed record CardDetectionResult(
    List<string> PlayerCards,
    List<string> BoardCards,
    string RawText,
    string? Error);
