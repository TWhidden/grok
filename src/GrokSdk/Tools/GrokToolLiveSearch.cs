using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrokSdk.Tools;

namespace GrokSdk.Tools
{
    /// <summary>
    /// Arguments for the live search tool, mapping to the Grok API's search parameters.
    /// </summary>
    public class GrokToolLiveSearchArgs
    {
        [JsonProperty("query")]
        public string Query { get; set; } = string.Empty;

        [JsonProperty("search_type")]
        public string SearchType { get; set; } = "web";

        [JsonProperty("from_date")]
        public string? FromDate { get; set; }

        [JsonProperty("to_date")]
        public string? ToDate { get; set; }

        [JsonProperty("max_results")]
        public int? MaxResults { get; set; }

        [JsonProperty("country")]
        public string? Country { get; set; }

        [JsonProperty("excluded_websites")]
        public List<string>? ExcludedWebsites { get; set; }

        [JsonProperty("safe_search")]
        public bool? SafeSearch { get; set; }

        [JsonProperty("x_handles")]
        public List<string>? XHandles { get; set; }

        [JsonProperty("rss_links")]
        public List<string>? RssLinks { get; set; }
    }

    /// <summary>
    /// Response structure for the live search tool.
    /// </summary>
    public class GrokToolLiveSearchResponse
    {
        [JsonProperty("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonProperty("citations")]
        public List<string> Citations { get; set; } = new List<string>();

        [JsonProperty("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// A tool that performs live searches using the Grok API.
    /// </summary>
    public class GrokToolLiveSearch(GrokClient client, string defaultModel = "grok-3-latest") : IGrokTool
    {
        public const string ToolName = "grok_tool_live_search";

        private readonly GrokClient _client = client ?? throw new ArgumentNullException(nameof(client));

        public string Name => ToolName;

        public string Description =>
            "Performs live searches for recent information from web, X, news, or RSS sources.";

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "The search query, e.g., 'latest tech news'." },
                search_type = new
                {
                    type = "string",
                    @enum = new[] { "web", "x", "news", "rss" },
                    @default = "web",
                    description = "Search type: 'web' (general web), 'x' (X platform), 'news' (news articles), 'rss' (RSS feeds)."
                },
                from_date = new { type = "string", format = "date", description = "Start date (YYYY-MM-DD), optional." },
                to_date = new { type = "string", format = "date", description = "End date (YYYY-MM-DD). This is up to, but not including. optional." },
                max_results = new { type = "integer", minimum = 1, description = "Max results, optional." },
                country = new { type = "string", description = "ISO alpha-2 country code for 'web' or 'news', optional." },
                excluded_websites = new
                {
                    type = "array",
                    items = new { type = "string" },
                    maxItems = 5,
                    description = "Websites to exclude for 'web' or 'news', max 5, optional."
                },
                safe_search = new { type = "boolean", description = "Enable safe search for 'web' or 'news', optional." },
                x_handles = new { type = "array", items = new { type = "string" }, description = "X handles for 'x' search, optional." },
                rss_links = new
                {
                    type = "array",
                    items = new { type = "string" },
                    maxItems = 1,
                    description = "RSS feed URLs for 'rss' search, max 1, optional."
                }
            },
            required = new[] { "query", "search_type" }
        };

        public async Task<string> ExecuteAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<GrokToolLiveSearchArgs>(arguments);
                if (args == null || string.IsNullOrEmpty(args.Query))
                    return JsonConvert.SerializeObject(new GrokToolLiveSearchResponse { Error = "Invalid or missing query." });

                var response = await PerformSearchAsync(args);
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new GrokToolLiveSearchResponse { Error = $"Execution failed: {ex.Message}" });
            }
        }

        private async Task<GrokToolLiveSearchResponse> PerformSearchAsync(GrokToolLiveSearchArgs args)
        {
            var validTypes = new HashSet<string> { "web", "x", "news", "rss" };
            if (!validTypes.Contains(args.SearchType))
                return new GrokToolLiveSearchResponse { Error = $"Invalid search_type: {args.SearchType}. Use: {string.Join(", ", validTypes)}." };

            if (args.ExcludedWebsites?.Count > 5)
                return new GrokToolLiveSearchResponse { Error = "excluded_websites exceeds 5 items." };
            if (args.RssLinks?.Count > 1)
                return new GrokToolLiveSearchResponse { Error = "rss_links exceeds 1 item." };
            if (args.MaxResults.HasValue && args.MaxResults < 1)
                return new GrokToolLiveSearchResponse { Error = "max_results must be at least 1." };

            DateTimeOffset? fromDate = null;
            if (args.FromDate != null)
            {
                if (DateTimeOffset.TryParseExact(args.FromDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTimeOffset parsedFromDate))
                {
                    fromDate = parsedFromDate;
                }
                else
                {
                    return new GrokToolLiveSearchResponse { Error = "from_date must be in YYYY-MM-DD format." };
                }
            }

            DateTimeOffset? toDate = null;
            if (args.ToDate != null)
            {
                if (DateTimeOffset.TryParseExact(args.ToDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTimeOffset parsedToDate))
                {
                    toDate = parsedToDate;
                }
                else
                {
                    return new GrokToolLiveSearchResponse { Error = "to_date must be in YYYY-MM-DD format." };
                }
            }

            GrokSource source = args.SearchType switch
            {
                "web" => new GrokWebSource
                {
                    Country = args.Country,
                    Excluded_websites = args.ExcludedWebsites,
                    Safe_search = args.SafeSearch ?? false
                },
                "x" => new GrokXSource { X_handles = args.XHandles },
                "news" => new GrokNewsSource
                {
                    Country = args.Country,
                    Excluded_websites = args.ExcludedWebsites,
                    Safe_search = args.SafeSearch ?? false
                },
                "rss" => new GrokRssSource { Links = args.RssLinks },
                _ => throw new InvalidOperationException("Unexpected search_type.")
            };

            var searchParams = new GrokSearchParameters
            {
                Mode = GrokSearchParametersMode.On,
                Return_citations = true,
                From_date = fromDate,
                To_date = toDate,
                Max_search_results = args.MaxResults,
                Sources = new List<GrokSource> { source }
            };

            var request = new GrokChatCompletionRequest
            {
                Messages = new List<GrokMessage>
                {
                    new GrokSystemMessage { Content = "Perform a live search and summarize the results." },
                    new GrokUserMessage { Content = new List<GrokContent> { new GrokTextPart { Text = args.Query } } }
                },
                Model = defaultModel,
                Search_parameters = searchParams
            };

            try
            {
                var response = await _client.CreateChatCompletionAsync(request);
                var choice = response.Choices.First();
                return new GrokToolLiveSearchResponse
                {
                    Summary = choice.Message.Content,
                    Citations = response.Citations.ToList()
                };
            }
            catch (Exception ex)
            {
                return new GrokToolLiveSearchResponse { Error = $"Search failed: {ex.Message}" };
            }
        }
    }
}