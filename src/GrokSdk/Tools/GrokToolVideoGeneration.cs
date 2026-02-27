using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrokSdk.Tools
{
    /// <summary>
    /// Arguments for the video generation tool.
    /// </summary>
    public class GrokToolVideoGenerationArgs
    {
        /// <summary>
        /// Text description of the video to generate.
        /// </summary>
        [JsonProperty("prompt")]
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// URL or base64 data URI of a source image for image-to-video generation.
        /// </summary>
        [JsonProperty("image_url")]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// URL of a source video for video editing. Max input length is 8.7 seconds.
        /// </summary>
        [JsonProperty("video_url")]
        public string? VideoUrl { get; set; }

        /// <summary>
        /// Video duration in seconds (1-15). Not supported for video editing.
        /// </summary>
        [JsonProperty("duration")]
        public int? Duration { get; set; }

        /// <summary>
        /// Aspect ratio: "1:1", "16:9", "9:16", "4:3", "3:4", "3:2", "2:3". Default: "16:9".
        /// Not supported for video editing.
        /// </summary>
        [JsonProperty("aspect_ratio")]
        public string? AspectRatio { get; set; }

        /// <summary>
        /// Resolution: "480p" or "720p". Default: "480p". Not supported for video editing.
        /// </summary>
        [JsonProperty("resolution")]
        public string? Resolution { get; set; }
    }

    /// <summary>
    /// Response from the video generation tool.
    /// </summary>
    public class GrokToolVideoGenerationResponse
    {
        /// <summary>
        /// The temporary URL of the generated video. Download promptly as URLs expire.
        /// </summary>
        [JsonProperty("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Duration of the generated video in seconds.
        /// </summary>
        [JsonProperty("duration")]
        public int? Duration { get; set; }

        /// <summary>
        /// Whether the generated video passed content moderation.
        /// </summary>
        [JsonProperty("respect_moderation")]
        public bool? RespectModeration { get; set; }

        /// <summary>
        /// The model used for generation.
        /// </summary>
        [JsonProperty("model")]
        public string? Model { get; set; }

        /// <summary>
        /// The generation status: "pending", "done", or "expired".
        /// </summary>
        [JsonProperty("status")]
        public string? Status { get; set; }

        /// <summary>
        /// The request ID for tracking this generation.
        /// </summary>
        [JsonProperty("request_id")]
        public string? RequestId { get; set; }

        /// <summary>
        /// An error message if the operation failed.
        /// </summary>
        [JsonProperty("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// A tool that generates videos from text prompts, animates images, or edits existing videos
    /// using xAI's Video Generation API.
    /// </summary>
    /// <remarks>
    /// Video generation is asynchronous — the tool submits a request and polls for results.
    /// Generation typically takes 1-5 minutes depending on duration, resolution, and prompt complexity.
    /// Supports text-to-video, image-to-video, and video editing modes.
    /// </remarks>
    public class GrokToolVideoGeneration : IGrokTool
    {
        /// <summary>
        /// The tool name used to identify this tool in GrokThread.
        /// </summary>
        public const string ToolName = "grok_tool_video_generation";

        /// <summary>
        /// Default model for video generation.
        /// </summary>
        public const string DefaultVideoModel = "grok-imagine-video";

        private readonly GrokClient _client;
        private readonly string _defaultModel;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _pollInterval;

        /// <summary>
        /// Creates a new GrokToolVideoGeneration instance.
        /// </summary>
        /// <param name="client">The GrokClient to use for API calls.</param>
        /// <param name="defaultModel">The model to use. Defaults to "grok-imagine-video".</param>
        /// <param name="timeout">Maximum time to wait for generation. Defaults to 10 minutes.</param>
        /// <param name="pollInterval">Time between status checks. Defaults to 5 seconds.</param>
        public GrokToolVideoGeneration(
            GrokClient client,
            string defaultModel = DefaultVideoModel,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _defaultModel = defaultModel;
            _timeout = timeout ?? TimeSpan.FromMinutes(10);
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        }

        /// <inheritdoc />
        public string Name => ToolName;

        /// <inheritdoc />
        public string Description =>
            "Generates videos from text prompts, animates images, or edits existing videos using xAI's video generation model.";

        /// <inheritdoc />
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                prompt = new { type = "string", description = "Text description of the video to generate." },
                image_url = new { type = "string", description = "URL or base64 data URI of source image for image-to-video." },
                video_url = new { type = "string", description = "URL of source video for video editing (max 8.7s)." },
                duration = new { type = "integer", minimum = 1, maximum = 15, description = "Video duration in seconds (1-15)." },
                aspect_ratio = new
                {
                    type = "string",
                    @enum = new[] { "1:1", "16:9", "9:16", "4:3", "3:4", "3:2", "2:3" },
                    description = "Aspect ratio. Default: 16:9."
                },
                resolution = new
                {
                    type = "string",
                    @enum = new[] { "480p", "720p" },
                    description = "Resolution. Default: 480p."
                }
            },
            required = new[] { "prompt" }
        };

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<GrokToolVideoGenerationArgs>(arguments);
                if (args == null || string.IsNullOrEmpty(args.Prompt))
                    return JsonConvert.SerializeObject(new GrokToolVideoGenerationResponse { Error = "Invalid or missing prompt." });

                var response = await GenerateAsync(args).ConfigureAwait(false);
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new GrokToolVideoGenerationResponse { Error = $"Video generation failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Starts a video generation and polls until completion or timeout.
        /// </summary>
        /// <param name="args">The video generation arguments.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The video generation response with URL when complete.</returns>
        public async Task<GrokToolVideoGenerationResponse> GenerateAsync(
            GrokToolVideoGenerationArgs args,
            CancellationToken cancellationToken = default)
        {
            if (args == null || string.IsNullOrEmpty(args.Prompt))
                throw new ArgumentException("Prompt is required.", nameof(args));

            // Step 1: Start the generation
            var request = new GrokVideoGenerationRequest
            {
                Model = _defaultModel,
                Prompt = args.Prompt,
                Duration = args.Duration ?? 5 // API requires duration between 1-15 seconds
            };

            if (!string.IsNullOrEmpty(args.AspectRatio))
                request.Aspect_ratio = ParseAspectRatio(args.AspectRatio!);

            if (!string.IsNullOrEmpty(args.Resolution))
                request.Resolution = ParseResolution(args.Resolution!);

            GrokVideoGenerationStartResponse startResponse;
            try
            {
                startResponse = await _client.CreateVideoGenerationAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new GrokToolVideoGenerationResponse { Error = $"Failed to start video generation: {ex.Message}" };
            }

            var requestId = startResponse.Request_id;

            // Step 2: Poll for result
            return await PollForResultAsync(requestId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Starts a video generation request without waiting for completion.
        /// Use <see cref="GetStatusAsync"/> to poll for results manually.
        /// </summary>
        /// <param name="args">The video generation arguments.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The request ID for polling.</returns>
        public async Task<string> StartAsync(
            GrokToolVideoGenerationArgs args,
            CancellationToken cancellationToken = default)
        {
            if (args == null || string.IsNullOrEmpty(args.Prompt))
                throw new ArgumentException("Prompt is required.", nameof(args));

            var request = new GrokVideoGenerationRequest
            {
                Model = _defaultModel,
                Prompt = args.Prompt,
                Duration = args.Duration ?? 5 // API requires duration between 1-15 seconds
            };

            if (!string.IsNullOrEmpty(args.ImageUrl))
                request.Image = new GrokImageUrl { Url = args.ImageUrl };

            if (!string.IsNullOrEmpty(args.VideoUrl))
                request.Video_url = args.VideoUrl;

            if (!string.IsNullOrEmpty(args.AspectRatio))
                request.Aspect_ratio = ParseAspectRatio(args.AspectRatio!);

            if (!string.IsNullOrEmpty(args.Resolution))
                request.Resolution = ParseResolution(args.Resolution!);

            var startResponse = await _client.CreateVideoGenerationAsync(request, cancellationToken).ConfigureAwait(false);
            return startResponse.Request_id;
        }

        /// <summary>
        /// Checks the status of a video generation request.
        /// </summary>
        /// <param name="requestId">The request ID from <see cref="StartAsync"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current status and video data if complete.</returns>
        public async Task<GrokToolVideoGenerationResponse> GetStatusAsync(
            string requestId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(requestId))
                throw new ArgumentException("Request ID is required.", nameof(requestId));

            try
            {
                var result = await _client.GetVideoGenerationAsync(requestId, cancellationToken).ConfigureAwait(false);
                return MapResult(result, requestId);
            }
            catch (GrokSdkException ex) when (ex.StatusCode == 202)
            {
                // 202 means video is still being generated
                return new GrokToolVideoGenerationResponse
                {
                    RequestId = requestId,
                    Status = "pending"
                };
            }
        }

        private async Task<GrokToolVideoGenerationResponse> PollForResultAsync(
            string requestId,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + _timeout;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                GrokVideoGenerationResult result;
                try
                {
                    result = await _client.GetVideoGenerationAsync(requestId, cancellationToken).ConfigureAwait(false);
                }
                catch (GrokSdkException ex) when (ex.StatusCode == 202)
                {
                    // 202 means still processing — wait and retry
                    await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                catch (Exception ex)
                {
                    return new GrokToolVideoGenerationResponse
                    {
                        RequestId = requestId,
                        Error = $"Failed to poll video status: {ex.Message}"
                    };
                }

                // 200 means video is complete
                return MapResult(result, requestId);
            }

            return new GrokToolVideoGenerationResponse
            {
                RequestId = requestId,
                Status = "timeout",
                Error = $"Video generation timed out after {_timeout.TotalMinutes} minutes."
            };
        }

        private static GrokToolVideoGenerationResponse MapResult(GrokVideoGenerationResult result, string requestId)
        {
            var response = new GrokToolVideoGenerationResponse
            {
                RequestId = requestId,
                Status = "done",
                Model = result.Model
            };

            if (result.Video != null)
            {
                response.Url = result.Video.Url;
                response.Duration = result.Video.Duration;
                response.RespectModeration = result.Video.Respect_moderation;
            }

            return response;
        }

        private static GrokVideoGenerationRequestAspect_ratio ParseAspectRatio(string value)
        {
            return value switch
            {
                "1:1" => GrokVideoGenerationRequestAspect_ratio._11,
                "16:9" => GrokVideoGenerationRequestAspect_ratio._169,
                "9:16" => GrokVideoGenerationRequestAspect_ratio._916,
                "4:3" => GrokVideoGenerationRequestAspect_ratio._43,
                "3:4" => GrokVideoGenerationRequestAspect_ratio._34,
                "3:2" => GrokVideoGenerationRequestAspect_ratio._32,
                "2:3" => GrokVideoGenerationRequestAspect_ratio._23,
                _ => GrokVideoGenerationRequestAspect_ratio._169
            };
        }

        private static GrokVideoGenerationRequestResolution ParseResolution(string value)
        {
            return value switch
            {
                "720p" => GrokVideoGenerationRequestResolution._720p,
                "480p" => GrokVideoGenerationRequestResolution._480p,
                _ => GrokVideoGenerationRequestResolution._480p
            };
        }
    }
}
