using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrokSdk.Tools
{
    /// <summary>
    /// Arguments for the collections search tool when invoked by GrokThread.
    /// </summary>
    public class GrokToolCollectionsSearchArgs
    {
        /// <summary>
        /// The search query to run against the collections.
        /// </summary>
        [JsonProperty("query")]
        public string Query { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from the collections search tool.
    /// </summary>
    public class GrokToolCollectionsSearchResponse
    {
        /// <summary>
        /// The response text synthesized from search results.
        /// </summary>
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Citations from the collections search results.
        /// </summary>
        [JsonProperty("citations")]
        public List<GrokToolWebSearchCitation> Citations { get; set; } = new List<GrokToolWebSearchCitation>();

        /// <summary>
        /// An error message if the search failed.
        /// </summary>
        [JsonProperty("error")]
        public string? Error { get; set; }

        /// <summary>
        /// The status of the response.
        /// </summary>
        [JsonProperty("status")]
        public string? Status { get; set; }
    }

    /// <summary>
    /// A tool that searches user-managed collections (vector stores) using xAI's Responses API
    /// with the <c>file_search</c> tool type. Collections must be created and managed externally
    /// via the xAI console or Collections Management API.
    /// </summary>
    /// <remarks>
    /// Uses the Responses API (/v1/responses) with tool type "file_search".
    /// Requires one or more collection IDs (vector store IDs) to search against.
    /// Returns text with collection citations (collections:// URIs).
    /// </remarks>
    public class GrokToolCollectionsSearch : IGrokTool
    {
        /// <summary>
        /// The tool name used to identify this tool in GrokThread.
        /// </summary>
        public const string ToolName = "grok_tool_collections_search";

        private readonly GrokClient _client;
        private readonly string _defaultModel;
        private readonly List<string> _vectorStoreIds;
        private readonly int? _maxNumResults;

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
        /// Creates a new GrokToolCollectionsSearch instance.
        /// </summary>
        /// <param name="client">The GrokClient to use for API calls.</param>
        /// <param name="vectorStoreIds">One or more collection/vector store IDs to search.</param>
        /// <param name="maxNumResults">Maximum number of results to return per search (optional).</param>
        /// <param name="defaultModel">The model to use. Defaults to "grok-4-fast".</param>
        public GrokToolCollectionsSearch(
            GrokClient client,
            List<string> vectorStoreIds,
            int? maxNumResults = null,
            string defaultModel = "grok-4-fast")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (vectorStoreIds == null || vectorStoreIds.Count == 0)
                throw new ArgumentException("At least one vector store ID is required.", nameof(vectorStoreIds));

            _vectorStoreIds = vectorStoreIds;
            _maxNumResults = maxNumResults;
            _defaultModel = defaultModel;
        }

        /// <summary>
        /// Creates a new GrokToolCollectionsSearch instance with a single collection ID.
        /// </summary>
        /// <param name="client">The GrokClient to use for API calls.</param>
        /// <param name="vectorStoreId">The collection/vector store ID to search.</param>
        /// <param name="maxNumResults">Maximum number of results to return per search (optional).</param>
        /// <param name="defaultModel">The model to use. Defaults to "grok-4-fast".</param>
        public GrokToolCollectionsSearch(
            GrokClient client,
            string vectorStoreId,
            int? maxNumResults = null,
            string defaultModel = "grok-4-fast")
            : this(client, new List<string> { vectorStoreId }, maxNumResults, defaultModel)
        {
            if (string.IsNullOrWhiteSpace(vectorStoreId))
                throw new ArgumentException("Vector store ID cannot be null or empty.", nameof(vectorStoreId));
        }

        /// <inheritdoc />
        public string Name => ToolName;

        /// <inheritdoc />
        public string Description =>
            "Searches user-managed collections (vector stores) for relevant documents and information using xAI's file_search tool.";

        /// <inheritdoc />
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "The search query to run against the collections." }
            },
            required = new[] { "query" }
        };

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<GrokToolCollectionsSearchArgs>(arguments);
                if (args == null || string.IsNullOrEmpty(args.Query))
                    return JsonConvert.SerializeObject(new GrokToolCollectionsSearchResponse { Error = "Invalid or missing query." });

                var response = await PerformSearchAsync(args.Query).ConfigureAwait(false);
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new GrokToolCollectionsSearchResponse { Error = $"Collections search failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Performs a direct collections search without going through the IGrokTool interface.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <returns>The parsed search response.</returns>
        public async Task<GrokToolCollectionsSearchResponse> QueryAsync(string query)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            return await PerformSearchAsync(query).ConfigureAwait(false);
        }

        private async Task<GrokToolCollectionsSearchResponse> PerformSearchAsync(string query)
        {
            var fileSearchTool = new GrokResponseTool
            {
                Type = GrokResponseToolType.File_search,
                Vector_store_ids = _vectorStoreIds
            };

            if (_maxNumResults.HasValue)
                fileSearchTool.Max_num_results = _maxNumResults.Value;

            var request = new GrokResponseRequest
            {
                Input = new List<GrokResponseInputMessage>
                {
                    new GrokResponseInputMessage
                    {
                        Role = GrokResponseInputMessageRole.User,
                        Content = query
                    }
                },
                Model = _defaultModel,
                Tools = new List<GrokResponseTool> { fileSearchTool },
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
                return new GrokToolCollectionsSearchResponse { Error = $"Collections search request failed: {ex.Message}" };
            }
        }

        private static GrokToolCollectionsSearchResponse ParseResponse(GrokResponseResponse response)
        {
            var result = new GrokToolCollectionsSearchResponse
            {
                Status = response.Status.ToString().ToLowerInvariant()
            };

            if (response.Output == null || response.Output.Count == 0)
            {
                result.Error = "No output received from collections search.";
                return result;
            }

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
