using Newtonsoft.Json;

namespace GrokSdk.Tools;

/// <summary>
///     A pre-built tool that performs complex reasoning on a problem using the Grok API.
///     Users can specify the problem and the level of reasoning effort ("low" or "high").
/// </summary>
public class GrokToolReasoning : IGrokTool
{
    private readonly GrokClient _client;
    private readonly string _grokModel;

    /// <summary>
    ///     Initializes a new instance of <see cref="GrokToolReasoning" /> with a Grok client and an optional model.
    /// </summary>
    /// <param name="client">The <see cref="GrokClient" /> instance used to make API calls.</param>
    /// <param name="grokModel">The Grok model to use for reasoning. Defaults to "grok-3-mini-beta".</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="client" /> is null.</exception>
    public GrokToolReasoning(GrokClient client, string grokModel = "grok-3-mini-beta")
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _grokModel = grokModel;
    }

    /// <summary>
    ///     Gets the name of the tool, used by Grok to identify it.
    /// </summary>
    public string Name => "reason";

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
    ///     Performs reasoning on the provided problem with the specified effort level and returns the result.
    /// </summary>
    /// <param name="arguments">A JSON-serialized string containing the 'problem' and optional 'effort' fields.</param>
    /// <returns>A task resolving to a JSON-serialized string with the reasoning result or an error message.</returns>
    public async Task<string> ExecuteAsync(string arguments)
    {
        ReasoningArgs args;
        try
        {
            args = JsonConvert.DeserializeObject<ReasoningArgs>(arguments);
        }
        catch (JsonException ex)
        {
            return JsonConvert.SerializeObject(new { error = $"Invalid arguments: {ex.Message}" });
        }

        if (string.IsNullOrEmpty(args.Problem))
            return JsonConvert.SerializeObject(new { error = "Problem cannot be empty." });

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
                return JsonConvert.SerializeObject(new { error = "Invalid effort level. Must be 'low' or 'high'." });
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
            return JsonConvert.SerializeObject(new { reasoning });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { error = $"Reasoning failed: {ex.Message}" });
        }
    }

    /// <summary>
    ///     Private class to deserialize the tool's arguments.
    /// </summary>
    private class ReasoningArgs
    {
        [JsonProperty("problem")]
        public string Problem { get; set; } = string.Empty;

        [JsonProperty("effort")]
        public string Effort { get; set; } = "low"; // Default to "low"
    }
}