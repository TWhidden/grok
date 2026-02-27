using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrokSdk.Tools
{
    /// <summary>
    /// Arguments for the web search tool, supporting both web and X (Twitter) searches via the Responses API.
    /// </summary>
    public class GrokToolWebSearchArgs
    {
        /// <summary>
        /// The search query.
        /// </summary>
        [JsonProperty("query")]
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// The type of search to perform: "web", "x", or "both". Defaults to "web".
        /// </summary>
        [JsonProperty("search_type")]
        public string SearchType { get; set; } = "web";

        /// <summary>
        /// For web search - only search within these domains (max 5). Cannot be set with excluded_domains.
        /// </summary>
        [JsonProperty("allowed_domains")]
        public List<string>? AllowedDomains { get; set; }

        /// <summary>
        /// For web search - exclude these domains from search (max 5). Cannot be set with allowed_domains.
        /// </summary>
        [JsonProperty("excluded_domains")]
        public List<string>? ExcludedDomains { get; set; }

        /// <summary>
        /// For X search - only consider posts from these X handles (max 10). Cannot be set with excluded_x_handles.
        /// </summary>
        [JsonProperty("allowed_x_handles")]
        public List<string>? AllowedXHandles { get; set; }

        /// <summary>
        /// For X search - exclude posts from these X handles (max 10). Cannot be set with allowed_x_handles.
        /// </summary>
        [JsonProperty("excluded_x_handles")]
        public List<string>? ExcludedXHandles { get; set; }

        /// <summary>
        /// For X search - start date in ISO8601 format (YYYY-MM-DD).
        /// </summary>
        [JsonProperty("from_date")]
        public string? FromDate { get; set; }

        /// <summary>
        /// For X search - end date in ISO8601 format (YYYY-MM-DD).
        /// </summary>
        [JsonProperty("to_date")]
        public string? ToDate { get; set; }

        /// <summary>
        /// Enable analysis of images found during web/X search.
        /// </summary>
        [JsonProperty("enable_image_understanding")]
        public bool? EnableImageUnderstanding { get; set; }
    }

    /// <summary>
    /// Response structure for the web search tool.
    /// </summary>
    public class GrokToolWebSearchResponse
    {
        /// <summary>
        /// The response text from the search query.
        /// </summary>
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// List of citation URLs from sources used during the search.
        /// </summary>
        [JsonProperty("citations")]
        public List<GrokToolWebSearchCitation> Citations { get; set; } = new List<GrokToolWebSearchCitation>();

        /// <summary>
        /// An error message if the search failed.
        /// </summary>
        [JsonProperty("error")]
        public string? Error { get; set; }

        /// <summary>
        /// The status of the response (e.g., "completed", "incomplete").
        /// </summary>
        [JsonProperty("status")]
        public string? Status { get; set; }
    }

    /// <summary>
    /// Represents a citation from a search result.
    /// </summary>
    public class GrokToolWebSearchCitation
    {
        /// <summary>
        /// The URL of the citation source.
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// The citation display title/number.
        /// </summary>
        [JsonProperty("title")]
        public string? Title { get; set; }
    }

    /// <summary>
    /// A tool that searches the web or X (Twitter) using xAI's Responses API with built-in agentic tools.
    /// </summary>
    /// <remarks>
    /// This tool uses the Responses API (/v1/responses) which supports server-side web_search and x_search tools.
    /// The xAI API handles the search execution automatically and returns results with inline citations.
    /// This replaces the deprecated GrokToolLiveSearch which used the search_parameters API.
    /// </remarks>
    public class GrokToolWebSearch : IGrokTool
    {
        /// <summary>
        /// The tool name used to identify this tool in GrokThread.
        /// </summary>
        public const string ToolName = "grok_tool_web_search";

        private readonly GrokClient _client;
        private readonly string _defaultModel;

        /// <summary>
        /// Options for controlling Responses API behavior (previous_response_id, max_output_tokens, etc.).
        /// </summary>
        public GrokResponsesToolOptions? Options { get; set; }

        /// <summary>
        /// The last response ID from the most recent API call. Can be used for conversation continuation.
        /// </summary>
        public string? LastResponseId { get; private set; }

        /// <summary>
        /// Tool usage events from the most recent API call.
        /// </summary>
        public List<GrokToolUsage> LastToolUsages { get; private set; } = new List<GrokToolUsage>();

        /// <summary>
        /// Creates a new GrokToolWebSearch instance.
        /// </summary>
        /// <param name="client">The GrokClient to use for API calls.</param>
        /// <param name="defaultModel">The model to use for search requests. Defaults to "grok-4-fast".</param>
        public GrokToolWebSearch(GrokClient client, string defaultModel = "grok-4-fast")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _defaultModel = defaultModel;
        }

        /// <inheritdoc />
        public string Name => ToolName;

        /// <inheritdoc />
        public string Description =>
            "Searches the web or X (Twitter) for recent information using xAI's Responses API with built-in agentic search tools.";

        /// <inheritdoc />
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "The search query, e.g., 'latest tech news'." },
                search_type = new
                {
                    type = "string",
                    @enum = new[] { "web", "x", "both" },
                    @default = "web",
                    description = "Search type: 'web' (web search), 'x' (X/Twitter search), 'both' (web + X combined)."
                },
                allowed_domains = new
                {
                    type = "array",
                    items = new { type = "string" },
                    maxItems = 5,
                    description = "For web search - only search within these domains (max 5). Cannot be set with excluded_domains."
                },
                excluded_domains = new
                {
                    type = "array",
                    items = new { type = "string" },
                    maxItems = 5,
                    description = "For web search - exclude these domains (max 5). Cannot be set with allowed_domains."
                },
                allowed_x_handles = new
                {
                    type = "array",
                    items = new { type = "string" },
                    maxItems = 10,
                    description = "For X search - only consider posts from these handles (max 10)."
                },
                excluded_x_handles = new
                {
                    type = "array",
                    items = new { type = "string" },
                    maxItems = 10,
                    description = "For X search - exclude posts from these handles (max 10)."
                },
                from_date = new { type = "string", format = "date", description = "For X search - start date (YYYY-MM-DD)." },
                to_date = new { type = "string", format = "date", description = "For X search - end date (YYYY-MM-DD)." },
                enable_image_understanding = new { type = "boolean", description = "Enable analysis of images found during search." }
            },
            required = new[] { "query" }
        };

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<GrokToolWebSearchArgs>(arguments);
                if (args == null || string.IsNullOrEmpty(args.Query))
                    return JsonConvert.SerializeObject(new GrokToolWebSearchResponse { Error = "Invalid or missing query." });

                var response = await PerformSearchAsync(args).ConfigureAwait(false);
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new GrokToolWebSearchResponse { Error = $"Execution failed: {ex.Message}" });
            }
        }

        private async Task<GrokToolWebSearchResponse> PerformSearchAsync(GrokToolWebSearchArgs args)
        {
            var tools = BuildTools(args);

            var request = new GrokResponseRequest
            {
                Input = new List<GrokResponseInputMessage>
                {
                    new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.System, Content = "You are a helpful assistant that searches for information and provides accurate, cited responses." },
                    new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.User, Content = args.Query }
                },
                Model = _defaultModel,
                Tools = tools,
                Stream = false,
                Store = Options?.Store ?? false
            };

            GrokResponsesToolHelper.ApplyOptions(request, Options);

            try
            {
                var response = await _client.CreateResponseAsync(request).ConfigureAwait(false);
                LastResponseId = response.Id;
                LastToolUsages = GrokResponsesToolHelper.ExtractToolUsages(response);
                return ParseResponse(response);
            }
            catch (Exception ex)
            {
                return new GrokToolWebSearchResponse { Error = $"Search failed: {ex.Message}" };
            }
        }

        /// <summary>
        /// Builds the list of Responses API tools based on the search arguments.
        /// </summary>
        private static List<GrokResponseTool> BuildTools(GrokToolWebSearchArgs args)
        {
            var tools = new List<GrokResponseTool>();
            var searchType = args.SearchType?.ToLowerInvariant() ?? "web";

            if (searchType == "web" || searchType == "both")
            {
                var webTool = new GrokResponseTool { Type = GrokResponseToolType.Web_search };

                if (args.AllowedDomains?.Count > 0)
                    webTool.Allowed_domains = args.AllowedDomains.Take(5).ToList();

                if (args.ExcludedDomains?.Count > 0)
                    webTool.Excluded_domains = args.ExcludedDomains.Take(5).ToList();

                if (args.EnableImageUnderstanding == true)
                    webTool.Enable_image_understanding = true;

                tools.Add(webTool);
            }

            if (searchType == "x" || searchType == "both")
            {
                var xTool = new GrokResponseTool { Type = GrokResponseToolType.X_search };

                if (args.AllowedXHandles?.Count > 0)
                    xTool.Allowed_x_handles = args.AllowedXHandles.Take(10).ToList();

                if (args.ExcludedXHandles?.Count > 0)
                    xTool.Excluded_x_handles = args.ExcludedXHandles.Take(10).ToList();

                if (!string.IsNullOrEmpty(args.FromDate))
                    xTool.From_date = DateTimeOffset.Parse(args.FromDate);

                if (!string.IsNullOrEmpty(args.ToDate))
                    xTool.To_date = DateTimeOffset.Parse(args.ToDate);

                if (args.EnableImageUnderstanding == true)
                    xTool.Enable_image_understanding = true;

                tools.Add(xTool);
            }

            return tools;
        }

        /// <summary>
        /// Parses the Responses API response into a GrokToolWebSearchResponse.
        /// </summary>
        private static GrokToolWebSearchResponse ParseResponse(GrokResponseResponse response)
        {
            var result = new GrokToolWebSearchResponse
            {
                Status = response.Status.ToString().ToLowerInvariant()
            };

            if (response.Output == null || response.Output.Count == 0)
            {
                result.Error = "No output received from search.";
                return result;
            }

            // Extract text content from message output items
            var textParts = new List<string>();
            var citations = new List<GrokToolWebSearchCitation>();

            foreach (var outputItem in response.Output)
            {
                if (outputItem.Content != null)
                {
                    foreach (var content in outputItem.Content)
                    {
                        if (!string.IsNullOrEmpty(content.Text))
                            textParts.Add(content.Text);

                        // Extract citations from annotations
                        if (content.Annotations != null)
                        {
                            foreach (var annotation in content.Annotations)
                            {
                                if (!string.IsNullOrEmpty(annotation.Url))
                                {
                                    citations.Add(new GrokToolWebSearchCitation
                                    {
                                        Url = annotation.Url,
                                        Title = annotation.Title
                                    });
                                }
                            }
                        }
                    }
                }
            }

            result.Text = string.Join("\n", textParts);
            result.Citations = citations;

            return result;
        }
    }
}
