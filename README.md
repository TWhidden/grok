# GrokSdk 🚀

[![NuGet](https://img.shields.io/nuget/v/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![Build Status](https://github.com/twhidden/grok/actions/workflows/ci.yml/badge.svg)](https://github.com/twhidden/grok/actions/workflows/ci.yml)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue.svg)](https://dotnet.microsoft.com/en-us/platform/dotnet-standard)

An unofficial .NET library for interacting with xAI's Grok API, featuring comprehensive support for **Grok 2**, **Grok 3**, **Grok 4**, and **Grok Vision** models, plus all Grok API functions including **Web Search**, **X Search**, **Image Understanding**, **Image Generation**, **Video Generation**, **Code Execution**, **Reasoning**, **Deferred Completions**, and **Structured Output**. The library includes full support for the **xAI Responses API** (`/v1/responses`). The library provides a powerful conversation management system with built-in tool integration, real-time streaming, and an extensible architecture for .NET applications.

If you find this tool helpful or use it in your projects, please drop me a note on [X](https://x.com/twhidden) to let me know—it encourages me to keep it going!

## 🎯 Key Features

- **Complete Model Support**: Full compatibility with Grok 2, 3, 4, and Vision models
- **All Grok Functions**: Web Search, X Search, Image Understanding, Image Generation, Video Generation, Code Execution, and Reasoning
- **Responses API**: Full support for xAI's Responses API with built-in agentic tools
- **Built-in Tools**: Pre-implemented tools for all Grok API capabilities
- **Video Generation**: Generate videos from text prompts, images, or existing videos
- **Code Execution**: Run code in xAI's sandboxed environment via the Responses API
- **Deferred Completions**: Submit requests and retrieve results asynchronously
- **Structured Output**: Constrain responses to JSON schemas with type-safe deserialization
- **Developer Role**: Enhanced instruction-following with developer role messages
- **Parallel Tool Execution**: Multiple tool calls execute concurrently for better performance
- **Streaming Conversations**: Real-time response streaming with state management
- **Tool Integration**: Extensible architecture for custom tool development
- **Thread Management**: Persistent conversation context with history compression
- **Cross-Platform**: Supports .NET 8.0 and .NET Standard 2.0

## 📦 Installation

Install via NuGet Package Manager:

```bash
dotnet add package GrokSdk
```

### Prerequisites

- **.NET 8.0** or **.NET Standard 2.0** compatible framework
- **Newtonsoft.Json** (v13.0.0 or compatible) for JSON serialization
- **HttpClient** from `System.Net.Http`
- **xAI API Key** - Get yours at [x.ai](https://x.ai/)

## 🚀 Quick Start

Here's a simple example to get you started with GrokSdk:

```csharp
using GrokSdk;
using GrokSdk.Tools;

// Initialize the client
var httpClient = new HttpClient();
var client = new GrokClient(httpClient, "your-api-key-here");

// Create a conversation thread
var thread = new GrokThread(client);

// Register built-in tools
thread.RegisterTool(new GrokToolImageGeneration(client));
thread.RegisterTool(new GrokToolReasoning(client));
thread.RegisterTool(new GrokToolWebSearch(client));
thread.RegisterTool(new GrokToolImageUnderstanding(client));
thread.RegisterTool(new GrokToolVideoGeneration(client));
thread.RegisterTool(new GrokToolCodeExecution(client));
thread.RegisterTool(new GrokToolMcp(client, "https://mcp.deepwiki.com/mcp", "deepwiki"));

// Start a conversation
await foreach (var message in thread.AskQuestion("Hello! Can you generate an image of a sunset?"))
{
    if (message is GrokTextMessage text)
        Console.Write(text.Message);
    else if (message is GrokToolResponse toolResponse)
        Console.WriteLine($"Tool '{toolResponse.ToolName}': {toolResponse.ToolResponse}");
    else if (message is GrokCitationMessage citations)
        foreach (var c in citations.Citations)
            Console.WriteLine($"  Source: [{c.Title}] {c.Url}");
}
```

## 🤖 Supported Models

GrokSdk supports all current xAI Grok models:

| Model | Description | Best For |
|-------|-------------|----------|
| `grok-4-1-fast-reasoning` | Grok 4.1 fast with reasoning & multimodal | High-speed reasoning tasks, image understanding |
| `grok-4-1-fast-non-reasoning` | Grok 4.1 fast without reasoning | Fast general conversations |
| `grok-code-fast-1` | Fast code-focused model | Code generation and understanding |
| `grok-4-fast-reasoning` | Grok 4 fast with reasoning | Balanced reasoning performance |
| `grok-4-fast-non-reasoning` | Grok 4 fast without reasoning | Fast responses for general use |
| `grok-4-0709` | Grok 4 version 0709 | Advanced reasoning and understanding |
| `grok-3` | Standard Grok 3 model | General conversations and tasks |

**Note**: "Latest" tagged models (e.g., `grok-3-latest`, `grok-4-latest`) are also supported and tested for compatibility.

### Image Generation
- `grok-imagine-image`: Image generation model (superior quality)

> ⚠️ **Deprecation Notice** (February 28, 2026): `grok-2-image-1212`, `grok-3-mini`, and `grok-2-vision-1212` are deprecated. Use `grok-imagine-image` for image generation and `grok-4-1-fast-reasoning` for reasoning and image understanding.

## 🛠️ Complete Grok API Function Support

GrokSdk provides built-in tools that implement all Grok API functions, giving you access to the full capabilities of the Grok platform:

### 🎨 Image Generation Function

Access Grok's native image generation capabilities to create images from text descriptions.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolImageGeneration(client));

await foreach (var message in thread.AskQuestion("Create an image of a futuristic city at night"))
{
    if (message is GrokToolResponse response && response.ToolName == GrokToolImageGeneration.ToolName)
    {
        // Response contains image URL or base64 data
        Console.WriteLine($"Generated image: {response.ToolResponse}");
    }
}
```

**Grok API Parameters:**
- `prompt`: Text description of the image to generate
- `n`: Number of images to generate (default: 1)
- `response_format`: Output format - "url" or "b64_json" (default: "url")

### 🧠 Reasoning Function

Leverage Grok's advanced reasoning capabilities for complex problem analysis.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolReasoning(client));

await foreach (var message in thread.AskQuestion("Analyze the pros and cons of renewable energy"))
{
    if (message is GrokToolResponse response && response.ToolName == GrokToolReasoning.ToolName)
    {
        Console.WriteLine($"Reasoning result: {response.ToolResponse}");
    }
}
```

**Grok API Parameters:**
- `problem`: The problem or question to reason about
- `effort`: Reasoning effort level - "low" or "high" (default: "low")

### 🔍 Web Search Function (Responses API)

Search the web and X/Twitter in real-time using xAI's Responses API (`/v1/responses`). This replaces the deprecated Live Search function.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolWebSearch(client));

await foreach (var message in thread.AskQuestion("What's the latest news about AI developments?"))
{
    if (message is GrokToolResponse response && response.ToolName == GrokToolWebSearch.ToolName)
    {
        Console.WriteLine($"Search results: {response.ToolResponse}");
    }
}
```

**Parameters:**
- `query`: Search query string (required)
- `search_type`: Type of search - `"web"`, `"x"`, or `"both"` (default: `"web"`)
- `allowed_domains`: For web search - only search within these domains (max 5)
- `excluded_domains`: For web search - exclude these domains (max 5)
- `allowed_x_handles`: For X search - only search posts from these handles (max 10)
- `excluded_x_handles`: For X search - exclude posts from these handles (max 10)
- `from_date`, `to_date`: For X search - date range in ISO8601 format (YYYY-MM-DD)

### 👁️ Image Understanding Function

Harness Grok's vision capabilities for detailed image analysis and Q&A.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolImageUnderstanding(client));

await foreach (var message in thread.AskQuestion("What's in this image? https://example.com/photo.jpg"))
{
    if (message is GrokToolResponse response && response.ToolName == GrokToolImageUnderstanding.ToolName)
    {
        Console.WriteLine($"Image analysis: {response.ToolResponse}");
    }
}
```

**Grok API Parameters:**
- `prompt`: Question or instruction about the image
- `image_url`: Image URL or base64-encoded image data
- `image_detail`: Analysis detail level - "low" or "high" (default: "low")

### 🎬 Video Generation Function

Generate videos from text prompts, animate images, or edit existing videos using the `grok-imagine-video` model.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolVideoGeneration(client));

await foreach (var message in thread.AskQuestion("Create a video of a cat playing with a ball"))
{
    if (message is GrokToolResponse response && response.ToolName == GrokToolVideoGeneration.ToolName)
    {
        Console.WriteLine($"Video URL: {response.ToolResponse}");
    }
}
```

**Direct programmatic usage:**
```csharp
var tool = new GrokToolVideoGeneration(client);
var result = await tool.GenerateAsync(new GrokToolVideoGenerationArgs
{
    Prompt = "A peaceful ocean wave",
    Duration = 5
});
Console.WriteLine($"Video URL: {result.Url}");
```

**Parameters:**
- `prompt`: Text description of the video to generate (required)
- `image_url`: URL or base64 image for image-to-video generation
- `video_url`: URL of source video for video editing (max 8.7s)
- `duration`: Duration in seconds (1-15, default: 5)
- `aspect_ratio`: `"1:1"`, `"16:9"`, `"9:16"`, `"4:3"`, `"3:4"`, `"3:2"`, `"2:3"` (default: `"16:9"`)
- `resolution`: `"480p"` or `"720p"` (default: `"480p"`)

### 💻 Code Execution Function (Code Interpreter)

Run code in xAI's sandboxed environment via the Responses API for calculations and data analysis.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolCodeExecution(client));

await foreach (var message in thread.AskQuestion("Calculate the factorial of 20"))
{
    if (message is GrokToolResponse response && response.ToolName == GrokToolCodeExecution.ToolName)
    {
        Console.WriteLine($"Result: {response.ToolResponse}");
    }
}
```

**Direct programmatic usage:**
```csharp
var tool = new GrokToolCodeExecution(client);
var response = await tool.QueryAsync("What is 2 to the power of 100?");
Console.WriteLine(response.Text);
```

### 🔗 MCP Tool (Model Context Protocol)

Connect Grok to external MCP servers, extending its capabilities with third-party tools. xAI manages the MCP connection via the Responses API.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolMcp(client, "https://mcp.deepwiki.com/mcp", "deepwiki"));

await foreach (var message in thread.AskQuestion("What is the microsoft/TypeScript project about?"))
{
    if (message is GrokToolResponse response && response.ToolName == GrokToolMcp.ToolName)
        Console.WriteLine($"MCP Result: {response.ToolResponse}");
}
```

**Multi-server configuration:**
```csharp
var servers = new List<GrokMcpServerConfig>
{
    new GrokMcpServerConfig
    {
        ServerUrl = "https://mcp.deepwiki.com/mcp",
        ServerLabel = "deepwiki"
    },
    new GrokMcpServerConfig
    {
        ServerUrl = "https://your-private-server.com/mcp",
        ServerLabel = "internal",
        Authorization = "Bearer your-token-here"
    }
};
var tool = new GrokToolMcp(client, servers);
```

### 📂 Collections Search Tool (File Search)

Search uploaded documents in xAI vector stores using the Responses API's `file_search` tool.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolCollectionsSearch(client, "vs_abc123"));

await foreach (var message in thread.AskQuestion("What does the Q3 report say about revenue?"))
{
    if (message is GrokToolResponse response)
        Console.WriteLine($"Result: {response.ToolResponse}");
}
```

### ⏳ Deferred Completions

Submit chat completion requests and retrieve results later — ideal for long-running or non-urgent tasks. Results remain available for 24 hours.

```csharp
var deferred = new GrokDeferredCompletion(client);

// Simple: ask and wait
string? answer = await deferred.AskDeferredAsync("What is the meaning of life?");
Console.WriteLine(answer);

// Advanced: submit and poll manually
var requestId = await deferred.SubmitAsync(request);
var result = await deferred.TryGetResultAsync(requestId);
```

### 📐 Structured Output

Constrain Grok's response to match a JSON schema for type-safe deserialization.

```csharp
public record WeatherInfo(string City, double Temperature, string Condition);

var weather = await GrokStructuredOutput.AskAsJsonAsync<WeatherInfo>(
    client,
    "What's the weather in Tokyo?",
    model: "grok-3-mini-fast"
);
Console.WriteLine($"{weather.City}: {weather.Temperature}°, {weather.Condition}");
```

### 🔧 Developer Role

Use the developer role for stronger instruction-following behavior on supported models:

```csharp
var thread = new GrokThread(client);
thread.AddDeveloperInstruction("Always respond in JSON format.");

await foreach (var message in thread.AskQuestion("List 3 colors"))
{
    // Response will follow developer instructions more strictly
}
```

## 💬 Complete Console Example

The `GrokSdk.ConsoleDemo` project is a ready-to-run console app that registers all built-in tools and handles every message type. You can run it directly:

```bash
cd src/GrokSdk.ConsoleDemo
dotnet run
```

Here is the full source (also in `GrokSdk.ConsoleDemo/Program.cs`):

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

Try these sample prompts:
- "Generate an image of a sunset over mountains" (Image Generation)
- "Search the web for the latest AI news" (Web Search)
- "Calculate the first 50 Fibonacci numbers" (Code Execution)
- "What is the microsoft/TypeScript project about?" (MCP / DeepWiki)
- "Explain the P vs NP problem" (Reasoning)

## 🔧 Advanced Usage

### Custom Tool Development

Create your own tools by implementing the `IGrokTool` interface or using `GrokToolDefinition`:

```csharp
// Simple function-based tool
thread.RegisterTool(new GrokToolDefinition(
    "weather_tool",
    "Get current weather information for a location",
    new { 
        type = "object",
        properties = new {
            location = new { type = "string", description = "City name or coordinates" }
        },
        required = new[] { "location" }
    },
    async args => {
        var location = JsonConvert.DeserializeObject<dynamic>(args).location;
        // Your weather API call here
        return $"Current weather in {location}: Sunny, 72°F";
    }
));

// Advanced tool class
public class CustomAnalyticsTool : IGrokTool
{
    public string Name => "analytics_tool";
    public string Description => "Analyze data and provide insights";
    public object Parameters => new {
        type = "object",
        properties = new {
            data = new { type = "string", description = "JSON data to analyze" }
        }
    };

    public async Task<string> ExecuteAsync(string arguments)
    {
        // Your custom tool implementation
        var args = JsonConvert.DeserializeObject<dynamic>(arguments);
        // Process data and return results
        return "Analysis complete: [results]";
    }
}
```

### Conversation Management

```csharp
// Add context before starting conversation
thread.AddUserMessage("Previous context message");
thread.AddAssistantMessage("Previous assistant response");

// Compress history to save tokens
await thread.CompressHistoryAsync();

// Clear conversation history
thread.ClearMessages();
```

### Model-Specific Usage

```csharp
// Use different models for different tasks
// Grok 4.1 Fast supports multimodal (text + image) inputs
var visionThread = new GrokThread(client);
await foreach (var message in visionThread.AskQuestion(
    "Describe this image in detail", 
    model: "grok-4-1-fast-reasoning"))
{
    // Handle vision model response
}

// High-performance reasoning
await foreach (var message in thread.AskQuestion(
    "Solve this complex math problem", 
    model: "grok-4-latest"))
{
    // Handle advanced model response
}
```
## 📚 Core Classes

### GrokClient
The main client for API communication:

```csharp
var client = new GrokClient(new HttpClient(), "your-api-key");
```

### GrokThread  
Manages conversations and tool execution:

```csharp
var thread = new GrokThread(client);
thread.AddSystemInstruction("You are a helpful assistant");
```

### GrokStreamingClient
For low-level streaming control:

```csharp
var streamingClient = client.GetStreamingClient();
streamingClient.OnChunkReceived += (s, chunk) => 
    Console.Write(chunk.Choices[0].Delta.Content);
```

## 🔄 Message Types

GrokSdk provides typed message handling through inheritance of `GrokMessageBase`:

- **`GrokTextMessage`**: Regular text responses from Grok
- **`GrokServiceMessage`**: Service-level messages and notifications  
- **`GrokStreamState`**: Stream state changes (Thinking, Streaming, Done, etc.)
- **`GrokToolResponse`**: Results from tool execution
- **`GrokCitationMessage`**: Source citations from web search results
- **`GrokError`**: Error handling with exception details

## ⚙️ Configuration

### Environment Variables
```bash
# Set your API key
export GROK_API_KEY="your-api-key-here"
```

### API Key File
Create an `apikey.txt` file in your application directory:
```
your-api-key-here
```

## 🧪 Testing

The SDK includes comprehensive tests covering:
- Live API integration tests
- Model compatibility testing
- Tool execution validation
- Error handling scenarios

Run tests with:
```bash
cd src
dotnet test
```

Note: Tests require a valid API key set via `GROK_API_KEY` environment variable.

## 📖 API Reference

Built with **NSwag v14.6.3.0** from the official [Grok API specification](https://docs.x.ai/api).

### Key Dependencies
- **Newtonsoft.Json**: JSON serialization
- **System.Net.Http**: HTTP client functionality
- **System.Threading.Channels**: Async streaming support (.NET Standard 2.0)

### Error Handling
- Custom `GrokSdkException` with detailed error information
- Automatic retry logic for rate limiting
- Graceful degradation for network issues

## 🤝 Contributing

We welcome contributions! Please visit our [GitHub repository](https://github.com/twhidden/grok):

1. **Report Issues**: Found a bug? Let us know!
2. **Feature Requests**: Suggest new features or improvements
3. **Pull Requests**: Submit changes with tests and documentation
4. **Documentation**: Help improve our examples and guides

### Development Setup
```bash
git clone https://github.com/twhidden/grok.git
cd grok/src
dotnet restore
dotnet build
```

## 📄 License

This project is licensed under the **MIT License** - free to use, modify, and distribute with no cost or warranty.

---

**Disclaimer**: This is an unofficial library for xAI's Grok API. For official documentation and support, visit [x.ai](https://x.ai/).