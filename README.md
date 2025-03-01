# GrokSdk 🚀

[![NuGet](https://img.shields.io/nuget/v/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![Build Status](https://github.com/twhidden/grok/actions/workflows/ci.yml/badge.svg)](https://github.com/twhidden/grok/actions/workflows/ci.yml)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue.svg)](https://dotnet.microsoft.com/en-us/platform/dotnet-standard)

An unofficial .NET library for interacting with Grok's API, with `GrokThread` as its centerpiece. This library streamlines conversation management, tool integration (e.g., image generation), and real-time streaming in .NET applications.

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

```
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

        // Register an image generation tool
        thread.RegisterTool(new GrokToolDefinition(
            ImageGeneratorHelper.GrokToolName,
            "Generate images based on user input request;",
            ImageGeneratorHelper.GrokArgsRequest,
            ImageGeneratorHelper.ProcessGrokRequestForImage));

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

## Additional Examples

### Simple Chat
```
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
```
var client = new GrokClient(new HttpClient(), "your-api-key-here");
var thread = client.GetGrokThread();
```

### GrokStreamingClient
For low-level streaming:
```
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

MIT License. See [LICENSE](LICENSE).