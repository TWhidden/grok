using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

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
    /// Returns a Responses API streaming client configured with the existing HttpClient and API token.
    /// Use this for streaming responses from the /v1/responses endpoint.
    /// </summary>
    /// <returns>A new instance of GrokResponsesStreamingClient.</returns>
    public GrokResponsesStreamingClient GetResponsesStreamingClient()
    {
        return new GrokResponsesStreamingClient(_httpClient, JsonSerializerSettings);
    }

    /// <summary>
    /// Get a Grok Thread Helper - which will allow you to stream back the response as its being received
    /// </summary>
    /// <returns></returns>
    public GrokThread GetGrokThread()
    {
        return new GrokThread(this);
    }

    /// <summary>
    /// Submits a chat completion request for deferred processing. The request is sent to the
    /// regular chat/completions endpoint with deferred=true. Returns a request_id that can be
    /// used to poll for the result via <see cref="GetDeferredChatCompletionAsync"/>.
    /// </summary>
    /// <param name="request">The chat completion request. The Deferred property will be set to true automatically.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The request_id for polling the deferred result.</returns>
    /// <exception cref="GrokSdkException">Thrown if the API returns an error.</exception>
    public async Task<string> CreateDeferredChatCompletionAsync(
        GrokChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Deferred = true;
        request.Stream = false;

        var content_ = new StringContent(
            Newtonsoft.Json.JsonConvert.SerializeObject(request, JsonSerializerSettings));
        content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");

        var response_ = await _httpClient.PostAsync(
            _baseUrl + "chat/completions", content_, cancellationToken).ConfigureAwait(false);

        try
        {
            var status_ = (int)response_.StatusCode;
            if (status_ == 200)
            {
                var responseText = await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(responseText);
                var requestId = json["request_id"]?.ToString();

                if (string.IsNullOrEmpty(requestId))
                    throw new GrokSdkException(
                        "Deferred response did not contain a request_id.",
                        status_, responseText, null, null);

                return requestId!;
            }
            else
            {
                var responseText = await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new GrokSdkException(
                    $"Deferred chat completion failed with status {status_}: {responseText}",
                    status_, responseText, null, null);
            }
        }
        finally
        {
            response_.Dispose();
            content_.Dispose();
        }
    }
}