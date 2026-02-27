using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
///     Options for configuring GrokThread behavior.
/// </summary>
public record GrokThreadOptions
{
    /// <summary>
    /// Percentage of max tokens at which to trigger compression (default: 80).
    /// </summary>
    public int CompressionThresholdPercent { get; init; } = 80;

    /// <summary>
    /// Percentage of max tokens at which to clear history (default: 100).
    /// </summary>
    public int ClearThresholdPercent { get; init; } = 100;

    /// <summary>
    /// Whether to enable automatic compression (default: true).
    /// </summary>
    public bool EnableCompression { get; init; } = true;

    /// <summary>
    /// Maximum number of messages to keep in history (FIFO, preserving system instructions). Null for unlimited.
    /// </summary>
    public int? MaxMessagesInHistory { get; init; } = 50;
}

/// <summary>
///     Manages the conversation thread with Grok, handling messages and tool calls.
/// </summary>
public class GrokThread
{
    /// <summary>
    ///     The default model to use for conversations. Inexpensive and fast
    /// </summary>
    public const string DefaultModel = "grok-4-fast";

    private static int GetMaxTokens(string model)
    {
        if (model.IndexOf("vision", StringComparison.OrdinalIgnoreCase) >= 0) return 32768;
        if (model.IndexOf("grok-4-fast", StringComparison.OrdinalIgnoreCase) >= 0) return 2000000;
        if (model.IndexOf("grok-4", StringComparison.OrdinalIgnoreCase) >= 0) return 256000;
        if (model.IndexOf("grok-3", StringComparison.OrdinalIgnoreCase) >= 0) return 131072;
        if (model.IndexOf("grok-2", StringComparison.OrdinalIgnoreCase) >= 0) return 131072;
        if (model.IndexOf("grok-code-fast", StringComparison.OrdinalIgnoreCase) >= 0) return 256000;
        return 131072; // Default fallback
    }

    private readonly GrokClient _client;
    private readonly Queue<GrokMessage> _conversationQueue = new();
    private readonly Dictionary<string, IGrokTool> _tools = new();
    private GrokMessage? _systemMessage;
    private string? _lastSystemInstruction;
    private bool _useDeveloperRole;

    private int _maxTokens = 131072; // Default, updated per model
    private GrokThreadOptions _options;
    private long _totalTokensUsed;

    private int HistoryCount => (_systemMessage != null ? 1 : 0) + _conversationQueue.Count;
    private IEnumerable<GrokMessage> FullHistory => _systemMessage != null ? new[] { _systemMessage }.Concat(_conversationQueue) : _conversationQueue;

    /// <summary>
    /// Initializes a new instance of GrokThread with default options.
    /// </summary>
    /// <param name="client">The GrokClient to use.</param>
    public GrokThread(GrokClient client) : this(client, new GrokThreadOptions()) { }

    /// <summary>
    /// Initializes a new instance of GrokThread with specified options.
    /// </summary>
    /// <param name="client">The GrokClient to use.</param>
    /// <param name="options">The options for configuring the thread.</param>
    public GrokThread(GrokClient client, GrokThreadOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options;
    }

    /// <summary>
    /// Gets the current thread options.
    /// </summary>
    public GrokThreadOptions CurrentOptions => _options;

    /// <summary>
    /// Updates the thread options. Changes take effect immediately for new operations.
    /// </summary>
    /// <param name="newOptions">The new options to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="newOptions"/> is null.</exception>
    public void UpdateOptions(GrokThreadOptions newOptions)
    {
        _options = newOptions ?? throw new ArgumentNullException(nameof(newOptions));
        ApplyOptionsToCurrentState();
    }

    /// <summary>
    /// Estimated tokens in the current thread history.
    /// </summary>
    public int ThreadTokens => EstimateTokens(FullHistory);

    /// <summary>
    /// Total tokens used across all requests in this thread instance.
    /// </summary>
    public long TotalTokensUsed => _totalTokensUsed;

    /// <summary>
    /// View into the History data backing the chat.
    /// </summary>
    public IReadOnlyCollection<GrokMessage> History => FullHistory.ToList();

    /// <summary>
    ///     Provide instruction to the system on how it should respond to the user.
    /// </summary>
    /// <param name="systemInstruction">The instruction systemInstruction to add.</param>
    public void AddSystemInstruction(string? systemInstruction)
    {
        _lastSystemInstruction = systemInstruction;
        _useDeveloperRole = false;
        if (systemInstruction != null) _systemMessage = new GrokSystemMessage { Content = systemInstruction };
    }

    /// <summary>
    ///     Provide developer-level instructions that the model should follow regardless of user input.
    ///     Similar to system instructions but with stronger instruction-following guarantees.
    /// </summary>
    /// <param name="developerInstruction">The developer instruction to add.</param>
    public void AddDeveloperInstruction(string? developerInstruction)
    {
        _lastSystemInstruction = developerInstruction;
        _useDeveloperRole = true;
        if (developerInstruction != null) _systemMessage = new GrokDeveloperMessage { Content = developerInstruction };
    }

    /// <summary>
    ///     Add to the History without having an API call. Next API hit will include this in the context
    /// </summary>
    /// <param name="message">User message to include on the next call</param>
    public void AddUserMessage(string message)
    {
        var userMessage = new GrokUserMessage
        {
            Content =
            [
                new GrokTextPart
                {
                    Text = message
                }
            ]
        };
        _conversationQueue.Enqueue(userMessage);
        TrimQueue();
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
            _conversationQueue.Clear();
            _systemMessage = null;
            if (_useDeveloperRole)
                AddDeveloperInstruction(_lastSystemInstruction);
            else
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
    /// Applies the current options to the existing conversation state, performing any necessary cleanup.
    /// </summary>
    private void ApplyOptionsToCurrentState()
    {
        TrimQueue();
    }

    private int EstimateTokens(IEnumerable<GrokMessage> messages)
    {
        var totalChars = messages.Sum(m => GetMessageText(m).Length);
        return (totalChars + 3) / 4;
    }

    private void TrimQueue()
    {
        if (!_options.MaxMessagesInHistory.HasValue) return;
        int maxConv = _options.MaxMessagesInHistory.Value - (_systemMessage != null ? 1 : 0);
        while (_conversationQueue.Count > maxConv)
        {
            _conversationQueue.Dequeue();
        }
    }

    private string GetMessageText(GrokMessage message)
    {
        if (message is GrokSystemMessage sys) return sys.Content ?? "";
        if (message is GrokDeveloperMessage dev) return dev.Content ?? "";
        if (message is GrokUserMessage user) return string.Join("", user.Content.OfType<GrokTextPart>().Select(tp => tp.Text));
        if (message is GrokAssistantMessage ass) return ass.Content ?? "";
        if (message is GrokToolMessage tool) return tool.Content ?? "";
        return "";
    }

    /// <summary>
    ///     Asks a question and processes the response, providing status updates and results via an IAsyncEnumerable.
    /// </summary>
    /// <param name="question">The question to ask.</param>
    /// <param name="files">Files to do an analysis on (optional).</param>
    /// <param name="model">The model to use (default: <see cref="DefaultModel"/>).</param>
    /// <param name="temperature">The temperature for response generation (default: 0).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An IAsyncEnumerable of GrokMessageBase containing status updates and responses.</returns>
    /// <exception cref="ArgumentException">Thrown if the question is null or empty.</exception>
    public async IAsyncEnumerable<GrokMessageBase> AskQuestion(
        string? question,
        List<byte[]>? files = null,
        string model = DefaultModel,
        float temperature = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(question))
            throw new ArgumentException("Question cannot be null or empty.", nameof(question));

        _conversationQueue.Enqueue(new GrokUserMessage { Content = [new GrokTextPart { Text = question }] });

        TrimQueue();

        _maxTokens = GetMaxTokens(model);
        var currentTokens = ThreadTokens;

        if (currentTokens >= _maxTokens * _options.ClearThresholdPercent / 100)
        {
            _conversationQueue.Clear();
            _systemMessage = null;
            AddSystemInstruction(_lastSystemInstruction);
            yield return new GrokServiceMessage("Conversation history cleared due to token limit.");
            yield return new GrokStreamState(StreamState.Done);
            yield break;
        }
        else if (_options.EnableCompression && currentTokens >= _maxTokens * _options.CompressionThresholdPercent / 100)
        {
            await CompressHistoryAsync(cancellationToken: cancellationToken);
            yield return new GrokServiceMessage("Conversation history compressed to manage tokens.");
        }

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

        await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
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
        var requestTokens = EstimateTokens(FullHistory);
        var toolCallsPending = true;

        while (toolCallsPending)
        {
            // Send "Thinking" status before making the API call
            channel.Writer.TryWrite(new GrokStreamState(StreamState.Thinking));

            var request = new GrokChatCompletionRequest
            {
                Messages = FullHistory.ToList(),
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
                Tool_choice = _tools.Any() ? Tool_choice.Auto : null,
            };

            channel.Writer.TryWrite(new GrokStreamState(StreamState.Streaming));
            var response = await _client.CreateChatCompletionAsync(request, cancellationToken);
            var choice = response.Choices.First();

            if (choice.Message.Tool_calls?.Count > 0)
            {
                // Execute all tool calls in parallel
                var toolTasks = new List<(GrokToolCall ToolCall, Task<string> ResultTask)>();
                
                foreach (var toolCall in choice.Message.Tool_calls)
                {
                    if (_tools.TryGetValue(toolCall.Function.Name, out var tool))
                    {
                        toolTasks.Add((toolCall, tool.ExecuteAsync(toolCall.Function.Arguments)));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Tool '{toolCall.Function.Name}' not found.");
                    }
                }

                channel.Writer.TryWrite(new GrokStreamState(StreamState.CallingTool));

                // Await all tool executions
                await Task.WhenAll(toolTasks.Select(t => t.ResultTask));

                // Process results in order
                foreach (var (toolCall, resultTask) in toolTasks)
                {
                    var result = await resultTask;
                    channel.Writer.TryWrite(new GrokToolResponse(toolCall.Function.Name, result));
                    _conversationQueue.Enqueue(new GrokToolMessage { Content = result, Tool_call_id = toolCall.Id });
                    TrimQueue();
                }
            }
            else
            {
                toolCallsPending = false;
                _conversationQueue.Enqueue(choice.Message);
                TrimQueue();

                // Surface citations if present
                if (response.Citations?.Count > 0)
                {
                    var citations = response.Citations.Select(url => new GrokCitation
                    {
                        Type = "url_citation",
                        Url = url
                    }).ToList();
                    channel.Writer.TryWrite(new GrokCitationMessage(citations));
                }

                // Send the final response to the channel
                channel.Writer.TryWrite(new GrokTextMessage(choice.Message.Content));
                channel.Writer.TryWrite(new GrokStreamState(StreamState.Done));
                channel.Writer.Complete();
            }
        }

        var responseTokens = EstimateTokens(new[] { FullHistory.Last() });
        _totalTokensUsed += requestTokens + responseTokens;
    }
}