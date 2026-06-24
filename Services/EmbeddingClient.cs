using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MultimodalEmbeddingLibrary.Services;

internal static class EmbeddingClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<float[]> EmbedImageAsync(string imageDataUri, string apiEndpoint, string apiKey)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = "embed-v4.0",
            input_type = "image",
            embedding_types = new[] { "float" },
            images = new[] { imageDataUri }
        });

        return await PostAndParseAsync(body, apiEndpoint, apiKey);
    }

    public static async Task<float[]> EmbedTextAsync(string text, string apiEndpoint, string apiKey)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = "embed-v4.0",
            input_type = "search_query",
            embedding_types = new[] { "float" },
            texts = new[] { text }
        });

        return await PostAndParseAsync(body, apiEndpoint, apiKey);
    }

    private static async Task<float[]> PostAndParseAsync(string body, string apiEndpoint, string apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, apiEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Cohere API returned {(int)response.StatusCode} {response.ReasonPhrase}. Body: {error}");
        }

        string json = await response.Content.ReadAsStringAsync();
        return ParseEmbeddings(json);
    }

    private static float[] ParseEmbeddings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("embeddings")
            .GetProperty("float")[0]
            .EnumerateArray()
            .Select(e => e.GetSingle())
            .ToArray();
    }
}
