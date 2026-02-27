using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GrokSdk
{
    /// <summary>
    /// Provides helper methods for deferred chat completions.
    /// Deferred completions allow submitting a request and retrieving the result later.
    /// Results are available for 24 hours and can only be retrieved once.
    /// </summary>
    public class GrokDeferredCompletion
    {
        private readonly GrokClient _client;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _pollInterval;

        /// <summary>
        /// Creates a new GrokDeferredCompletion helper.
        /// </summary>
        /// <param name="client">The GrokClient to use for API calls.</param>
        /// <param name="timeout">Maximum time to wait for a result. Defaults to 10 minutes.</param>
        /// <param name="pollInterval">Time between status checks. Defaults to 10 seconds.</param>
        public GrokDeferredCompletion(
            GrokClient client,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _timeout = timeout ?? TimeSpan.FromMinutes(10);
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Submits a deferred chat completion and waits for the result.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The completed chat completion response.</returns>
        /// <exception cref="TimeoutException">Thrown if the result is not ready within the timeout period.</exception>
        public async Task<GrokChatCompletionResponse> CreateAndWaitAsync(
            GrokChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            var requestId = await _client.CreateDeferredChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
            return await WaitForResultAsync(requestId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Submits a deferred chat completion and returns the request ID for manual polling.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The request ID.</returns>
        public async Task<string> SubmitAsync(
            GrokChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            return await _client.CreateDeferredChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves the result of a deferred completion. Returns null if not ready yet.
        /// </summary>
        /// <param name="requestId">The request ID from <see cref="SubmitAsync"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response if ready, or null if still processing.</returns>
        public async Task<GrokChatCompletionResponse?> TryGetResultAsync(
            string requestId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _client.GetDeferredChatCompletionAsync(requestId, cancellationToken).ConfigureAwait(false);
            }
            catch (GrokSdkException ex) when (ex.StatusCode == 202)
            {
                return null; // Not ready yet
            }
        }

        /// <summary>
        /// Waits for a deferred completion to be ready, polling at the configured interval.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The completed response.</returns>
        /// <exception cref="TimeoutException">Thrown if the result is not ready within the timeout period.</exception>
        public async Task<GrokChatCompletionResponse> WaitForResultAsync(
            string requestId,
            CancellationToken cancellationToken = default)
        {
            var deadline = DateTime.UtcNow + _timeout;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await TryGetResultAsync(requestId, cancellationToken).ConfigureAwait(false);
                if (result != null)
                    return result;

                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException($"Deferred completion {requestId} not ready after {_timeout.TotalMinutes} minutes.");
        }

        /// <summary>
        /// Convenience method: send a simple text prompt as a deferred completion and wait for the result.
        /// </summary>
        /// <param name="prompt">The user prompt.</param>
        /// <param name="model">Model to use. Defaults to "grok-4-fast".</param>
        /// <param name="systemInstruction">Optional system instruction.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response text.</returns>
        public async Task<string?> AskDeferredAsync(
            string prompt,
            string model = "grok-4-fast",
            string? systemInstruction = null,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<GrokMessage>();

            if (!string.IsNullOrEmpty(systemInstruction))
            {
                messages.Add(new GrokSystemMessage { Content = systemInstruction });
            }

            messages.Add(new GrokUserMessage
            {
                Content = new System.Collections.ObjectModel.Collection<GrokContent>
                {
                    new GrokTextPart { Text = prompt }
                }
            });

            var request = new GrokChatCompletionRequest
            {
                Messages = messages,
                Model = model,
                Stream = false
            };

            var response = await CreateAndWaitAsync(request, cancellationToken).ConfigureAwait(false);
            return response.Choices?.FirstOrDefault()?.Message?.Content;
        }
    }
}
