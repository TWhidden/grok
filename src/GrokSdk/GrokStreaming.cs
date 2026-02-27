using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace GrokSdk;

/// <summary>
///     A client for handling streaming responses from the Grok API.
/// </summary>
public class GrokStreamingClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerSettings _jsonSerializerSettings;

    /// <summary>
    /// Current streaming state
    /// </summary>
    private StreamState _currentState = StreamState.None;

    internal GrokStreamingClient(HttpClient httpClient, JsonSerializerSettings jsonSerializerSettings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _jsonSerializerSettings =
            jsonSerializerSettings ?? throw new ArgumentNullException(nameof(jsonSerializerSettings));
    }

    /// <summary>
    /// Event indicating the stream has started
    /// </summary>
    public event EventHandler? OnStreamStarted;
    /// <summary>
    /// Event indicating the stream has a data chunk returned
    /// </summary>
    public event EventHandler<ChatCompletionChunk>? OnChunkReceived;
    /// <summary>
    /// Event indicating the stream has completed
    /// </summary>
    public event EventHandler<StreamCompletedEventArgs>? OnStreamCompleted;
    /// <summary>
    /// Event indicating the stream encountered an error
    /// </summary>
    public event EventHandler<Exception>? OnStreamError;
    /// <summary>
    /// Event indicating the current stream state
    /// </summary>
    public event EventHandler<StreamState>? OnStateChanged;
    /// <summary>
    /// Event indicating complete tool calls have been accumulated from the stream.
    /// Fired when <c>finish_reason</c> is "tool_calls" and all deltas have been collected.
    /// </summary>
    public event EventHandler<List<GrokToolCall>>? OnToolCallsReceived;

    /// <summary>
    ///     Starts streaming a chat completion response from the Grok API.
    /// </summary>
    /// <param name="request">The chat completion request with Stream set to true.</param>
    /// <param name="cancellationToken">Cancel the current request</param>
    /// <returns>A task representing the streaming operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the request is null.</exception>
    public async Task StartStreamAsync(GrokChatCompletionRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            // Ensure streaming is enabled
            request.Stream = true;

            var stopwatch = Stopwatch.StartNew();

            // Notify that the stream is starting
            UpdateState(StreamState.Thinking);
            OnStreamStarted?.Invoke(this, EventArgs.Empty);

            // Send the streaming request
            var response = await SendStreamingRequestAsync(request, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidOperationException("Response was null");

            // Process the SSE stream
            await ProcessStreamAsync(response, cancellationToken);

            // Stream completed successfully
            UpdateState(StreamState.Done);
            OnStreamCompleted?.Invoke(this, new StreamCompletedEventArgs(stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            UpdateState(StreamState.Error);
            OnStreamError?.Invoke(this, ex);
            throw; // Re-throw to allow caller to handle if needed
        }
    }

    private async Task<HttpResponseMessage> SendStreamingRequestAsync(
        GrokChatCompletionRequest request, 
        CancellationToken cancellationToken
        )
    {
        var jsonRequest = JsonConvert.SerializeObject(request, _jsonSerializerSettings);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("v1/chat/completions", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private async Task ProcessStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
#if NETSTANDARD2_0
        using var stream = await response.Content.ReadAsStreamAsync();
#else
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#endif
        using var reader = new StreamReader(stream);

        var toolCallAccumulator = new StreamingToolCallAccumulator();

        while (!reader.EndOfStream)
        {
#if NETSTANDARD2_0
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
#else
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#endif
            if (cancellationToken.IsCancellationRequested)
            {
                UpdateState(StreamState.Error);
                OnStreamError?.Invoke(this, new OperationCanceledException("Stream was canceled."));
                return;
            }

            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("data: "))
            {
                var json = line.Substring(6); // Remove "data: " prefix
                if (json == "[DONE]")
                {
                    // If we accumulated tool calls, emit them before finishing
                    if (toolCallAccumulator.HasToolCalls)
                    {
                        OnToolCallsReceived?.Invoke(this, toolCallAccumulator.GetToolCalls());
                    }
                    break;
                }

                try
                {
                    var chunk = JsonConvert.DeserializeObject<ChatCompletionChunk>(json)!;

                    // Accumulate tool call deltas if present
                    if (chunk.Choices?.Count > 0)
                    {
                        var choice = chunk.Choices[0];
                        if (choice.Delta?.ToolCalls != null)
                        {
                            foreach (var toolCallDelta in choice.Delta.ToolCalls)
                            {
                                toolCallAccumulator.AddDelta(toolCallDelta);
                            }
                            UpdateState(StreamState.CallingTool);
                        }

                        // Check if tool calls are complete
                        if (choice.FinishReason == "tool_calls" && toolCallAccumulator.HasToolCalls)
                        {
                            OnToolCallsReceived?.Invoke(this, toolCallAccumulator.GetToolCalls());
                            toolCallAccumulator.Reset();
                        }
                    }

                    UpdateState(StreamState.Streaming);
                    OnChunkReceived?.Invoke(this, chunk);
                }
                catch (Exception ex)
                {
                    UpdateState(StreamState.Error);
                    OnStreamError?.Invoke(this, new Exception($"Failed to parse chunk: {json}", ex));
                }
            }
        }
    }

    private void UpdateState(StreamState newState)
    {
        if (_currentState != newState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(this, _currentState);
        }
    }
}

// Enum to represent stream states
public enum StreamState
{
    None, // Initial State
    Thinking, // Before streaming starts (e.g., API is processing)
    CallingTool, // When a tool is being called
    Streaming, // Actively receiving chunks
    Done, // Stream has completed successfully
    Error // An error occurred
}

// Represents a chunk of the streaming response
public class ChatCompletionChunk
{
    /// <summary>
    ///     Unique identifier for the chunk (required).
    /// </summary>
    [JsonProperty("id", Required = Required.Always)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Object type (required, e.g., "chat.completion.chunk").
    /// </summary>
    [JsonProperty("object", Required = Required.Always)]
    public string Object { get; set; } = string.Empty;

    /// <summary>
    ///     Unix timestamp of when the chunk was created (required).
    /// </summary>
    [JsonProperty("created", Required = Required.Always)]
    public int Created { get; set; }

    /// <summary>
    ///     Model used for generation (required).
    /// </summary>
    [JsonProperty("model", Required = Required.Always)]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    ///     List of partial responses (required).
    /// </summary>
    [JsonProperty("choices", Required = Required.Always)]
    public List<ChoiceDelta> Choices { get; set; } = new();

    /// <summary>
    ///     Usage statistics (optional).
    /// </summary>
    [JsonProperty("usage", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
    public GrokUsage? Usage { get; set; }

    /// <summary>
    ///     Unique identifier for the system configuration (optional).
    /// </summary>
    [JsonProperty("system_fingerprint", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
    public string? SystemFingerprint { get; set; }
}

// Represents a partial choice in the streaming response
public class ChoiceDelta
{
    /// <summary>
    ///     Index of the choice (required).
    /// </summary>
    [JsonProperty("index", Required = Required.Always)]
    public int Index { get; set; }

    /// <summary>
    ///     Partial message data (required).
    /// </summary>
    [JsonProperty("delta", Required = Required.Always)]
    public MessageDelta Delta { get; set; } = new();

    /// <summary>
    ///     Reason for completion (optional, e.g., "stop").
    /// </summary>
    [JsonProperty("finish_reason", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
    public string? FinishReason { get; set; }
}

// Represents a partial message in the streaming response
public class MessageDelta
{
    /// <summary>
    ///     Role of the message sender (optional).
    /// </summary>
    [JsonProperty("role", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
    public string? Role { get; set; }

    /// <summary>
    ///     Partial content of the message (optional).
    /// </summary>
    [JsonProperty("content", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
    public string? Content { get; set; }

    /// <summary>
    ///     Partial tool call data (optional). Present when the model is requesting tool calls.
    /// </summary>
    [JsonProperty("tool_calls", Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
    public List<ToolCallDelta>? ToolCalls { get; set; }
}

/// <summary>
/// Represents a partial tool call in a streaming delta.
/// Tool call arguments arrive incrementally across multiple chunks.
/// </summary>
public class ToolCallDelta
{
    /// <summary>
    /// Index of this tool call in the tool_calls array.
    /// </summary>
    [JsonProperty("index")]
    public int Index { get; set; }

    /// <summary>
    /// Tool call ID (only present in the first chunk for this tool call).
    /// </summary>
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string? Id { get; set; }

    /// <summary>
    /// The type of tool call (e.g., "function"). Only present in the first chunk.
    /// </summary>
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    /// <summary>
    /// Partial function call data.
    /// </summary>
    [JsonProperty("function", NullValueHandling = NullValueHandling.Ignore)]
    public ToolCallFunctionDelta? Function { get; set; }
}

/// <summary>
/// Partial function data within a streaming tool call delta.
/// </summary>
public class ToolCallFunctionDelta
{
    /// <summary>
    /// Function name (only present in the first chunk).
    /// </summary>
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    /// <summary>
    /// Partial arguments fragment. Accumulated across chunks to form complete JSON arguments.
    /// </summary>
    [JsonProperty("arguments", NullValueHandling = NullValueHandling.Ignore)]
    public string? Arguments { get; set; }
}

/// <summary>
/// Accumulates tool call deltas from streaming chunks into complete tool calls.
/// </summary>
public class StreamingToolCallAccumulator
{
    private readonly Dictionary<int, AccumulatedToolCall> _toolCalls = new();

    /// <summary>
    /// Process a tool call delta from a streaming chunk.
    /// </summary>
    /// <param name="delta">The tool call delta to accumulate.</param>
    public void AddDelta(ToolCallDelta delta)
    {
        if (!_toolCalls.TryGetValue(delta.Index, out var accumulated))
        {
            accumulated = new AccumulatedToolCall();
            _toolCalls[delta.Index] = accumulated;
        }

        if (delta.Id != null)
            accumulated.Id = delta.Id;

        if (delta.Type != null)
            accumulated.Type = delta.Type;

        if (delta.Function?.Name != null)
            accumulated.FunctionName = delta.Function.Name;

        if (delta.Function?.Arguments != null)
            accumulated.ArgumentsBuilder.Append(delta.Function.Arguments);
    }

    /// <summary>
    /// Gets the complete accumulated tool calls.
    /// </summary>
    /// <returns>List of complete tool calls with accumulated arguments.</returns>
    public List<GrokToolCall> GetToolCalls()
    {
        var result = new List<GrokToolCall>();
        foreach (var kvp in _toolCalls.OrderBy(k => k.Key))
        {
            var acc = kvp.Value;
            result.Add(new GrokToolCall
            {
                Id = acc.Id ?? string.Empty,
                Type = GrokToolCallType.Function,
                Function = new Function
                {
                    Name = acc.FunctionName ?? string.Empty,
                    Arguments = acc.ArgumentsBuilder.ToString()
                }
            });
        }
        return result;
    }

    /// <summary>
    /// Whether any tool calls have been accumulated.
    /// </summary>
    public bool HasToolCalls => _toolCalls.Count > 0;

    /// <summary>
    /// Resets the accumulator for a new response.
    /// </summary>
    public void Reset() => _toolCalls.Clear();

    private class AccumulatedToolCall
    {
        public string? Id;
        public string? Type;
        public string? FunctionName;
        public readonly System.Text.StringBuilder ArgumentsBuilder = new();
    }
}

public class StreamCompletedEventArgs(TimeSpan duration) : EventArgs
{
    public TimeSpan Duration { get; } = duration;
}