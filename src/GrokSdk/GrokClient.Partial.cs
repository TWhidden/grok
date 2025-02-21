using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GrokSdk
{
    public partial class GrokClient
    {
        public GrokClient(HttpClient httpClient, string apiToken) : this(httpClient)
        {
            if (string.IsNullOrWhiteSpace(apiToken))
                throw new ArgumentNullException(nameof(apiToken), "API token cannot be null or empty.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            _httpClient.BaseAddress = new Uri("https://api.x.ai/v1");
        }

        /// <summary>
        /// Returns a streaming client configured with the existing HttpClient and API token.
        /// </summary>
        /// <returns>A new instance of GrokStreamingClient.</returns>
        public GrokStreamingClient GetStreamingClient()
        {
            return new GrokStreamingClient(_httpClient, JsonSerializerSettings);
        }

        /// <summary>
        /// A client for handling streaming responses from the Grok API.
        /// </summary>
        public class GrokStreamingClient
        {
            private readonly HttpClient _httpClient;
            private readonly JsonSerializerSettings _jsonSerializerSettings;

            // Events for real-time updates
            public event EventHandler? OnStreamStarted;
            public event EventHandler<ChatCompletionChunk>? OnChunkReceived;
            public event EventHandler? OnStreamCompleted;
            public event EventHandler<Exception>? OnStreamError;
            public event EventHandler<StreamState>? OnStateChanged;

            private StreamState _currentState = StreamState.None;

            internal GrokStreamingClient(HttpClient httpClient, JsonSerializerSettings jsonSerializerSettings)
            {
                _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
                _jsonSerializerSettings = jsonSerializerSettings ?? throw new ArgumentNullException(nameof(jsonSerializerSettings));
            }

            /// <summary>
            /// Starts streaming a chat completion response from the Grok API.
            /// </summary>
            /// <param name="request">The chat completion request with Stream set to true.</param>
            /// <returns>A task representing the streaming operation.</returns>
            /// <exception cref="ArgumentNullException">Thrown if the request is null.</exception>
            public async Task StartStreamAsync(ChatCompletionRequest request)
            {
                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                try
                {
                    // Ensure streaming is enabled
                    request.Stream = true;

                    // Notify that the stream is starting
                    UpdateState(StreamState.Thinking);
                    OnStreamStarted?.Invoke(this, EventArgs.Empty);

                    // Send the streaming request
                    HttpResponseMessage response = await SendStreamingRequestAsync(request);

                    // Process the SSE stream
                    await ProcessStreamAsync(response);

                    // Stream completed successfully
                    UpdateState(StreamState.Done);
                    OnStreamCompleted?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    UpdateState(StreamState.Error);
                    OnStreamError?.Invoke(this, ex);
                    throw; // Re-throw to allow caller to handle if needed
                }
            }

            private async Task<HttpResponseMessage> SendStreamingRequestAsync(ChatCompletionRequest request)
            {
                string jsonRequest = JsonConvert.SerializeObject(request, _jsonSerializerSettings);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync("v1/chat/completions", content);
                response.EnsureSuccessStatusCode();
                return response;
            }

            private async Task ProcessStreamAsync(HttpResponseMessage response)
            {
                using Stream stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    string? line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("data: "))
                    {
                        string json = line.Substring(6); // Remove "data: " prefix
                        if (json == "[DONE]")
                        {
                            break; // End of stream
                        }

                        try
                        {
                            ChatCompletionChunk chunk = JsonConvert.DeserializeObject<ChatCompletionChunk>(json)!;
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
    }

    // Enum to represent stream states
    public enum StreamState
    {
        None,
        Thinking,  // Before streaming starts (e.g., API is processing)
        Streaming, // Actively receiving chunks
        Done,      // Stream has completed successfully
        Error      // An error occurred
    }

    // Represents a chunk of the streaming response
    public class ChatCompletionChunk
    {
        /// <summary>
        /// Unique identifier for the chunk (required).
        /// </summary>
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Object type (required, e.g., "chat.completion.chunk").
        /// </summary>
        [JsonProperty("object", Required = Required.Always)]
        public string Object { get; set; } = string.Empty;

        /// <summary>
        /// Unix timestamp of when the chunk was created (required).
        /// </summary>
        [JsonProperty("created", Required = Required.Always)]
        public int Created { get; set; }

        /// <summary>
        /// Model used for generation (required).
        /// </summary>
        [JsonProperty("model", Required = Required.Always)]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// List of partial responses (required).
        /// </summary>
        [JsonProperty("choices", Required = Required.Always)]
        public List<ChoiceDelta> Choices { get; set; } = new List<ChoiceDelta>();

        /// <summary>
        /// Usage statistics (optional).
        /// </summary>
        [JsonProperty("usage", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public Usage? Usage { get; set; }

        /// <summary>
        /// Unique identifier for the system configuration (optional).
        /// </summary>
        [JsonProperty("system_fingerprint", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public string? SystemFingerprint { get; set; }
    }

    // Represents a partial choice in the streaming response
    public class ChoiceDelta
    {
        /// <summary>
        /// Index of the choice (required).
        /// </summary>
        [JsonProperty("index", Required = Required.Always)]
        public int Index { get; set; }

        /// <summary>
        /// Partial message data (required).
        /// </summary>
        [JsonProperty("delta", Required = Required.Always)]
        public MessageDelta Delta { get; set; } = new MessageDelta();

        /// <summary>
        /// Reason for completion (optional, e.g., "stop").
        /// </summary>
        [JsonProperty("finish_reason", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public string? FinishReason { get; set; }
    }

    // Represents a partial message in the streaming response
    public class MessageDelta
    {
        /// <summary>
        /// Role of the message sender (optional).
        /// </summary>
        [JsonProperty("role", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public string? Role { get; set; }

        /// <summary>
        /// Partial content of the message (optional).
        /// </summary>
        [JsonProperty("content", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        public string? Content { get; set; }
    }
}