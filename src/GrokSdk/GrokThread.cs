using System.Threading.Channels;
using System.Text;

namespace GrokSdk;

/// <summary>
/// Base message type for different kinds of responses, nullable enabled
/// </summary>
public abstract record GrokMessage
{
}

/// <summary>
/// Text message type inheriting from GrokMessage
/// </summary>
/// <param name="Message"></param>
public record GrokTextMessage(string Message) : GrokMessage
{
}

/// <summary>
/// Service based messages from Grok
/// </summary>
/// <param name="Message"></param>
public record GrokServiceMessage(string Message) : GrokMessage
{
}

/// <summary>
/// Exception handle indicating a failure occured
/// </summary>
/// <param name="Exception"></param>
public record GrokError(Exception Exception) : GrokMessage
{

}

/// <summary>
/// The State of the stream
/// </summary>
/// <param name="StreamState"></param>
public record GrokStreamState(StreamState StreamState) : GrokMessage
{

}

// Manages the conversation thread
public class GrokThread(GrokClient client)
{
    private readonly GrokClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly List<Message> _history = [];

    /// <summary>
    ///     Asks a question and streams response text parts as an IAsyncEnumerable.
    /// </summary>
    /// <param name="question">The question to ask.</param>
    /// <param name="model">The model to use (default: "grok-2-latest").</param>
    /// <param name="temperature">The temperature for response generation (default: 0).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An IAsyncEnumerable of GrokMessage containing the question and responses.</returns>
    /// <exception cref="ArgumentException">Thrown if the question is null or empty.</exception>
    public IAsyncEnumerable<GrokMessage> AskQuestion(
        string? question,
        string? model = "grok-2-latest",
        float temperature = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(question))
            throw new ArgumentException("Question cannot be null or empty.", nameof(question));

        _history.Add(new UserMessage { Content = question });

        var channel = Channel.CreateUnbounded<GrokMessage>();

        _ = Task.Run(async () => await StreamResponsesAsync(
            new ChatCompletionRequest
            {
                Messages = _history,
                Model = model,
                Temperature = temperature,
                Stream = true
            },
            channel,
            cancellationToken), cancellationToken);

        return channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    ///     Streams the question and responses to the channel using the streaming client.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="channel">The channel to write messages to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task StreamResponsesAsync(
        ChatCompletionRequest request,
        Channel<GrokMessage> channel,
        CancellationToken cancellationToken)
    {
        var streamingClient = _client.GetStreamingClient();

        var responseBuilder = new StringBuilder();

        const int maxRetries = 3;
        const int defaultDelayMs = 1000; // 1 second default delay if Retry-After is missing
        int retryCount = 0;

        streamingClient.OnChunkReceived += OnChunkReceived;
        streamingClient.OnStreamCompleted += OnStreamCompleted;
        streamingClient.OnStreamError += OnStreamError;
        streamingClient.OnStateChanged += OnStateChanged;

        while (retryCount <= maxRetries)
        {
            try
            {
                await streamingClient.StartStreamAsync(request, cancellationToken);
                break; // Success, exit retry loop
            }
            catch (GrokSdkException ex) when (ex.StatusCode == 429 && retryCount < maxRetries)
            {
                retryCount++;
                channel.Writer.TryWrite(new GrokServiceMessage($"Rate limit hit, retrying ({retryCount}/{maxRetries})..."));

                // Check for Retry-After header
                int delayMs = defaultDelayMs;
                if (ex.Headers.TryGetValue("Retry-After", out var retryAfterValues))
                {
                    var retryAfter = retryAfterValues?.FirstOrDefault();
                    if (int.TryParse(retryAfter, out var seconds))
                    {
                        delayMs = seconds * 1000; // Convert seconds to milliseconds
                    }
                }

                await Task.Delay(delayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                OnStreamError(this, ex); // Handle other exceptions immediately
                break;
            }
        }

        streamingClient.OnChunkReceived -= OnChunkReceived;
        streamingClient.OnStreamCompleted -= OnStreamCompleted;
        streamingClient.OnStreamError -= OnStreamError;
        streamingClient.OnStateChanged -= OnStateChanged;

        return;

        return;

        void OnChunkReceived(object? sender, ChatCompletionChunk chunk)
        {
            var content = chunk.Choices.FirstOrDefault()?.Delta.Content;
            if (string.IsNullOrEmpty(content)) return;
            channel.Writer.TryWrite(new GrokTextMessage(content));
            responseBuilder.Append(content);
        }

        void OnStreamError(object? sender, Exception ex)
        {
            if (ex is OperationCanceledException)
                channel.Writer.TryWrite(new GrokTextMessage("Stream canceled"));
            else
                channel.Writer.TryWrite(new GrokError(ex));
            channel.Writer.TryComplete(ex);
        }

        void OnStateChanged(object? sender, StreamState e)
        {
            channel.Writer.TryWrite(new GrokStreamState(e));
        }

        void OnStreamCompleted(object? sender, EventArgs e)
        {
            // Record the full response in the chat history for thread context
            var fullResponse = responseBuilder.ToString();
            _history.Add(new AssistantMessage { Content = fullResponse });
            channel.Writer.Complete();
        }
    }
}