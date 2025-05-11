using Newtonsoft.Json;

namespace GrokSdk.Tools;

/// <summary>
///     A pre-built tool that generates images using the Grok API based on a text prompt.
///     Users can specify the number of images (1 to 10) and choose the output format: 'url' for image URLs or 'base64' for
///     base64-encoded image data. Defaults to 'base64'.
/// </summary>
public class GrokToolImageGeneration : IGrokTool
{
    private readonly GrokClient _client;

    /// <summary>
    ///     Initializes a new instance of <see cref="GrokToolImageGeneration" /> with a Grok client.
    /// </summary>
    /// <param name="client">The <see cref="GrokClient" /> instance used to make API calls.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="client" /> is null.</exception>
    public GrokToolImageGeneration(GrokClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    ///     Gets the name of the tool, used by Grok to identify it.
    /// </summary>
    public string Name => "generate_image";

    /// <summary>
    ///     Gets a description of the tool's functionality.
    /// </summary>
    public string Description =>
        "Generates images based on a text prompt. Users can specify the number of images (1 to 10) and choose the output format: 'url' for image URLs or 'base64' for base64-encoded image data. Defaults to 'base64'.";

    public object Parameters => new Dictionary<string, object>
    {
        { "prompt", new Dictionary<string, object> { { "type", "string" } } },
        {
            "n",
            new Dictionary<string, object>
                { { "type", "integer" }, { "minimum", 1 }, { "maximum", 10 }, { "default", 1 } }
        },
        {
            "response_format",
            new Dictionary<string, object>
                { { "type", "string" }, { "enum", new[] { "url", "base64" } }, { "default", "base64" } }
        }
    };

    /// <summary>
    ///     Event raised when an image URL is received.
    /// </summary>
    public event EventHandler<GrokImageUrlEventArgs>? ImageUrlReceived;

    /// <summary>
    ///     Event raised when base64 image data is received.
    /// </summary>
    public event EventHandler<GrokImageBase64EventArgs>? ImageBase64Received;

    /// <summary>
    ///     Generates images based on the provided prompt and returns the results in the specified format.
    /// </summary>
    /// <param name="arguments">A JSON-serialized string containing the 'prompt', 'n', and 'response_format' fields.</param>
    /// <returns>A task resolving to a JSON-serialized string with the image data or an error message.</returns>
    public async Task<string> ExecuteAsync(string arguments)
    {
        try
        {
            var args = JsonConvert.DeserializeObject<GrokToolImageGenerationArgs>(arguments);
            if (string.IsNullOrEmpty(args?.Prompt))
                return JsonConvert.SerializeObject(new { error = "Prompt cannot be empty." });
            if (args.N < 1 || args.N > 10)
                return JsonConvert.SerializeObject(new { error = "n must be between 1 and 10." });
            if (args.ResponseFormat != "url" && args.ResponseFormat != "base64")
                return JsonConvert.SerializeObject(
                    new { error = "Invalid response_format. Must be 'url' or 'base64'." });

            var imageRequest = new GrokImageGenerationRequest
            {
                Prompt = args.Prompt,
                N = args.N,
                Response_format = args.ResponseFormat == "base64"
                    ? GrokImageGenerationRequestResponse_format.B64_json
                    : GrokImageGenerationRequestResponse_format.Url
            };

            var imageResponse = await _client.GenerateImagesAsync(imageRequest);
            if (imageResponse.Data != null && imageResponse.Data.Any())
            {
                foreach (var imageData in imageResponse.Data)
                {
                    switch (args.ResponseFormat)
                    {
                        case "url":
                            ImageUrlReceived?.Invoke(this, new GrokImageUrlEventArgs(imageData.Url, imageData.Revised_prompt));
                            break;
                        case "base64":
                            ImageBase64Received?.Invoke(this, new GrokImageBase64EventArgs(imageData.B64_json, imageData.Revised_prompt));
                            break;
                    }
                }
                var result = new { images = imageResponse.Data };
                return JsonConvert.SerializeObject(result);
            }

            return JsonConvert.SerializeObject(new { error = "No images generated." });
        }
        catch (JsonException ex)
        {
            return JsonConvert.SerializeObject(new { error = $"Invalid arguments: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { error = $"Image generation failed: {ex.Message}" });
        }
    }

    /// <summary>
    ///     Private class to deserialize the tool's arguments.
    /// </summary>
    private class GrokToolImageGenerationArgs
    {
        [JsonProperty("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonProperty("n")]
        public int N { get; set; } = 1;

        [JsonProperty("response_format")]
        public string ResponseFormat { get; set; } = "base64";
    }
}

/// <summary>
///     Event arguments for when an image URL is received.
/// </summary>
public class GrokImageUrlEventArgs(string url, string revisedPrompt) : EventArgs
{
    /// <summary>
    /// URL to the image
    /// </summary>
    public string Url { get; } = url;

    /// <summary>
    /// Revised prompt generated by AI
    /// </summary>
    public string RevisedPrompt { get; } = revisedPrompt;
}

/// <summary>
///     Event arguments for when base64 image data is received.
/// </summary>
public class GrokImageBase64EventArgs(string base64Data, string revisedPrompt) : EventArgs
{
    /// <summary>
    /// Base64 Data for the image
    /// </summary>
    public string Base64Data { get; } = base64Data;

    /// <summary>
    /// Revised prompt generated by AI
    /// </summary>
    public string RevisedPrompt { get; } = revisedPrompt;
}