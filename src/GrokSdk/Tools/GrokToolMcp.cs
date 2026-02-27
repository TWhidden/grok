using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrokSdk.Tools
{
    /// <summary>
    /// Configuration for a single MCP server connection.
    /// </summary>
    public class GrokMcpServerConfig
    {
        /// <summary>
        /// The URL of the MCP server to connect to. Only Streaming HTTP and SSE transports are supported.
        /// </summary>
        [JsonProperty("server_url")]
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// A label to identify the server (used for tool call prefixing).
        /// </summary>
        [JsonProperty("server_label")]
        public string? ServerLabel { get; set; }

        /// <summary>
        /// A description of what the server provides.
        /// </summary>
        [JsonProperty("server_description")]
        public string? ServerDescription { get; set; }

        /// <summary>
        /// List of specific tool names to allow. Empty or null allows all tools.
        /// </summary>
        [JsonProperty("allowed_tool_names")]
        public List<string>? AllowedToolNames { get; set; }

        /// <summary>
        /// A token that will be set in the Authorization header on requests to the MCP server.
        /// </summary>
        [JsonProperty("authorization")]
        public string? Authorization { get; set; }

        /// <summary>
        /// Additional headers to include in requests to the MCP server.
        /// </summary>
        [JsonProperty("extra_headers")]
        public Dictionary<string, string>? ExtraHeaders { get; set; }
    }

    /// <summary>
    /// Arguments for the MCP tool when invoked by GrokThread.
    /// </summary>
    public class GrokToolMcpArgs
    {
        /// <summary>
        /// The user query or prompt to send to the MCP-enabled conversation.
        /// </summary>
        [JsonProperty("query")]
        public string Query { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from the MCP tool.
    /// </summary>
    public class GrokToolMcpResponse
    {
        /// <summary>
        /// The response text from the model.
        /// </summary>
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// List of MCP tool calls that were made during the response.
        /// </summary>
        [JsonProperty("tool_calls")]
        public List<GrokToolMcpCallInfo> ToolCalls { get; set; } = new List<GrokToolMcpCallInfo>();

        /// <summary>
        /// An error message if the operation failed.
        /// </summary>
        [JsonProperty("error")]
        public string? Error { get; set; }

        /// <summary>
        /// The status of the response.
        /// </summary>
        [JsonProperty("status")]
        public string? Status { get; set; }

        /// <summary>
        /// Citation annotations from the response.
        /// </summary>
        [JsonProperty("citations")]
        public List<GrokToolWebSearchCitation> Citations { get; set; } = new List<GrokToolWebSearchCitation>();
    }

    /// <summary>
    /// Information about an MCP tool call made during the response.
    /// </summary>
    public class GrokToolMcpCallInfo
    {
        /// <summary>
        /// The type of tool call (e.g., "mcp_call", "web_search_call").
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The name of the tool that was called.
        /// </summary>
        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The ID of the tool call.
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }
    }

    /// <summary>
    /// A tool that connects Grok to external MCP (Model Context Protocol) servers via the Responses API.
    /// MCP servers extend Grok's capabilities with custom third-party tools.
    /// </summary>
    /// <remarks>
    /// This tool uses the Responses API (/v1/responses) with tool type "mcp".
    /// xAI manages the MCP server connection and interaction on your behalf.
    /// Supports multi-server configurations and tool filtering via <see cref="GrokMcpServerConfig.AllowedToolNames"/>.
    /// </remarks>
    public class GrokToolMcp : IGrokTool
    {
        /// <summary>
        /// The tool name used to identify this tool in GrokThread.
        /// </summary>
        public const string ToolName = "grok_tool_mcp";

        private readonly GrokClient _client;
        private readonly string _defaultModel;
        private readonly List<GrokMcpServerConfig> _servers;

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
        /// Creates a new GrokToolMcp instance with a single MCP server.
        /// </summary>
        /// <param name="client">The GrokClient to use for API calls.</param>
        /// <param name="serverUrl">The URL of the MCP server.</param>
        /// <param name="serverLabel">Optional label for the server.</param>
        /// <param name="defaultModel">The model to use. Defaults to "grok-4-fast".</param>
        public GrokToolMcp(GrokClient client, string serverUrl, string? serverLabel = null, string defaultModel = "grok-4-fast")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("Server URL cannot be null or empty.", nameof(serverUrl));

            _defaultModel = defaultModel;
            _servers = new List<GrokMcpServerConfig>
            {
                new GrokMcpServerConfig { ServerUrl = serverUrl, ServerLabel = serverLabel }
            };
        }

        /// <summary>
        /// Creates a new GrokToolMcp instance with multiple MCP servers.
        /// </summary>
        /// <param name="client">The GrokClient to use for API calls.</param>
        /// <param name="servers">The MCP server configurations.</param>
        /// <param name="defaultModel">The model to use. Defaults to "grok-4-fast".</param>
        public GrokToolMcp(GrokClient client, List<GrokMcpServerConfig> servers, string defaultModel = "grok-4-fast")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (servers == null || servers.Count == 0)
                throw new ArgumentException("At least one MCP server configuration is required.", nameof(servers));

            _defaultModel = defaultModel;
            _servers = servers;
        }

        /// <inheritdoc />
        public string Name => ToolName;

        /// <inheritdoc />
        public string Description =>
            "Connects to external MCP (Model Context Protocol) servers to extend capabilities with custom tools from third parties.";

        /// <inheritdoc />
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "The query or prompt to process using MCP server tools." }
            },
            required = new[] { "query" }
        };

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<GrokToolMcpArgs>(arguments);
                if (args == null || string.IsNullOrEmpty(args.Query))
                    return JsonConvert.SerializeObject(new GrokToolMcpResponse { Error = "Invalid or missing query." });

                var response = await PerformMcpRequestAsync(args.Query).ConfigureAwait(false);
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new GrokToolMcpResponse { Error = $"MCP execution failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Performs a direct MCP request without going through the IGrokTool interface.
        /// Useful for calling the MCP tool programmatically with a simple string query.
        /// </summary>
        /// <param name="query">The query to send.</param>
        /// <returns>The parsed MCP response.</returns>
        public async Task<GrokToolMcpResponse> QueryAsync(string query)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            return await PerformMcpRequestAsync(query).ConfigureAwait(false);
        }

        private async Task<GrokToolMcpResponse> PerformMcpRequestAsync(string query)
        {
            var tools = BuildMcpTools();

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
                return new GrokToolMcpResponse { Error = $"MCP request failed: {ex.Message}" };
            }
        }

        private List<GrokResponseTool> BuildMcpTools()
        {
            var tools = new List<GrokResponseTool>();

            foreach (var server in _servers)
            {
                var tool = new GrokResponseTool
                {
                    Type = GrokResponseToolType.Mcp,
                    Server_url = server.ServerUrl
                };

                if (!string.IsNullOrEmpty(server.ServerLabel))
                    tool.Server_label = server.ServerLabel;

                if (!string.IsNullOrEmpty(server.ServerDescription))
                    tool.Server_description = server.ServerDescription;

                if (server.AllowedToolNames?.Count > 0)
                    tool.Allowed_tool_names = server.AllowedToolNames;

                if (!string.IsNullOrEmpty(server.Authorization))
                    tool.Authorization = server.Authorization;

                if (server.ExtraHeaders?.Count > 0)
                    tool.Extra_headers = server.ExtraHeaders;

                tools.Add(tool);
            }

            return tools;
        }

        private static GrokToolMcpResponse ParseResponse(GrokResponseResponse response)
        {
            var result = new GrokToolMcpResponse
            {
                Status = response.Status.ToString().ToLowerInvariant()
            };

            if (response.Output == null || response.Output.Count == 0)
            {
                result.Error = "No output received from MCP request.";
                return result;
            }

            var textParts = new List<string>();
            var citations = new List<GrokToolWebSearchCitation>();
            var toolCalls = new List<GrokToolMcpCallInfo>();

            foreach (var outputItem in response.Output)
            {
                // Collect tool call info
                var itemType = outputItem.Type.ToString().ToLowerInvariant();
                if (itemType.Contains("call"))
                {
                    toolCalls.Add(new GrokToolMcpCallInfo
                    {
                        Type = itemType,
                        Name = outputItem.Name,
                        Id = outputItem.Id
                    });
                }

                // Extract text content from message outputs
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
            result.ToolCalls = toolCalls;

            return result;
        }
    }
}
