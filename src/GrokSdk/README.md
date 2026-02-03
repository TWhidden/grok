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

`GrokThread` powers this library, enabling multi-turn conversations, tool execution, and asynchronous streaming. Below is a console chat example featuring an image generation tool:

```csharp
using GrokSdk;

namespace GrokConsoleTest;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Initialize HTTP client and GrokClient
        var httpClient = new HttpClient();
        var sdk = new GrokClient(httpClient, "your-api-key-here"); // Replace with your API key

        // Create GrokThread to manage the conversation
        var thread = new GrokThread(sdk);

        // Define Grok's behavior with system instructions
        thread.AddSystemInstruction("This is a console chat bot for testing communications with Grok");

        // Show welcome message
        Console.WriteLine("Welcome to the Grok Chat Console. Type your questions below. Type 'quit' to exit.");

        // Register your own tool (Generic)
        thread.RegisterTool(new GrokToolDefinition(
            "Tool Name",
            "Tool Instruction",
            "Tool Arguments",
            <ToolExecutionFunctionCallback()>));
        
        // Built in Grok Image Generation Tool
        thread.RegisterTool(new GrokToolImageGeneration(sdk))

        // Built in Grok Reasoning Tool
        thread.RegisterTool(new GrokToolReasoning(sdk))

        // Preload context with neutral discussion (non-API processed)
        thread.AddUserMessage("Alex Said: I tried a new coffee shop today.");
        thread.AddUserMessage("Sam Said: Nice! How was the coffee?");
        thread.AddUserMessage("Alex Said: Really good, I might go back tomorrow.");
        await thread.CompressHistoryAsync(); // Compress history to save tokens

        // Main interaction loop
        while (true)
        {
            // Prompt user with green text
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("You: ");
            Console.ResetColor();
            var input = Console.ReadLine();

            // Exit on 'quit' or empty input
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "quit") break;

            try
            {
                // Stream responses for the user's question
                var messages = thread.AskQuestion(input);
                await foreach (var message in messages)
                {
                    if (message is GrokStreamState state)
                    {
                        // Display state updates with colors
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
                                Console.WriteLine("Done processing...");
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
                    }
                    else if (message is GrokTextMessage textMessage)
                    {
                        // Stream text responses in blue
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(textMessage.Message);
                    }
                    else if (message is GrokToolResponse toolResponse)
                    {
                        // Handle tool output (e.g., image URL)
                        if (toolResponse.ToolName == ImageGeneratorHelper.GrokToolName)
                        {
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"Image URL: {toolResponse.ToolResponse}");
                        }
                    }
                }
                Console.ResetColor();
                Console.WriteLine(); // New line after response
            }
            catch (Exception ex)
            {
                // Display errors in red
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("Goodbye!");
    }
}

// Image generation tool helper
internal static class ImageGeneratorHelper
{
    private static readonly HttpClient httpClient = new();

    public static string GrokToolName = "image_generation";

    public static object GrokArgsRequest = new
    {
        type = "object",
        properties = new
        {
            imageDescription = new
            {
                type = "string",
                description = "The image description that will be used to generate an image"
            }
        },
        required = new[] { "imageDescription" }
    };

    /// <summary>
    /// Processes Grok's tool request and generates an image URL.
    /// </summary>
    public static async Task<string> ProcessGrokRequestForImage(string serializedRequest)
    {
        var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(serializedRequest) ??
                   throw new Exception("Could not process arguments from function");
        var description = args["imageDescription"];
        return await GenerateImageUrlAsync(description);
    }

    /// <summary>
    /// Generates an image URL by sending a description to an external service.
    /// </summary>
    private static async Task<string> GenerateImageUrlAsync(string generationText)
    {
        if (generationText == null) throw new ArgumentNullException(nameof(generationText));

        var url = "your-webhook-url-here"; // Replace with your webhook URL
        var json = JsonSerializer.Serialize(new { ImageDescription = generationText });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, content);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"POST request failed with status code {response.StatusCode}");

        var responseString = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseString);
        if (!jsonResponse.TryGetValue("image_url", out var imageUrl))
            throw new Exception("Image URL not found in response");

        return imageUrl;
    }
}
```

### Key Features of GrokThread
- **Conversation Management**: Tracks history for context-aware responses.
- **Tool Integration**: Executes custom tools like image generation (see above).
- **Streaming**: Provides real-time updates via `IAsyncEnumerable`.
- **Compression**: Shrinks history with `CompressHistoryAsync` to optimize tokens.
- **State Tracking**: Monitors states (`Thinking`, `Streaming`, etc.) for feedback.

## New Built-in Tools

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

### Live Search Tool
The `GrokToolLiveSearch` tool enables Grok to perform real-time searches on various sources, including web, news, X, and RSS feeds. It returns a summary and citations based on the search query and parameters.

To use this tool, register it with your `GrokThread` instance:

```csharp  
var thread = new GrokThread(client);  
thread.RegisterTool(new GrokToolLiveSearch(client));  
thread.AddSystemInstruction("You can search the web, news, X, and RSS feeds in real-time.");  
```

**Usage Example:**  
```csharp  
await foreach (var message in thread.AskQuestion("What's the latest news about AI?"))  
{  
    if (message is GrokToolResponse toolResponse && toolResponse.ToolName == GrokToolLiveSearch.ToolName)  
    {  
        Console.WriteLine($"Search Summary: {toolResponse.ToolResponse}");  
    }  
}  
```

**Parameters:**  
- `query`: The search query (required).  
- `search_type`: The type of search ("web", "news", "x", "rss") (required).  
- Optional parameters: `from_date`, `to_date`, `max_results`, `country`, etc. Refer to the tool's documentation for a full list.

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
    if (response is GrokTextMessage text) Console.WriteLine(text.Message);
}
```

### Tool Example: Image Generation
The `ImageGeneratorHelper` above shows how to integrate a tool that generates images:
- Ask: "Generate an image of a sunset."
- Grok calls `image_generation`, which returns a URL from your webhook.

## Supporting Classes

### GrokClient
Sets up the API connection:
```csharp
var client = new GrokClient(new HttpClient(), "your-api-key-here");
var thread = client.GetGrokThread();
```

### GrokStreamingClient
For low-level streaming:
```csharp
var streamingClient = client.GetStreamingClient();
streamingClient.OnChunkReceived += (s, chunk) => Console.Write(chunk.Choices[0].Delta.Content);
await streamingClient.StartStreamAsync(new ChatCompletionRequest { /* ... */ });
```

## Notes

- **Swagger**: Built with NSwag v14.2.0.0. See [Grok API docs](https://x.ai/api-docs).
- **Serialization**: Uses `Newtonsoft.Json` (planned switch to `System.Text.Json`).
- **Errors**: Throws `GrokSdkException` with details.

## Contributing

Visit [GitHub](https://github.com/twhidden/grok):
- Report issues or suggest features.
- Submit pull requests with tests.

## License

This project is licensed under the MIT License, free for everyone to use, modify, and distribute with no cost or warranty.