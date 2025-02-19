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

}
