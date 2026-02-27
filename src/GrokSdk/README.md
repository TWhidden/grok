# GrokSdk 🚀

If you find this tool helpful or are able to use it in your projects, please drop me a note on [X](https://x.com/twhidden) to let me know—it encourages me to keep it going! 

[![NuGet](https://img.shields.io/nuget/v/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![Build Status](https://github.com/twhidden/grok/actions/workflows/ci.yml/badge.svg)](https://github.com/twhidden/grok/actions/workflows/ci.yml)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue.svg)](https://dotnet.microsoft.com/en-us/platform/dotnet-standard)

An unofficial .NET library for interacting with Grok's API, with `GrokThread` as its centerpiece. This library streamlines conversation management, tool integration (e.g., image generation and reasoning), and real-time streaming in .NET applications.

## Installation

Install via NuGet:

```
dotnet add package GrokSdk
```

Dependencies:
- `Newtonsoft.Json` (v13.0.0.0 or compatible) for JSON serialization.
- .NET-compatible `HttpClient` (e.g., from `System.Net.Http`).

## Core Feature: GrokThread

`GrokThread` powers this library, enabling multi-turn conversations, tool execution, and asynchronous streaming. The `GrokSdk.ConsoleDemo` project is a working console app you can run immediately — it registers all built-in tools and handles every message type:

```csharp
using GrokSdk;
using GrokSdk.Tools;
using Newtonsoft.Json;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Initialize the HTTP client and GrokClient
        string apiKey = GetApiKey();
        var httpClient = new HttpClient();
        var sdk = new GrokClient(httpClient, apiKey);

        // Create a GrokThread instance to manage the conversation
        var thread = new GrokThread(sdk);

        // Set initial model
        string currentModel = "grok-4-fast";

        // Register built-in tools
        thread.RegisterTool(new GrokToolImageGeneration(sdk));
        thread.RegisterTool(new GrokToolReasoning(sdk));
        thread.RegisterTool(new GrokToolWebSearch(sdk, currentModel));
        thread.RegisterTool(new GrokToolImageUnderstanding(sdk));
        thread.RegisterTool(new GrokToolCodeExecution(sdk));
        thread.RegisterTool(new GrokToolVideoGeneration(sdk));
        thread.RegisterTool(new GrokToolMcp(sdk, "https://mcp.deepwiki.com/mcp", "deepwiki"));

        // Welcome message with instructions
        Console.WriteLine("=== Grok Chat Console ===");
        Console.WriteLine($"Model: {currentModel}");
        Console.WriteLine("Commands: 'quit' to exit, 'm' to switch model");
        Console.WriteLine("Registered tools: Image Generation, Reasoning, Web Search,");
        Console.WriteLine("  Image Understanding, Code Execution, Video Generation, MCP (DeepWiki)");
        Console.WriteLine();

        // Main interaction loop
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("You: ");
            Console.ResetColor();
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "quit") break;

            if (input.Trim().ToLower() == "m")
            {
                currentModel = currentModel == "grok-3" ? "grok-4-fast" : "grok-3";
                Console.WriteLine($"Switched to model: {currentModel}");
                continue;
            }

            try
            {
                var messages = thread.AskQuestion(input, model: currentModel);

                await foreach (var message in messages)
                {
                    switch (message)
                    {
                        case GrokStreamState state:
                            switch (state.StreamState)
                            {
                                case StreamState.Thinking:
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Thinking...");
                                    break;
                                case StreamState.Streaming:
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.WriteLine("Streaming...");
                                    break;
                                case StreamState.Done:
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("Done.");
                                    break;
                                case StreamState.Error:
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Error Processing...");
                                    break;
                                case StreamState.CallingTool:
                                    Console.ForegroundColor = ConsoleColor.Magenta;
                                    Console.WriteLine("Calling Tool...");
                                    break;
                            }
                            Console.ResetColor();
                            break;

                        case GrokTextMessage textMessage:
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.Write(textMessage.Message);
                            Console.ResetColor();
                            break;

                        case GrokToolResponse toolResponse:
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"\n[Tool: {toolResponse.ToolName}]");
                            // Truncate long tool output for display
                            var toolOutput = toolResponse.ToolResponse;
                            if (toolOutput.Length > 500)
                                toolOutput = toolOutput[..500] + "... (truncated)";
                            Console.WriteLine(toolOutput);
                            Console.ResetColor();
                            break;

                        case GrokCitationMessage citationMessage:
                            Console.ForegroundColor = ConsoleColor.DarkCyan;
                            Console.WriteLine("\nSources:");
                            foreach (var citation in citationMessage.Citations)
                            {
                                var title = citation.Title ?? citation.Url;
                                Console.WriteLine($"  [{title}] {citation.Url}");
                            }
                            Console.ResetColor();
                            break;

                        case GrokServiceMessage serviceMessage:
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"[Service] {serviceMessage.Message}");
                            Console.ResetColor();
                            break;

                        case GrokError error:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[Error] {error.Exception.Message}");
                            Console.ResetColor();
                            break;
                    }
                }

                Console.ResetColor();
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("Goodbye!");
    }

    private static string GetApiKey()
    {
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), ".xai_key");
        if (File.Exists(filePath))
        {
            string key = File.ReadAllText(filePath).Trim();
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }
        }

        Console.WriteLine("Please enter your xAI API key:");
        string enteredKey = Console.ReadLine()?.Trim() ?? throw new Exception("Failed to get Console ReadLine");
        while (string.IsNullOrEmpty(enteredKey))
        {
            Console.WriteLine("API key cannot be empty. Please enter a valid key:");
            enteredKey = Console.ReadLine()?.Trim() ?? throw new Exception("Failed to get Console ReadLine");
        }

        File.WriteAllText(filePath, enteredKey);
        return enteredKey;
    }
}
```

### Key Features of GrokThread
- **Conversation Management**: Tracks message history for context-aware multi-turn responses.
- **Built-in Tools**: Seven built-in tools registered in the console demo: image generation, reasoning, web search, image understanding, code execution, video generation, and MCP.
- **Custom Tools**: Register your own tools via `GrokToolDefinition` (see below).
- **Streaming**: Real-time response streaming via `IAsyncEnumerable<GrokMessageBase>`.
- **Message Types**: Stream yields typed messages — `GrokTextMessage`, `GrokStreamState`, `GrokToolResponse`, `GrokCitationMessage`, `GrokServiceMessage`, `GrokError`.
- **Compression**: Shrinks history with `CompressHistoryAsync` to optimize tokens.
- **State Tracking**: Monitors states (`Thinking`, `Streaming`, `CallingTool`, `Done`, `Error`).
- **Developer Role**: Use `AddDeveloperInstruction()` for the `developer` message role (stronger instruction-following).
- **Parallel Tool Execution**: Multiple tool calls execute in parallel via `Task.WhenAll`.
- **Citation Surfacing**: `GrokCitationMessage` records emitted when citations are present in responses.

### Custom Tool Registration
You can register your own tools using `GrokToolDefinition`:

```csharp
thread.RegisterTool(new GrokToolDefinition(
    "weather_lookup",
    "Look up current weather for a given city",
    new { type = "object", properties = new { city = new { type = "string" } }, required = new[] { "city" } },
    async (args) =>
    {
        // Parse args JSON and return result
        var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(args);
        return $"{{\"temp\": \"72F\", \"city\": \"{parsed!["city"]}\"}}";
    }));
```

## Built-in Tools

### Image Generation Tool
The `GrokToolImageGeneration` tool allows Grok to generate images based on a text prompt. Users can specify the number of images and the response format (`url` or `base64`).

#### Usage Example
```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolImageGeneration(client));
thread.AddSystemInstruction("You have the ability to generate images from text descriptions.");

await foreach (var message in thread.AskQuestion("Generate an image of a sunset"))
{
    if (message is GrokToolResponse toolResponse && toolResponse.ToolName == GrokToolImageGeneration.ToolName)
    {
        Console.WriteLine($"Image URL: {toolResponse.ToolResponse}");
    }
}
```

### Reasoning Tool
The `GrokToolReasoning` enables Grok to perform complex reasoning on a given problem, with configurable effort levels ("low" or "high").

#### Usage Example
```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolReasoning(client));
thread.AddSystemInstruction("You have access to advanced reasoning capabilities for complex problems.");

await foreach (var message in thread.AskQuestion("Explain why the sky is blue with high effort"))
{
    if (message is GrokToolResponse toolResponse && toolResponse.ToolName == GrokToolReasoning.ToolName)
    {
        Console.WriteLine($"Reasoning: {toolResponse.ToolResponse}");
    }
}
```

### Web Search Tool (Responses API)
The `GrokToolWebSearch` tool enables Grok to perform real-time web and X/Twitter searches using xAI's Responses API (`/v1/responses`). It replaces the deprecated `GrokToolLiveSearch`.

#### Usage Example
```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolWebSearch(client));
thread.AddSystemInstruction("You can search the web and X/Twitter for real-time information.");

await foreach (var message in thread.AskQuestion("What's the latest news about AI?"))
{
    if (message is GrokToolResponse toolResponse && toolResponse.ToolName == GrokToolWebSearch.ToolName)
    {
        Console.WriteLine($"Search Results: {toolResponse.ToolResponse}");
    }
}
```

**Parameters:**
- `query`: The search query (required).
- `search_type`: Type of search - `"web"`, `"x"`, or `"both"` (default: `"web"`).
- `allowed_domains`: For web search - only search within these domains (max 5).
- `excluded_domains`: For web search - exclude these domains (max 5).
- `allowed_x_handles`: For X search - only search posts from these handles (max 10).
- `excluded_x_handles`: For X search - exclude posts from these handles (max 10).
- `from_date`: For X search - start date in ISO8601 format (YYYY-MM-DD).
- `to_date`: For X search - end date in ISO8601 format (YYYY-MM-DD).

## Responses API (Direct Usage)

The xAI Responses API (`/v1/responses`) provides server-side tool execution for web search, X search, code interpreter, and function calling. You can use it directly without `GrokThread`:

```csharp
using GrokSdk;

var httpClient = new HttpClient();
var client = new GrokClient(httpClient, "your-api-key-here");

// Web search with the Responses API
var request = new GrokResponseRequest
{
    Input = new List<GrokResponseInputMessage>
    {
        new GrokResponseInputMessage 
        { 
            Role = GrokResponseInputMessageRole.User, 
            Content = "What's happening in AI today?" 
        }
    },
    Model = "grok-4-fast",
    Tools = new List<GrokResponseTool>
    {
        new GrokResponseTool { Type = GrokResponseToolType.Web_search }
    }
};

var response = await client.CreateResponseAsync(request);

// Extract text and citations from the response
foreach (var item in response.Output)
{
    if (item.Type == GrokResponseOutputItemType.Message && item.Content != null)
    {
        foreach (var content in item.Content)
        {
            Console.WriteLine(content.Text);
            if (content.Annotations != null)
            {
                foreach (var annotation in content.Annotations)
                {
                    Console.WriteLine($"  Citation: {annotation.Url}");
                }
            }
        }
    }
}
```

### Supported Responses API Tools
| Tool Type | Description |
|-----------|-------------|
| `web_search` | Search the web with optional domain filtering |
| `x_search` | Search X/Twitter posts with handle and date filtering |
| `code_interpreter` | Execute code on xAI servers |
| `function` | Custom function calling (same concept as chat completions) |
| `file_search` | Search uploaded files with vector store IDs |
| `mcp` | Connect to external MCP (Model Context Protocol) servers |

### MCP Tool (Model Context Protocol)
The `GrokToolMcp` tool connects Grok to external MCP servers, extending its capabilities with custom third-party tools. xAI manages the MCP server connection on your behalf via the Responses API.

#### Usage Example
```csharp
var thread = new GrokThread(client);
// Connect to a public MCP server (e.g., DeepWiki for GitHub repo docs)
thread.RegisterTool(new GrokToolMcp(client, "https://mcp.deepwiki.com/mcp", "deepwiki"));
thread.AddSystemInstruction("You can search GitHub repository documentation using the MCP tool.");

await foreach (var message in thread.AskQuestion("What is the microsoft/TypeScript project about?"))
{
    if (message is GrokToolResponse toolResponse && toolResponse.ToolName == GrokToolMcp.ToolName)
    {
        Console.WriteLine($"MCP Result: {toolResponse.ToolResponse}");
    }
}
```

**Multi-server configuration:**
```csharp
var servers = new List<GrokMcpServerConfig>
{
    new GrokMcpServerConfig
    {
        ServerUrl = "https://mcp.deepwiki.com/mcp",
        ServerLabel = "deepwiki",
        ServerDescription = "Search GitHub repository documentation"
    },
    new GrokMcpServerConfig
    {
        ServerUrl = "https://your-private-mcp-server.com/mcp",
        ServerLabel = "internal",
        Authorization = "Bearer your-token-here",
        AllowedToolNames = new List<string> { "search", "lookup" }
    }
};
var tool = new GrokToolMcp(client, servers);
```

**Direct programmatic usage:**
```csharp
var tool = new GrokToolMcp(client, "https://mcp.deepwiki.com/mcp", "deepwiki");
var response = await tool.QueryAsync("What does the dotnet/runtime repository do?");
Console.WriteLine(response.Text);
```

**Parameters:**
- `server_url`: The URL of the MCP server (required). Only Streaming HTTP and SSE transports are supported.
- `server_label`: A label to identify the server.
- `server_description`: A description of what the server provides.
- `allowed_tool_names`: List of specific tool names to allow (empty allows all).
- `authorization`: Token for the Authorization header on requests to the MCP server.
- `extra_headers`: Additional headers to include in MCP server requests.

### Code Execution Tool (Code Interpreter)
The `GrokToolCodeExecution` tool runs code in xAI's sandboxed environment via the Responses API. Useful for mathematical calculations, data analysis, and general computation.

#### Usage Example
```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolCodeExecution(client));
thread.AddSystemInstruction("You can execute code for calculations and data analysis.");

await foreach (var message in thread.AskQuestion("Calculate the factorial of 20"))
{
    if (message is GrokToolResponse toolResponse && toolResponse.ToolName == GrokToolCodeExecution.ToolName)
    {
        Console.WriteLine($"Result: {toolResponse.ToolResponse}");
    }
}
```

**Direct programmatic usage:**
```csharp
var tool = new GrokToolCodeExecution(client);
var response = await tool.QueryAsync("What is 2 to the power of 100?");
Console.WriteLine(response.Text);
```

### Collections Search Tool (File Search)
The `GrokToolCollectionsSearch` tool searches uploaded documents in xAI vector stores using the Responses API's `file_search` tool. You must have vector store IDs from the xAI console.

#### Usage Example
```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolCollectionsSearch(client, "vs_abc123"));
thread.AddSystemInstruction("You can search uploaded documents to answer questions.");

await foreach (var message in thread.AskQuestion("What does the Q3 report say about revenue?"))
{
    if (message is GrokToolResponse toolResponse)
        Console.WriteLine($"Result: {toolResponse.ToolResponse}");
}
```

**Direct programmatic usage:**
```csharp
var tool = new GrokToolCollectionsSearch(client, new List<string> { "vs_abc123", "vs_def456" }, maxNumResults: 10);
var response = await tool.QueryAsync("Find all references to machine learning");
Console.WriteLine(response.Text);
foreach (var citation in response.Citations)
    Console.WriteLine($"  Source: {citation.Url} - {citation.Title}");
```

### Responses API Tool Options
All Responses API tool wrappers (`GrokToolWebSearch`, `GrokToolCodeExecution`, `GrokToolMcp`, `GrokToolCollectionsSearch`) support `GrokResponsesToolOptions` for advanced control:

```csharp
var tool = new GrokToolWebSearch(client);

// Set options before querying
tool.Options = new GrokResponsesToolOptions
{
    Store = true,                // Enable response storage for continuation
    MaxOutputTokens = 1000,      // Limit output tokens
    MaxTurns = 5,                // Limit agentic turns
    Include = new List<string> { "reasoning.encrypted_content" }
};

// After execution, access tracking data
var argsJson = JsonConvert.SerializeObject(new { query = "Latest AI news" });
await tool.ExecuteAsync(argsJson);
Console.WriteLine($"Response ID: {tool.LastResponseId}");

// Continue the conversation using previous response ID
tool.Options = new GrokResponsesToolOptions
{
    Store = true,
    PreviousResponseId = tool.LastResponseId
};
await tool.ExecuteAsync(JsonConvert.SerializeObject(new { query = "Tell me more about the first result" }));
```

### Tool Usage Tracking
After each API call, Responses API tool wrappers populate `LastToolUsages` with details about server-side tool calls:

```csharp
var tool = new GrokToolWebSearch(client);
var argsJson = JsonConvert.SerializeObject(new { query = "weather today" });
await tool.ExecuteAsync(argsJson);

foreach (var usage in tool.LastToolUsages)
{
    Console.WriteLine($"Tool: {usage.Type}, Status: {usage.Status}");
    if (usage.Action != null)
        Console.WriteLine($"  Action: {JsonConvert.SerializeObject(usage.Action)}");
}
```

### Citation Support in GrokThread
When the API returns citations (e.g., from web search), `GrokThread` emits `GrokCitationMessage` records in the async stream:

```csharp
await foreach (var message in thread.AskQuestion("What's the latest news about SpaceX?"))
{
    if (message is GrokTextMessage text)
        Console.Write(text.Message);
    else if (message is GrokCitationMessage citations)
    {
        Console.WriteLine("\nSources:");
        foreach (var citation in citations.Citations)
            Console.WriteLine($"  [{citation.Title}] {citation.Url}");
    }
}
```

### Responses API Streaming
The `GrokResponsesStreamingClient` provides real-time streaming from the Responses API using Server-Sent Events (SSE):

#### Event-based streaming
```csharp
var streamingClient = client.GetResponsesStreamingClient();
streamingClient.OnTextDelta += (s, delta) => Console.Write(delta.Delta);
streamingClient.OnTextDone += (s, fullText) => Console.WriteLine($"\n\nFull text: {fullText}");
streamingClient.OnResponseCompleted += (s, response) => Console.WriteLine($"Done! ID: {response.Id}");

var request = new GrokResponseRequest
{
    Input = new List<GrokResponseInputMessage>
    {
        new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.User, Content = "Tell me a story" }
    },
    Model = "grok-4-fast",
    Tools = new List<GrokResponseTool> { new GrokResponseTool { Type = GrokResponseToolType.Web_search } }
};

await streamingClient.StartStreamAsync(request);
```

#### IAsyncEnumerable streaming (.NET 8+)
```csharp
var streamingClient = client.GetResponsesStreamingClient();
var request = new GrokResponseRequest { /* ... */ };

await foreach (var evt in streamingClient.StreamAsync(request))
{
    if (evt.EventType == "response.output_text.delta")
        Console.Write(evt.ParsedData?["delta"]?.ToString());
}
```

### Streaming Tool Call Deltas (Chat Completions)
The `GrokStreamingClient` now accumulates streaming tool calls and fires `OnToolCallsReceived` when all deltas are collected:

```csharp
var streamingClient = client.GetStreamingClient();

streamingClient.OnToolCallsReceived += (s, toolCalls) =>
{
    foreach (var call in toolCalls)
        Console.WriteLine($"Tool call: {call.Function.Name}({call.Function.Arguments})");
};

streamingClient.OnChunkReceived += (s, chunk) =>
{
    if (chunk.Choices[0].Delta.Content != null)
        Console.Write(chunk.Choices[0].Delta.Content);
};

await streamingClient.StartStreamAsync(request, CancellationToken.None);
```

You can also use the `StreamingToolCallAccumulator` directly for manual accumulation:
```csharp
var accumulator = new StreamingToolCallAccumulator();
streamingClient.OnChunkReceived += (s, chunk) =>
{
    var toolCallDeltas = chunk.Choices[0].Delta.ToolCalls;
    if (toolCallDeltas != null)
        foreach (var delta in toolCallDeltas)
            accumulator.AddDelta(delta);
};
// After streaming completes:
var completedCalls = accumulator.GetToolCalls();
```

### Video Generation Tool
The `GrokToolVideoGeneration` tool generates videos from text prompts, animates images, or edits existing videos using xAI's video generation model (`grok-imagine-video`). Video generation is asynchronous — the tool submits a request and polls for results.

#### Usage Example
```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolVideoGeneration(client));
thread.AddSystemInstruction("You can generate videos from text descriptions.");

await foreach (var message in thread.AskQuestion("Generate a video of a cat playing with a ball"))
{
    if (message is GrokToolResponse toolResponse && toolResponse.ToolName == GrokToolVideoGeneration.ToolName)
    {
        Console.WriteLine($"Video: {toolResponse.ToolResponse}");
    }
}
```

**Direct programmatic usage:**
```csharp
var tool = new GrokToolVideoGeneration(client);

// Generate and wait for completion (polls automatically)
var result = await tool.GenerateAsync(new GrokToolVideoGenerationArgs 
{ 
    Prompt = "A peaceful ocean wave", 
    Duration = 5 
});
Console.WriteLine($"Video URL: {result.Url}");

// Or start and poll manually
var requestId = await tool.StartAsync(new GrokToolVideoGenerationArgs { Prompt = "A sunset" });
var status = await tool.GetStatusAsync(requestId);
```

**Parameters:**
- `prompt`: Text description of the video to generate (required).
- `image_url`: URL or base64 data URI of a source image for image-to-video.
- `video_url`: URL of a source video for video editing (max 8.7s).
- `duration`: Video duration in seconds (1-15). Default: 5.
- `aspect_ratio`: `"1:1"`, `"16:9"`, `"9:16"`, `"4:3"`, `"3:4"`, `"3:2"`, `"2:3"`. Default: `"16:9"`.
- `resolution`: `"480p"` or `"720p"`. Default: `"480p"`.

### Deferred Completions
The `GrokDeferredCompletion` helper enables asynchronous chat completions — submit a request and retrieve the result later. Useful for long-running or non-urgent tasks. Results are available for 24 hours.

#### Usage Example
```csharp
var deferred = new GrokDeferredCompletion(client);

// Simple: ask a question and wait for the result
string? answer = await deferred.AskDeferredAsync("What is the meaning of life?");
Console.WriteLine(answer);

// Advanced: submit and poll manually
var requestId = await deferred.SubmitAsync(new GrokChatCompletionRequest
{
    Messages = new List<GrokMessage>
    {
        new GrokUserMessage 
        { 
            Content = new Collection<GrokContent> 
            { 
                new GrokTextPart { Text = "Explain quantum computing" } 
            } 
        }
    },
    Model = "grok-3-mini-fast"
});

// Poll later
var result = await deferred.TryGetResultAsync(requestId);
if (result != null)
    Console.WriteLine(result.Choices[0].Message.Content);
```

### Structured Output
The `GrokStructuredOutput` helper constrains Grok's response to match a JSON schema, enabling type-safe responses.

#### Usage Example
```csharp
// Define a response type
public record WeatherInfo(string City, double Temperature, string Condition);

// Ask and get a typed response
var weather = await GrokStructuredOutput.AskAsJsonAsync<WeatherInfo>(
    client,
    "What's the weather in Tokyo?",
    model: "grok-3-mini-fast"
);
Console.WriteLine($"{weather.City}: {weather.Temperature}°, {weather.Condition}");

// Or manually create a response format for use with GrokThread
var format = GrokStructuredOutput.CreateJsonFormat<WeatherInfo>();
```

---

### Image Understanding Tool
The `GrokToolImageUnderstanding` tool allows Grok to analyze images and answer questions about them. It takes a prompt and an image (via URL or base64 data) and returns a description or answer based on the image content.

To use this tool, register it with your `GrokThread` instance:

```csharp  
var thread = new GrokThread(client);  
thread.RegisterTool(new GrokToolImageUnderstanding(client));  
thread.AddSystemInstruction("You can analyze images and answer questions about their content.");  
```

**Usage Example:**  
```csharp  
await foreach (var message in thread.AskQuestion("What's in this image? https://example.com/image.jpg"))  
{  
    if (message is GrokToolResponse toolResponse && toolResponse.ToolName == GrokToolImageUnderstanding.ToolName)  
    {  
        Console.WriteLine($"Image Description: {toolResponse.ToolResponse}");  
    }  
}  
```

**Parameters:**  
- `prompt`: The question or task related to the image (required).  
- `image_url`: The URL or base64-encoded image data (required).  
- `image_detail`: Optional, specifies the level of detail ("low" or "high", defaults to "low").

## Additional Examples

### Simple Chat
```csharp
var thread = new GrokThread(new GrokClient(new HttpClient(), "your-api-key-here"));
thread.AddSystemInstruction("You are a helpful assistant.");
await foreach (var response in thread.AskQuestion("Hello!"))
{
    if (response is GrokTextMessage text) Console.Write(text.Message);
}
```

### Running the Console Demo
The `GrokSdk.ConsoleDemo` project is a ready-to-run example with all tools registered:
```bash
cd src/GrokSdk.ConsoleDemo
dotnet run
```
It will prompt for your xAI API key on first run (saved to `.xai_key`). Try these sample prompts:
- "Generate an image of a sunset over mountains" (Image Generation)
- "Search the web for the latest AI news" (Web Search)
- "Calculate the first 50 Fibonacci numbers" (Code Execution)
- "What is the microsoft/TypeScript project about?" (MCP / DeepWiki)
- "Explain the P vs NP problem" (Reasoning)

## Supporting Classes

### GrokClient
Sets up the API connection:
```csharp
var client = new GrokClient(new HttpClient(), "your-api-key-here");
var thread = client.GetGrokThread();
```

### GrokStreamingClient (Chat Completions)
For low-level chat completions streaming:
```csharp
var streamingClient = client.GetStreamingClient();
streamingClient.OnChunkReceived += (s, chunk) => Console.Write(chunk.Choices[0].Delta.Content);
streamingClient.OnToolCallsReceived += (s, calls) =>
    calls.ForEach(c => Console.WriteLine($"Tool: {c.Function.Name}"));
await streamingClient.StartStreamAsync(new GrokChatCompletionRequest { /* ... */ }, CancellationToken.None);
```

### GrokResponsesStreamingClient (Responses API)
For low-level Responses API streaming:
```csharp
var streamingClient = client.GetResponsesStreamingClient();
streamingClient.OnTextDelta += (s, delta) => Console.Write(delta.Delta);
streamingClient.OnResponseCompleted += (s, response) => Console.WriteLine($"\nDone: {response.Id}");
await streamingClient.StartStreamAsync(new GrokResponseRequest { /* ... */ });
```

## Notes

- **Code Generation**: Built with NSwag v14.6.3.0 from `grok-api.yaml`. See [Grok API docs](https://docs.x.ai/api).
- **Serialization**: Uses `Newtonsoft.Json` for all API communication.
- **Errors**: Throws `GrokSdkException` with HTTP status code and response body details.
- **Targets**: .NET 8.0 and .NET Standard 2.0.

## Contributing

Visit [GitHub](https://github.com/twhidden/grok):
- Report issues or suggest features.
- Submit pull requests with tests.

## License

This project is licensed under the MIT License, free for everyone to use, modify, and distribute with no cost or warranty.