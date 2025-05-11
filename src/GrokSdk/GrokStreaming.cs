using System.Diagnostics;
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
                if (json == "[DONE]") break; // End of stream

                try
                {
                    var chunk = JsonConvert.DeserializeObject<ChatCompletionChunk>(json)!;

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
}

public class StreamCompletedEventArgs(TimeSpan duration) : EventArgs
{
    public TimeSpan Duration { get; } = duration;
}