using Newtonsoft.Json;

namespace GrokSdk.Tools;

/// <summary>
///     Represents the arguments for the image generation tool.
/// </summary>
public class GrokToolImageGenerationArgs
{
    /// <summary>
    ///     The text prompt for generating the image.
    /// </summary>
    [JsonProperty("prompt")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    ///     The number of images to generate (1 to 10). Defaults to 1.
    /// </summary>
    [JsonProperty("n")]
    public int N { get; set; } = 1;

    /// <summary>
    ///     The desired response format: 'url' or 'base64'. Defaults to 'base64'.
    /// </summary>
    [JsonProperty("response_format")]
    public string ResponseFormat { get; set; } = "base64";
}

/// <summary>
///     Represents a single generated image.
/// </summary>
public class GrokToolImageData
{
    /// <summary>
    ///     The URL of the generated image, if requested.
    /// </summary>
    [JsonProperty("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     The base64-encoded image data, if requested.
    /// </summary>
    [JsonProperty("b64_json")]
    public string? B64Json { get; set; }

    /// <summary>
    ///     The revised prompt used for image generation.
    /// </summary>
    [JsonProperty("revised_prompt")]
    public string RevisedPrompt { get; set; } = string.Empty;
}

/// <summary>
///     Represents the response from the image generation tool.
/// </summary>
public class GrokToolImageGenerationResponse
{
    /// <summary>
    ///     The list of generated images.
    /// </summary>
    [JsonProperty("images")]
    public List<GrokToolImageData> Images { get; set; } = [];

    /// <summary>
    ///     An error message if the operation failed.
    /// </summary>
    [JsonProperty("error")]
    public string? Error { get; set; }

    /// <summary>
    ///     Deserializes a JSON string into a <see cref="GrokToolImageGenerationResponse" /> object.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A <see cref="GrokToolImageGenerationResponse" /> object, or null if deserialization fails.</returns>
    public static GrokToolImageGenerationResponse? DeserializeResponse(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<GrokToolImageGenerationResponse>(json);
        }
        catch (JsonException)
        {
            return null; // Returns null if JSON is invalid; adjust error handling as needed
        }
    }
}

/// <summary>
///     A pre-built tool that generates images using the Grok API based on a text prompt.
///     Users can specify the number of images (1 to 10) and choose the output format: 'url' for image URLs or 'base64' for
///     base64-encoded image data. Defaults to 'base64'.
/// </summary>
public class GrokToolImageGeneration : IGrokTool
{
    /// <summary>
    ///     Tool Name
    /// </summary>
    public const string ToolName = "grok_tool_generate_images";

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
    public string Name => ToolName;

    /// <summary>
    ///     Gets a description of the tool's functionality.
    /// </summary>
    public string Description =>
        "Generates images based on a text prompt. Users can specify the number of images (1 to 10) and choose the output format: 'url' for image URLs or 'base64' for base64-encoded image data.";

    /// <summary>
    ///     Gets the JSON schema for the tool's parameters.
    /// </summary>
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
                { { "type", "string" }, { "enum", new[] { "url", "base64" } }, { "default", "url" } }
        }
    };

    /// <summary>
    ///     Executes the tool with JSON-serialized arguments, as required by IGrokTool.
    /// </summary>
    /// <param name="arguments">A JSON-serialized string containing the 'prompt', 'n', and 'response_format' fields.</param>
    /// <returns>A task resolving to a JSON-serialized string with the image data or an error message.</returns>
    public async Task<string> ExecuteAsync(string arguments)
    {
        try
        {
            var args = JsonConvert.DeserializeObject<GrokToolImageGenerationArgs>(arguments);
            if (args == null)
                return JsonConvert.SerializeObject(
                    new GrokToolImageGenerationResponse { Error = "Arguments are null." });

            var response = await ExecuteTypedAsync(args);
            return JsonConvert.SerializeObject(response);
        }
        catch (JsonException ex)
        {
            return JsonConvert.SerializeObject(new GrokToolImageGenerationResponse
                { Error = $"Invalid arguments: {ex.Message}" });
        }
    }

    /// <summary>
    ///     Event raised when an image URL is received.
    /// </summary>
    public event EventHandler<GrokToolImageUrlEventArgs>? ImageUrlReceived;

    /// <summary>
    ///     Event raised when base64 image data is received.
    /// </summary>
    public event EventHandler<GrokToolImageBase64EventArgs>? ImageBase64Received;

    /// <summary>
    ///     Generates images based on the provided arguments and returns the results.
    /// </summary>
    /// <param name="args">The arguments for image generation.</param>
    /// <returns>A task resolving to the image generation response.</returns>
    public async Task<GrokToolImageGenerationResponse> ExecuteTypedAsync(GrokToolImageGenerationArgs args)
    {
        if (string.IsNullOrEmpty(args.Prompt))
            return new GrokToolImageGenerationResponse { Error = "Prompt cannot be empty." };
        if (args.N < 1 || args.N > 10)
            return new GrokToolImageGenerationResponse { Error = "n must be between 1 and 10." };
        if (args.ResponseFormat != "url" && args.ResponseFormat != "base64")
            return new GrokToolImageGenerationResponse
                { Error = "Invalid response_format. Must be 'url' or 'base64'." };

        var imageRequest = new GrokImageGenerationRequest
        {
            Prompt = args.Prompt,
            N = args.N,
            Response_format = args.ResponseFormat == "base64"
                ? GrokImageGenerationRequestResponse_format.B64_json
                : GrokImageGenerationRequestResponse_format.Url
        };

        try
        {
            var imageResponse = await _client.GenerateImagesAsync(imageRequest);
            if (imageResponse.Data != null && imageResponse.Data.Any())
            {
                var images = imageResponse.Data.Select(d => new GrokToolImageData
                {
                    Url = d.Url,
                    B64Json = d.B64_json,
                    RevisedPrompt = d.Revised_prompt
                }).ToList();

                // Raise events for each image
                foreach (var image in images)
                    if (args.ResponseFormat == "url" && image.Url != null)
                        ImageUrlReceived?.Invoke(this, new GrokToolImageUrlEventArgs(image.Url, image.RevisedPrompt));
                    else if (args.ResponseFormat == "base64" && image.B64Json != null)
                        ImageBase64Received?.Invoke(this,
                            new GrokToolImageBase64EventArgs(image.B64Json, image.RevisedPrompt));

                return new GrokToolImageGenerationResponse { Images = images };
            }

            return new GrokToolImageGenerationResponse { Error = "No images generated." };
        }
        catch (Exception ex)
        {
            return new GrokToolImageGenerationResponse { Error = $"Image generation failed: {ex.Message}" };
        }
    }
}

/// <summary>
///     Event arguments for when an image URL is received.
/// </summary>
public class GrokToolImageUrlEventArgs(string url, string revisedPrompt) : EventArgs
{
    /// <summary>
    ///     URL to the image
    /// </summary>
    public string Url { get; } = url;

    /// <summary>
    ///     Revised prompt generated by AI
    /// </summary>
    public string RevisedPrompt { get; } = revisedPrompt;
}

/// <summary>
///     Event arguments for when base64 image data is received.
/// </summary>
public class GrokToolImageBase64EventArgs(string base64Data, string revisedPrompt) : EventArgs
{
    /// <summary>
    ///     Base64 Data for the image
    /// </summary>
    public string Base64Data { get; } = base64Data;

    /// <summary>
    ///     Revised prompt generated by AI
    /// </summary>
    public string RevisedPrompt { get; } = revisedPrompt;
}