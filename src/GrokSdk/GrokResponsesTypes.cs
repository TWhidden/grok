using Newtonsoft.Json;
using System.Collections.Generic;

namespace GrokSdk
{
    /// <summary>
    /// Represents a citation from a Grok API response, providing source attribution
    /// for information in the response text.
    /// </summary>
    public class GrokCitation
    {
        /// <summary>
        /// The citation type (e.g., "url_citation").
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The source URL of the citation.
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// The display title for the citation.
        /// </summary>
        [JsonProperty("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Character position where the citation starts in the response text.
        /// </summary>
        [JsonProperty("start_index")]
        public int? StartIndex { get; set; }

        /// <summary>
        /// Character position where the citation ends (exclusive) in the response text.
        /// </summary>
        [JsonProperty("end_index")]
        public int? EndIndex { get; set; }
    }

    /// <summary>
    /// A GrokThread message containing citation information extracted from a response.
    /// </summary>
    /// <param name="Citations">List of citations from the response.</param>
    public record GrokCitationMessage(List<GrokCitation> Citations) : GrokMessageBase
    {
    }

    /// <summary>
    /// Represents a server-side tool usage event tracked by the Responses API.
    /// </summary>
    public class GrokToolUsage
    {
        /// <summary>
        /// The type of tool call (e.g., "web_search_call", "x_search_call", 
        /// "code_interpreter_call", "file_search_call", "mcp_call").
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for this tool usage event.
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /// <summary>
        /// The name of the tool that was called (for function/MCP tools).
        /// </summary>
        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The status of the tool call.
        /// </summary>
        [JsonProperty("status")]
        public string? Status { get; set; }

        /// <summary>
        /// Additional action details for the tool call (search queries, code, etc.).
        /// </summary>
        [JsonProperty("action")]
        public Dictionary<string, object>? Action { get; set; }
    }

    /// <summary>
    /// Options for configuring Responses API tool wrappers.
    /// </summary>
    public class GrokResponsesToolOptions
    {
        /// <summary>
        /// ID of a previous response to continue the conversation from.
        /// When set, the API uses the previous response's context without resending history.
        /// </summary>
        public string? PreviousResponseId { get; set; }

        /// <summary>
        /// Maximum number of output tokens to generate.
        /// </summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Maximum number of agentic turns the model can take. Limits tool call loops.
        /// </summary>
        public int? MaxTurns { get; set; }

        /// <summary>
        /// Whether to store the response for later retrieval (default: false for tool wrappers).
        /// When true, enables conversation continuation via <see cref="PreviousResponseId"/>.
        /// </summary>
        public bool Store { get; set; } = false;

        /// <summary>
        /// Additional data to include in the response.
        /// Common values: "reasoning.encrypted_content", "inline_citations".
        /// </summary>
        public List<string>? Include { get; set; }
    }
}
