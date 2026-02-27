using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GrokSdk
{
    /// <summary>
    /// SSE event from the Responses API stream.
    /// </summary>
    public class ResponsesStreamEvent
    {
        /// <summary>
        /// The event type (e.g., "response.created", "response.output_text.delta", "response.completed").
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// The raw JSON data payload for this event.
        /// </summary>
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// Parsed JSON data (lazy-loaded).
        /// </summary>
        public JObject? ParsedData => string.IsNullOrEmpty(Data) ? null : JObject.Parse(Data);
    }

    /// <summary>
    /// Represents a text delta from the Responses API stream.
    /// </summary>
    public class ResponsesTextDelta
    {
        /// <summary>
        /// The output item index.
        /// </summary>
        public int OutputIndex { get; set; }

        /// <summary>
        /// The content index within the output item.
        /// </summary>
        public int ContentIndex { get; set; }

        /// <summary>
        /// The text fragment.
        /// </summary>
        public string Delta { get; set; } = string.Empty;
    }

    /// <summary>
    /// A streaming client for the Responses API (/v1/responses) using Server-Sent Events.
    /// Handles the Responses API SSE event format which differs from Chat Completions streaming.
    /// </summary>
    /// <remarks>
    /// Responses API SSE events include:
    /// - response.created: Response object created
    /// - response.in_progress: Response processing started
    /// - response.output_item.added: New output item (message, tool call, etc.)
    /// - response.output_text.delta: Incremental text content
    /// - response.output_text.done: Text content complete
    /// - response.content_part.added/done: Content part lifecycle
    /// - response.output_item.done: Output item complete
    /// - response.completed: Full response complete
    /// - response.failed: Error occurred
    /// </remarks>
    public class GrokResponsesStreamingClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        internal GrokResponsesStreamingClient(HttpClient httpClient, JsonSerializerSettings jsonSerializerSettings)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _jsonSerializerSettings = jsonSerializerSettings ?? throw new ArgumentNullException(nameof(jsonSerializerSettings));
        }

        /// <summary>
        /// Fired when the response is created.
        /// </summary>
        public event EventHandler<GrokResponseResponse>? OnResponseCreated;

        /// <summary>
        /// Fired for each text delta received during streaming.
        /// </summary>
        public event EventHandler<ResponsesTextDelta>? OnTextDelta;

        /// <summary>
        /// Fired when the complete response text for an output item is done.
        /// </summary>
        public event EventHandler<string>? OnTextDone;

        /// <summary>
        /// Fired for each raw SSE event (for advanced consumers).
        /// </summary>
        public event EventHandler<ResponsesStreamEvent>? OnEvent;

        /// <summary>
        /// Fired when an output item is added (message, function_call, search, etc.).
        /// </summary>
        public event EventHandler<JObject>? OnOutputItemAdded;

        /// <summary>
        /// Fired when the response is fully completed.
        /// </summary>
        public event EventHandler<GrokResponseResponse>? OnResponseCompleted;

        /// <summary>
        /// Fired when an error occurs during streaming.
        /// </summary>
        public event EventHandler<Exception>? OnStreamError;

        /// <summary>
        /// Starts streaming a Responses API request.
        /// </summary>
        /// <param name="request">The response request with Stream set to true.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task StartStreamAsync(GrokResponseRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.Stream = true;

            try
            {
                var jsonRequest = JsonConvert.SerializeObject(request, _jsonSerializerSettings);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var httpResponse = await _httpClient.PostAsync("v1/responses", content, cancellationToken)
                    .ConfigureAwait(false);
                httpResponse.EnsureSuccessStatusCode();

                await ProcessStreamAsync(httpResponse, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                OnStreamError?.Invoke(this, ex);
                throw;
            }
        }

#if NETSTANDARD2_0
        /// <summary>
        /// Streams a Responses API request and returns events via a callback pattern (netstandard2.0 compatible).
        /// </summary>
        public async Task StreamAsync(GrokResponseRequest request, Action<ResponsesStreamEvent> eventCallback, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.Stream = true;

            var jsonRequest = JsonConvert.SerializeObject(request, _jsonSerializerSettings);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var httpResponse = await _httpClient.PostAsync("v1/responses", content, cancellationToken)
                .ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();

            using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            
            string? currentEventType = null;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) return;

                if (string.IsNullOrEmpty(line))
                {
                    currentEventType = null;
                    continue;
                }

                if (line.StartsWith("event: "))
                {
                    currentEventType = line.Substring(7).Trim();
                }
                else if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);
                    var evt = new ResponsesStreamEvent
                    {
                        EventType = currentEventType ?? "message",
                        Data = data
                    };
                    eventCallback(evt);
                }
            }
        }
#else
        /// <summary>
        /// Streams a Responses API request and yields events as an async enumerable.
        /// </summary>
        public async IAsyncEnumerable<ResponsesStreamEvent> StreamAsync(
            GrokResponseRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.Stream = true;

            var jsonRequest = JsonConvert.SerializeObject(request, _jsonSerializerSettings);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var httpResponse = await _httpClient.PostAsync("v1/responses", content, cancellationToken)
                .ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();

            await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            string? currentEventType = null;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) yield break;

                if (string.IsNullOrEmpty(line))
                {
                    currentEventType = null;
                    continue;
                }

                if (line.StartsWith("event: "))
                {
                    currentEventType = line.Substring(7).Trim();
                }
                else if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);
                    yield return new ResponsesStreamEvent
                    {
                        EventType = currentEventType ?? "message",
                        Data = data
                    };
                }
            }
        }
#endif

        private async Task ProcessStreamAsync(HttpResponseMessage httpResponse, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0
            using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
            await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
            using var reader = new StreamReader(stream);

            string? currentEventType = null;
            var textAccumulator = new StringBuilder();

            while (!reader.EndOfStream)
            {
#if NETSTANDARD2_0
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
#else
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#endif
                if (cancellationToken.IsCancellationRequested) return;

                if (string.IsNullOrEmpty(line))
                {
                    currentEventType = null;
                    continue;
                }

                if (line.StartsWith("event: "))
                {
                    currentEventType = line.Substring(7).Trim();
                    continue;
                }

                if (!line.StartsWith("data: ")) continue;

                var data = line.Substring(6);
                var eventType = currentEventType ?? "message";

                var evt = new ResponsesStreamEvent { EventType = eventType, Data = data };
                OnEvent?.Invoke(this, evt);

                try
                {
                    ProcessEvent(eventType, data, textAccumulator);
                }
                catch (Exception ex)
                {
                    OnStreamError?.Invoke(this, new Exception($"Failed to process event '{eventType}': {data}", ex));
                }
            }
        }

        private void ProcessEvent(string eventType, string data, StringBuilder textAccumulator)
        {
            switch (eventType)
            {
                case "response.created":
                    var created = JsonConvert.DeserializeObject<GrokResponseResponse>(data, _jsonSerializerSettings);
                    if (created != null)
                        OnResponseCreated?.Invoke(this, created);
                    break;

                case "response.output_item.added":
                    var itemData = JObject.Parse(data);
                    OnOutputItemAdded?.Invoke(this, itemData);
                    break;

                case "response.output_text.delta":
                    var deltaObj = JObject.Parse(data);
                    var textDelta = new ResponsesTextDelta
                    {
                        OutputIndex = deltaObj["output_index"]?.Value<int>() ?? 0,
                        ContentIndex = deltaObj["content_index"]?.Value<int>() ?? 0,
                        Delta = deltaObj["delta"]?.Value<string>() ?? string.Empty
                    };
                    textAccumulator.Append(textDelta.Delta);
                    OnTextDelta?.Invoke(this, textDelta);
                    break;

                case "response.output_text.done":
                    OnTextDone?.Invoke(this, textAccumulator.ToString());
                    textAccumulator.Clear();
                    break;

                case "response.completed":
                    var completed = JsonConvert.DeserializeObject<GrokResponseResponse>(data, _jsonSerializerSettings);
                    if (completed != null)
                        OnResponseCompleted?.Invoke(this, completed);
                    break;

                case "response.failed":
                    OnStreamError?.Invoke(this, new Exception($"Responses API stream failed: {data}"));
                    break;
            }
        }
    }
}
