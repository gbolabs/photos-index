using System.Net.Http.Json;
using System.Text.Json;

namespace Integration.Tests.Helpers;

/// <summary>
/// Extension methods for HttpClient to simplify integration testing.
/// </summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Deserializes JSON response with custom options.
    /// </summary>
    public static async Task<T?> ReadAsJsonAsync<T>(this HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    /// <summary>
    /// Sends a PATCH request with JSON content.
    /// </summary>
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(
        this HttpClient client,
        string requestUri,
        T value)
    {
        var content = JsonContent.Create(value, options: JsonOptions);
        return client.PatchAsync(requestUri, content);
    }

    /// <summary>
    /// Gets the trace ID from response headers.
    /// </summary>
    public static string? GetTraceId(this HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-Trace-Id", out var values))
        {
            return values.FirstOrDefault();
        }
        return null;
    }

    /// <summary>
    /// Asserts that response is successful and returns deserialized content.
    /// </summary>
    public static async Task<T> GetSuccessResponseAsync<T>(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>();
        if (result == null)
        {
            throw new InvalidOperationException("Response content was null");
        }
        return result;
    }
}
