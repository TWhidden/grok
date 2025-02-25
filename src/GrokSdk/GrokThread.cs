using System.Threading.Channels;

namespace GrokSdk;

/// <summary>
/// Base message type for different kinds of responses, nullable enabled
/// </summary>
public abstract record GrokMessageBase
{
}

/// <summary>
/// Text message type inheriting from GrokMessage
/// </summary>
/// <param name="Message"></param>
public record GrokTextMessage(string Message) : GrokMessageBase
{
}

/// <summary>
/// Service based messages from Grok
/// </summary>
/// <param name="Message"></param>
public record GrokServiceMessage(string Message) : GrokMessageBase
{
}

/// <summary>
/// Exception handle indicating a failure occured
/// </summary>
/// <param name="Exception"></param>
public record GrokError(Exception Exception) : GrokMessageBase
{

}

/// <summary>
/// The State of the stream
/// </summary>
/// <param name="StreamState"></param>
public record GrokStreamState(StreamState StreamState) : GrokMessageBase
{

}

/// <summary>
/// Manages the conversation thread with Grok, handling messages and tool calls.
/// </summary>
public class GrokThread(GrokClient client)
{
    private readonly GrokClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly List<GrokMessage> _history = new();
    private readonly Dictionary<string, GrokToolDefinition> _tools = new();

    /// <summary>
    /// Provide instruction to the system on how it should respond to the user.
    /// </summary>
    /// <param name="message">The instruction message to add.</param>
    public void AddSystemInstruction(string message)
    {
        _history.Add(new GrokSystemMessage { Content = message });
    }

    /// <summary>
    /// Registers a tool with the thread, making it available for Grok to use.
    /// </summary>
    /// <param name="tool">The tool definition to register.</param>
    public void RegisterTool(GrokToolDefinition tool)
    {
        if (tool == null)
            throw new ArgumentNullException(nameof(tool));


#if NETSTANDARD2_0
        if (_tools.ContainsKey(tool.Name))
        {
            throw new ArgumentException($"A tool with name '{tool.Name}' already exists.");
        }
        _tools.Add(tool.Name, tool);
#else
        if (!_tools.TryAdd(tool.Name, tool))
            throw new ArgumentException($"A tool with name '{tool.Name}' already exists.");
#endif

    }

    /// <summary>
    /// Asks a question and processes the response, providing status updates and results via an IAsyncEnumerable.
    /// </summary>
    /// <param name="question">The question to ask.</param>
    /// <param name="files">Files to do an analysis on (optional).</param>
    /// <param name="model">The model to use (default: "grok-2-latest").</param>
    /// <param name="temperature">The temperature for response generation (default: 0).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An IAsyncEnumerable of GrokMessageBase containing status updates and responses.</returns>
    /// <exception cref="ArgumentException">Thrown if the question is null or empty.</exception>
    public IAsyncEnumerable<GrokMessageBase> AskQuestion(
        string? question,
        List<byte[]>? files = null,
        string model = "grok-2-latest",
        float temperature = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(question))
            throw new ArgumentException("Question cannot be null or empty.", nameof(question));

        _history.Add(new GrokUserMessage { Content = [new GrokTextPart(){Text = question }]});

        var channel = Channel.CreateUnbounded<GrokMessageBase>();

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessConversationAsync(model, temperature, channel, cancellationToken);
            }
            catch (Exception ex)
            {
                channel.Writer.TryWrite(new GrokError(ex));
                channel.Writer.TryWrite(new GrokStreamState(StreamState.Error));
                channel.Writer.TryComplete(ex);
            }
        }, cancellationToken);

        return channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Processes the conversation by handling tool calls and sending the final response.
    /// </summary>
    /// <param name="model">The model to use.</param>
    /// <param name="temperature">The temperature for response generation.</param>
    /// <param name="channel">The channel to write messages to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task ProcessConversationAsync(
        string model,
        float temperature,
        Channel<GrokMessageBase> channel,
        CancellationToken cancellationToken)
    {
        bool toolCallsPending = true;

        while (toolCallsPending)
        {
            // Send "Thinking" status before making the API call
            channel.Writer.TryWrite(new GrokStreamState(StreamState.Thinking));

            var request = new GrokChatCompletionRequest
            {
                Messages = _history,
                Model = model,
                Temperature = temperature,
                Stream = false, // Always non-streaming
                Tools = _tools.Any() ? _tools.Values.Select(t => new GrokTool
                {
                    Type = GrokToolType.Function,
                    Function = new GrokFunctionDefinition
                    {
                        Name = t.Name,
                        Description = t.Description,
                        Parameters = t.Parameters
                    }
                }).ToList() : null,
                Tool_choice = _tools.Any() ? Tool_choice.Auto : null
            };

            channel.Writer.TryWrite(new GrokStreamState(StreamState.Streaming));
            var response = await _client.CreateChatCompletionAsync(request, cancellationToken);
            var choice = response.Choices.First();

            if (choice.Message.Tool_calls?.Count > 0)
            {
                foreach (var toolCall in choice.Message.Tool_calls)
                {
                    if (_tools.TryGetValue(toolCall.Function.Name, out var tool))
                    {
                        channel.Writer.TryWrite(new GrokStreamState(StreamState.Streaming));
                        string result = await tool.Execute(toolCall.Function.Arguments);
                        
                        _history.Add(new GrokToolMessage { Content = result, Tool_call_id = toolCall.Id });
                        
                    }
                    else
                    {
                        throw new InvalidOperationException($"Tool '{toolCall.Function.Name}' not found.");
                    }
                }
            }
            else
            {
                toolCallsPending = false;
                _history.Add(choice.Message);
                
                // Send the final response to the channel
                channel.Writer.TryWrite(new GrokTextMessage(choice.Message.Content));
                channel.Writer.TryWrite(new GrokStreamState(StreamState.Done));
                channel.Writer.Complete();
            }
        }
    }
}