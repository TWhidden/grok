# GrokSdk 🚀

[![NuGet](https://img.shields.io/nuget/v/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![Build Status](https://github.com/twhidden/grok/actions/workflows/ci.yml/badge.svg)](https://github.com/twhidden/grok/actions/workflows/ci.yml)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue.svg)](https://dotnet.microsoft.com/en-us/platform/dotnet-standard)

An unofficial .NET library for interacting with xAI's Grok API, featuring comprehensive support for **Grok 2**, **Grok 3**, **Grok 4**, and **Grok Vision** models, plus all Grok API functions including **Live Search**, **Image Understanding**, **Image Generation**, and **Reasoning**. The library provides a powerful conversation management system with built-in tool integration, real-time streaming, and an extensible architecture for .NET applications.

If you find this tool helpful or use it in your projects, please drop me a note on [X](https://x.com/twhidden) to let me know—it encourages me to keep it going!

## 🎯 Key Features

- **Complete Model Support**: Full compatibility with Grok 2, 3, 4, and Vision models
- **All Grok Functions**: Live Search, Image Understanding, Image Generation, and Reasoning
- **Built-in Tools**: Pre-implemented tools for all Grok API capabilities
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
thread.RegisterTool(new GrokToolLiveSearch(client));
thread.RegisterTool(new GrokToolImageUnderstanding(client));

// Set system instructions
thread.AddSystemInstruction("You are a helpful AI assistant with access to various tools.");

// Start a conversation
await foreach (var message in thread.AskQuestion("Hello! Can you generate an image of a sunset?"))
{
    if (message is GrokTextMessage text)
        Console.WriteLine(text.Message);
    else if (message is GrokToolResponse toolResponse)
        Console.WriteLine($"Tool '{toolResponse.ToolName}' executed: {toolResponse.ToolResponse}");
}
```

## 🤖 Supported Models

GrokSdk supports all current xAI Grok models:

| Model | Description | Best For |
|-------|-------------|----------|
| `grok-4-1-fast-reasoning` | Grok 4.1 fast with reasoning capabilities | High-speed reasoning tasks |
| `grok-4-1-fast-non-reasoning` | Grok 4.1 fast without reasoning | Fast general conversations |
| `grok-code-fast-1` | Fast code-focused model | Code generation and understanding |
| `grok-4-fast-reasoning` | Grok 4 fast with reasoning | Balanced reasoning performance |
| `grok-4-fast-non-reasoning` | Grok 4 fast without reasoning | Fast responses for general use |
| `grok-4-0709` | Grok 4 version 0709 | Advanced reasoning and understanding |
| `grok-3-mini` | Mini version of Grok 3 | Lightweight tasks |
| `grok-3` | Standard Grok 3 model | General conversations and tasks |
| `grok-2-vision-1212` | Grok 2 with vision capabilities | Image analysis and understanding |

**Note**: "Latest" tagged models (e.g., `grok-3-latest`, `grok-4-latest`) are also supported and tested for compatibility.

### Image Generation
- `grok-2-image-1212`: Image generation model

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

### 🔍 Live Search Function

Utilize Grok's real-time search across web, news, X (Twitter), and RSS feeds.

```csharp
var thread = new GrokThread(client);
thread.RegisterTool(new GrokToolLiveSearch(client));

await foreach (var message in thread.AskQuestion("What's the latest news about AI developments?"))
{
    if (message is GrokToolResponse response && response.ToolName == GrokToolLiveSearch.ToolName)
    {
        Console.WriteLine($"Search results: {response.ToolResponse}");
    }
}
```

**Grok API Parameters:**
- `query`: Search query string
- `search_type`: Type of search - "web", "news", "x", or "rss"
- `max_results`: Maximum number of results (optional)
- `from_date`, `to_date`: Date range filters (optional)
- `country`: Country code for localized results (optional)

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

## 💬 Complete Console Example

Here's a full-featured console application demonstrating all capabilities:
```csharp
using GrokSdk;
using GrokSdk.Tools;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Initialize the HTTP client and GrokClient
        string apiKey = GetApiKey(); // Implement your API key retrieval
        var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, apiKey);

        // Create a GrokThread instance to manage the conversation
        var thread = new GrokThread(client);
        string currentModel = "grok-3-latest";

        // Register all built-in tools
        thread.RegisterTool(new GrokToolImageGeneration(client));
        thread.RegisterTool(new GrokToolReasoning(client));
        thread.RegisterTool(new GrokToolLiveSearch(client, currentModel));
        thread.RegisterTool(new GrokToolImageUnderstanding(client));

        // Set system instructions
        thread.AddSystemInstruction("You are a helpful AI assistant with access to image generation, reasoning, search, and image understanding tools.");

        Console.WriteLine("🚀 Grok Chat Console");
        Console.WriteLine("Available models: grok-3-latest, grok-4-latest");
        Console.WriteLine("Type 'quit' to exit, 'm' to switch models");
        Console.WriteLine();

        // Main interaction loop
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"You ({currentModel}): ");
            Console.ResetColor();
            
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "quit") break;

            // Model switching
            if (input.Trim().ToLower() == "m")
            {
                currentModel = currentModel switch
                {
                    "grok-3-latest" => "grok-4-latest",
                    "grok-4-latest" => "grok-3-latest",
                    _ => "grok-3-latest"
                };
                Console.WriteLine($"Switched to model: {currentModel}");
                continue;
            }

            try
            {
                // Stream the response
                await foreach (var message in thread.AskQuestion(input, model: currentModel))
                {
                    switch (message)
                    {
                        case GrokStreamState state:
                            Console.ForegroundColor = state.StreamState switch
                            {
                                StreamState.Thinking => ConsoleColor.Yellow,
                                StreamState.Streaming => ConsoleColor.Cyan,
                                StreamState.CallingTool => ConsoleColor.Magenta,
                                StreamState.Done => ConsoleColor.Green,
                                StreamState.Error => ConsoleColor.Red,
                                _ => ConsoleColor.White
                            };
                            Console.WriteLine($"[{state.StreamState}]");
                            Console.ResetColor();
                            break;

                        case GrokTextMessage textMessage:
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.Write(textMessage.Message);
                            Console.ResetColor();
                            break;

                        case GrokToolResponse toolResponse:
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"\n🛠️ {toolResponse.ToolName}: {toolResponse.ToolResponse}");
                            Console.ResetColor();
                            break;

                        case GrokError error:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ Error: {error.Exception.Message}");
                            Console.ResetColor();
                            break;
                    }
                }
                Console.WriteLine(); // New line after response
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("👋 Goodbye!");
    }

    private static string GetApiKey()
    {
        // Try environment variable first
        var apiKey = Environment.GetEnvironmentVariable("GROK_API_KEY");
        if (!string.IsNullOrEmpty(apiKey)) return apiKey;

        // Try reading from file
        try
        {
            if (File.Exists("apikey.txt"))
                return File.ReadAllText("apikey.txt").Trim();
        }
        catch { }

        throw new InvalidOperationException("API key not found. Set GROK_API_KEY environment variable or create apikey.txt file.");
    }
}
```

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
var visionThread = new GrokThread(client);
await foreach (var message in visionThread.AskQuestion(
    "Describe this image in detail", 
    model: "grok-2-vision-1212"))
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

Built with **NSwag v14.2.0.0** from the official [Grok API specification](https://x.ai/api-docs).

### Key Dependencies
- **Newtonsoft.Json**: JSON serialization (planned migration to System.Text.Json)
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