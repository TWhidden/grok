using Newtonsoft.Json;

namespace GrokSdk.Tools;

/// <summary>
///     Represents the arguments for the reasoning tool.
/// </summary>
public class GrokToolReasoningArgs
{
    /// <summary>
    ///     The problem to reason about.
    /// </summary>
    [JsonProperty("problem")]
    public string Problem { get; set; } = string.Empty;

    /// <summary>
    ///     The effort level for reasoning, either "low" or "high". Defaults to "low".
    /// </summary>
    [JsonProperty("effort")]
    public string Effort { get; set; } = "low";
}

/// <summary>
///     Represents the response from the reasoning tool.
/// </summary>
public class GrokToolReasoningResponse
{
    /// <summary>
    ///     The reasoning result.
    /// </summary>
    [JsonProperty("reasoning")]
    public string? Reasoning { get; set; }

    /// <summary>
    ///     An error message if the operation failed.
    /// </summary>
    [JsonProperty("error")]
    public string? Error { get; set; }

    /// <summary>
    ///     Deserializes a JSON string into a <see cref="GrokToolReasoningResponse" /> object.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A <see cref="GrokToolReasoningResponse" /> object, or null if deserialization fails.</returns>
    public static GrokToolReasoningResponse? DeserializeResponse(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<GrokToolReasoningResponse>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
///     A pre-built tool that performs complex reasoning on a problem using the Grok API.
///     Users can specify the problem and the level of reasoning effort ("low" or "high").
/// </summary>
public class GrokToolReasoning : IGrokTool
{
    /// <summary>
    ///     Tool Name
    /// </summary>
    public const string ToolName = "grok_tool_reasoning";

    private readonly GrokClient _client;
    private readonly string _grokModel;

    /// <summary>
    ///     Initializes a new instance of <see cref="GrokToolReasoning" /> with a Grok client and an optional model.
    /// </summary>
    /// <param name="client">The <see cref="GrokClient" /> instance used to make API calls.</param>
    /// <param name="grokModel">The Grok model to use for reasoning. Defaults to "grok-3-mini".</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="client" /> is null.</exception>
    public GrokToolReasoning(GrokClient client, string grokModel = "grok-3-mini")
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _grokModel = grokModel;
    }

    /// <summary>
    ///     Gets the name of the tool, used by Grok to identify it.
    /// </summary>
    public string Name => ToolName;

    /// <summary>
    ///     Gets a description of the tool's functionality.
    /// </summary>
    public string Description =>
        "Performs complex reasoning on a given problem with configurable effort levels ('low' or 'high').";

    /// <summary>
    ///     Gets the JSON schema for the tool's parameters, expecting a 'problem' string and an optional 'effort' string.
    /// </summary>
    public object Parameters => new Dictionary<string, object>
    {
        { "problem", new Dictionary<string, object> { { "type", "string" } } },
        {
            "effort",
            new Dictionary<string, object>
                { { "type", "string" }, { "enum", new[] { "low", "high" } }, { "default", "low" } }
        }
    };

    /// <summary>
    ///     Executes the tool with JSON-serialized arguments, as required by IGrokTool.
    /// </summary>
    /// <param name="arguments">A JSON-serialized string containing the 'problem' and optional 'effort' fields.</param>
    /// <returns>A task resolving to a JSON-serialized string with the reasoning result or an error message.</returns>
    public async Task<string> ExecuteAsync(string arguments)
    {
        if (string.IsNullOrEmpty(arguments))
            return JsonConvert.SerializeObject(new GrokToolReasoningResponse
                { Error = "Arguments are null or empty." });

        try
        {
            var args = JsonConvert.DeserializeObject<GrokToolReasoningArgs>(arguments);
            if (args == null)
                return JsonConvert.SerializeObject(new GrokToolReasoningResponse
                    { Error = "Failed to deserialize arguments." });
            var response = await ExecuteTypedAsync(args);
            return JsonConvert.SerializeObject(response);
        }
        catch (JsonException ex)
        {
            return JsonConvert.SerializeObject(new GrokToolReasoningResponse
                { Error = $"Invalid arguments: {ex.Message}" });
        }
    }

    /// <summary>
    ///     Performs reasoning on the provided problem with the specified effort level and returns the result.
    /// </summary>
    /// <param name="args">The arguments for the reasoning operation.</param>
    /// <returns>A task resolving to the reasoning response.</returns>
    public async Task<GrokToolReasoningResponse> ExecuteTypedAsync(GrokToolReasoningArgs args)
    {
        if (string.IsNullOrEmpty(args.Problem))
            return new GrokToolReasoningResponse { Error = "Problem cannot be empty." };

        GrokChatCompletionRequestReasoning_effort effort;
        switch (args.Effort.ToLower())
        {
            case "low":
                effort = GrokChatCompletionRequestReasoning_effort.Low;
                break;
            case "high":
                effort = GrokChatCompletionRequestReasoning_effort.High;
                break;
            default:
                return new GrokToolReasoningResponse { Error = "Invalid effort level. Must be 'low' or 'high'." };
        }

        var request = new GrokChatCompletionRequest
        {
            Messages = new List<GrokMessage>
            {
                new GrokUserMessage
                {
                    Content = new List<GrokContent> { new GrokTextPart { Text = args.Problem } }
                }
            },
            Model = _grokModel,
            Reasoning_effort = effort
        };

        try
        {
            var response = await _client.CreateChatCompletionAsync(request);
            var reasoning = response.Choices.First().Message.Content;
            return new GrokToolReasoningResponse { Reasoning = reasoning };
        }
        catch (Exception ex)
        {
            return new GrokToolReasoningResponse { Error = $"Reasoning failed: {ex.Message}" };
        }
    }
}