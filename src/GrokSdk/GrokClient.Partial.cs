using System.Net.Http.Headers;

namespace GrokSdk;

public partial class GrokClient
{
    public GrokClient(HttpClient httpClient, string apiToken) : this(httpClient)
    {
        if (string.IsNullOrWhiteSpace(apiToken))
            throw new ArgumentNullException(nameof(apiToken), "API token cannot be null or empty.");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        _httpClient.BaseAddress = new Uri("https://api.x.ai/v1");
    }

    /// <summary>
    ///     Returns a streaming client configured with the existing HttpClient and API token.
    /// </summary>
    /// <returns>A new instance of GrokStreamingClient.</returns>
    public GrokStreamingClient GetStreamingClient()
    {
        return new GrokStreamingClient(_httpClient, JsonSerializerSettings);
    }

    /// <summary>
    /// Get a Grok Thread Helper - which will allow you to stream back the response as its being received
    /// </summary>
    /// <returns></returns>
    public GrokThread GetGrokThread()
    {
        return new GrokThread(this);
    }
}