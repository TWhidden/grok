using GrokSdk.Tools;
using System.Threading.Channels;

namespace GrokSdk;

/// <summary>
///     Base message type for different kinds of responses, nullable enabled
/// </summary>
public abstract record GrokMessageBase
{
}

/// <summary>
///     Text message type inheriting from GrokMessage
/// </summary>
/// <param name="Message"></param>
public record GrokTextMessage(string Message) : GrokMessageBase
{
}

/// <summary>
///     Service based messages from Grok
/// </summary>
/// <param name="Message"></param>
public record GrokServiceMessage(string Message) : GrokMessageBase
{
}

/// <summary>
///     Exception handle indicating a failure occured
/// </summary>
/// <param name="Exception"></param>
public record GrokError(Exception Exception) : GrokMessageBase
{
}

/// <summary>
///     The State of the stream
/// </summary>
/// <param name="StreamState"></param>
public record GrokStreamState(StreamState StreamState) : GrokMessageBase
{
}

/// <summary>
///     When a tool executes, the response is passed back to Grok, but this will allow additional usage.
/// </summary>
/// <param name="ToolName">Name of the tool that returned</param>
/// <param name="ToolResponse">The raw response from the tool being passed back to Grok</param>
public record GrokToolResponse(string ToolName, string ToolResponse) : GrokMessageBase
{
}

/// <summary>
///     Manages the conversation thread with Grok, handling messages and tool calls.
/// </summary>
public class GrokThread(GrokClient client)
{
    private readonly GrokClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly List<GrokMessage> _history = new();
    private readonly Dictionary<string, IGrokTool> _tools = new();
    private string? _lastSystemInstruction;

    /// <summary>
    /// View into the History data backing the chat.
    /// </summary>
    public IReadOnlyCollection<GrokMessage> History => _history;

    /// <summary>
    ///     Provide instruction to the system on how it should respond to the user.
    /// </summary>
    /// <param name="systemInstruction">The instruction systemInstruction to add.</param>
    public void AddSystemInstruction(string? systemInstruction)
    {
        _lastSystemInstruction = systemInstruction;
        if (systemInstruction != null) _history.Add(new GrokSystemMessage { Content = systemInstruction });
    }

    /// <summary>
    ///     Add to the History without having an API call. Next API hit will include this in the context
    /// </summary>
    /// <param name="message">User message to include on the next call</param>
    public void AddUserMessage(string message)
    {
        _history.Add(new GrokUserMessage
        {
            Content =
            [
                new GrokTextPart
                {
                    Text = message
                }
            ]
        });
    }

    /// <summary>
    ///     Takes the all the History data and compresses to reduce token consumption but still keeps context.
    /// </summary>
    /// <param name="compressPrompt">Alternate Prompt that may be better at compressing data efficiently</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CompressHistoryAsync(
        string compressPrompt =
            "Summarize this chat history into a single item that captures the main context. Return to me that compressed chat.",
        CancellationToken cancellationToken = default)
    {
        await foreach (var messageResponse in AskQuestion(compressPrompt, cancellationToken: cancellationToken))
        {
            if (messageResponse is not GrokTextMessage message) continue;
            var compressedSummary = message.Message;
            _history.Clear();
            AddSystemInstruction(_lastSystemInstruction);
            AddUserMessage(compressedSummary);
        }
    }

    /// <summary>
    /// Registers a tool with the thread, making it available for Grok to call during conversations.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tool"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if a tool with the same name is already registered.</exception>
    public void RegisterTool(IGrokTool tool)
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
    ///     Asks a question and processes the response, providing status updates and results via an IAsyncEnumerable.
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

        _history.Add(new GrokUserMessage { Content = [new GrokTextPart { Text = question }] });

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
    ///     Processes the conversation by handling tool calls and sending the final response.
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
        var toolCallsPending = true;

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
                Tools = _tools.Any()
                    ? _tools.Values.Select(t => new GrokTool
                    {
                        Type = GrokToolType.Function,
                        Function = new GrokFunctionDefinition
                        {
                            Name = t.Name,
                            Description = t.Description,
                            Parameters = t.Parameters
                        }
                    }).ToList()
                    : null,
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

                        channel.Writer.TryWrite(new GrokStreamState(StreamState.CallingTool));
                        // Execute the tool
                        var result = await tool.ExecuteAsync(toolCall.Function.Arguments);

                        // the channel reader may care about this raw data -
                        // it could be used in an outside resource like an image URL
                        channel.Writer.TryWrite(new GrokToolResponse(toolCall.Function.Name, result));

                        // Respond to Grok accordingly
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