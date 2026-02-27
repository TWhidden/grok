using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrokSdk.Tools
{
    /// <summary>
    /// Arguments for the code execution tool.
    /// </summary>
    public class GrokToolCodeExecutionArgs
    {
        /// <summary>
        /// The prompt describing the computation or code to execute.
        /// </summary>
        [JsonProperty("query")]
        public string Query { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from the code execution tool.
    /// </summary>
    public class GrokToolCodeExecutionResponse
    {
        /// <summary>
        /// The response text, including any computed results.
        /// </summary>
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Code blocks that were generated and executed.
        /// </summary>
        [JsonProperty("code_blocks")]
        public List<GrokCodeBlock> CodeBlocks { get; set; } = new List<GrokCodeBlock>();

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
    }

    /// <summary>
    /// Represents a code block generated during code execution.
    /// </summary>
    public class GrokCodeBlock
    {
        /// <summary>
        /// The code that was generated.
        /// </summary>
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// The output produced by executing the code.
        /// </summary>
        [JsonProperty("output")]
        public string? Output { get; set; }

        /// <summary>
        /// The type of the output item (e.g., "code_interpreter_call").
        /// </summary>
        [JsonProperty("type")]
        public string? Type { get; set; }
    }

    /// <summary>
    /// A tool that executes code using xAI's Code Interpreter via the Responses API.
    /// The API runs code server-side in a sandboxed environment and returns results.
    /// </summary>
    /// <remarks>
    /// Uses the Responses API (/v1/responses) with tool type "code_interpreter".
    /// Useful for mathematical calculations, data analysis, charting, and general computation.
    /// The server-side sandbox typically supports Python with common data science libraries.
    /// </remarks>
    public class GrokToolCodeExecution : IGrokTool
    {
        /// <summary>
        /// The tool name used to identify this tool in GrokThread.
        /// </summary>
        public const string ToolName = "grok_tool_code_execution";

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
        /// Creates a new GrokToolCodeExecution instance.
        /// </summary>
        /// <param name="client">The GrokClient to use for API calls.</param>
        /// <param name="defaultModel">The model to use. Defaults to "grok-4-fast".</param>
        public GrokToolCodeExecution(GrokClient client, string defaultModel = "grok-4-fast")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _defaultModel = defaultModel;
        }

        /// <inheritdoc />
        public string Name => ToolName;

        /// <inheritdoc />
        public string Description =>
            "Executes code in a sandboxed environment for calculations, data analysis, and computation using xAI's Code Interpreter.";

        /// <inheritdoc />
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "The prompt describing the computation or code to execute, e.g., 'Calculate the factorial of 20' or 'Generate a chart of sin(x) from 0 to 2π'." }
            },
            required = new[] { "query" }
        };

        /// <inheritdoc />
        public async Task<string> ExecuteAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<GrokToolCodeExecutionArgs>(arguments);
                if (args == null || string.IsNullOrEmpty(args.Query))
                    return JsonConvert.SerializeObject(new GrokToolCodeExecutionResponse { Error = "Invalid or missing query." });

                var response = await PerformCodeExecutionAsync(args.Query).ConfigureAwait(false);
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new GrokToolCodeExecutionResponse { Error = $"Code execution failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Performs a direct code execution request without going through the IGrokTool interface.
        /// </summary>
        /// <param name="query">The prompt describing the computation to perform.</param>
        /// <returns>The parsed code execution response.</returns>
        public async Task<GrokToolCodeExecutionResponse> QueryAsync(string query)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            return await PerformCodeExecutionAsync(query).ConfigureAwait(false);
        }

        private async Task<GrokToolCodeExecutionResponse> PerformCodeExecutionAsync(string query)
        {
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
                Tools = new List<GrokResponseTool>
                {
                    new GrokResponseTool { Type = GrokResponseToolType.Code_interpreter }
                },
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
                return new GrokToolCodeExecutionResponse { Error = $"Code execution request failed: {ex.Message}" };
            }
        }

        private static GrokToolCodeExecutionResponse ParseResponse(GrokResponseResponse response)
        {
            var result = new GrokToolCodeExecutionResponse
            {
                Status = response.Status.ToString().ToLowerInvariant()
            };

            if (response.Output == null || response.Output.Count == 0)
            {
                result.Error = "No output received from code execution.";
                return result;
            }

            var textParts = new List<string>();
            var codeBlocks = new List<GrokCodeBlock>();

            foreach (var outputItem in response.Output)
            {
                var itemType = outputItem.Type.ToString().ToLowerInvariant();

                // Capture code interpreter calls
                if (itemType.Contains("code_interpreter"))
                {
                    var block = new GrokCodeBlock
                    {
                        Type = itemType
                    };

                    // The code_interpreter_call output item contains code and results
                    if (outputItem.AdditionalProperties != null)
                    {
                        if (outputItem.AdditionalProperties.TryGetValue("code", out var code))
                            block.Code = code?.ToString() ?? string.Empty;
                        if (outputItem.AdditionalProperties.TryGetValue("output", out var output))
                            block.Output = output?.ToString();
                    }

                    codeBlocks.Add(block);
                }

                // Extract text content from message outputs
                if (outputItem.Content != null)
                {
                    foreach (var content in outputItem.Content)
                    {
                        if (!string.IsNullOrEmpty(content.Text))
                            textParts.Add(content.Text);
                    }
                }
            }

            result.Text = string.Join("\n", textParts);
            result.CodeBlocks = codeBlocks;

            return result;
        }
    }
}
