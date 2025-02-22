# GrokSdk 🚀

[![NuGet](https://img.shields.io/nuget/v/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![Build Status](https://github.com/twhidden/grok/actions/workflows/ci.yml/badge.svg)](https://github.com/twhidden/grok/actions/workflows/ci.yml)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue.svg)](https://dotnet.microsoft.com/en-us/platform/dotnet-standard)

An unofficial .NET library for interacting with Grok's API. This library enables developers to create chat completions with Grok’s API seamlessly in .NET applications, now with enhanced type safety using specific message classes and support for streaming responses.

## Installation

Install the library via NuGet by running the following command in your project directory:

```bash
dotnet add package GrokSdk
```

Ensure you have the following dependencies in your project:
- `Newtonsoft.Json` (v13.0.0.0 or compatible) for JSON serialization.
- .NET-compatible `HttpClient` (e.g., from `System.Net.Http`).

## Usage

The library provides a `GrokClient` class to interact with Grok’s API. Below are examples demonstrating authentication, chat completions, conversation context, command-based interactions, streaming responses, and tool choice usage.

### Authentication

Initialize `GrokClient` with an `HttpClient` instance and your Grok API key:

```csharp
using GrokSdk;

var httpClient = new HttpClient();
var client = new GrokClient(httpClient, "your-api-key-here");
```

>**Note:** The API key is required and must be provided during instantiation. A `null` API key will throw an `ArgumentNullException`.

The default base URL is set to `https://api.x.ai/v1`, but you can modify it using the `BaseUrl` property if needed:

```csharp
client.BaseUrl = "https://custom-api-endpoint.com/v1/";
```

### Creating a Chat Completion

Here’s how to create a chat completion using specific message types:

```csharp
using System.Collections.ObjectModel;

var request = new ChatCompletionRequest
{
    Messages = new Collection<Message>
    {
        new SystemMessage { Content = "You are a helpful assistant." },
        new UserMessage { Content = "Say hello world." }
    },
    Model = "grok-2-latest",
    Stream = false,
    Temperature = 0f
};

try
{
    var response = await client.CreateChatCompletionAsync(request);
    Console.WriteLine(response.Choices[0].Message.Content); // Outputs the assistant's response
}
catch (GrokSdkException ex)
{
    Console.WriteLine($"Error {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
}
```

#### Key Classes and Properties
- **`ChatCompletionRequest`**:
  - `Messages`: A collection of `Message` objects (required). Use specific types like `SystemMessage`, `UserMessage`, `AssistantMessage`, or `ToolMessage`.
  - `Model`: The Grok model (e.g., `"grok-2-latest"`) (required).
  - `Stream`: Boolean to enable streaming (default: `false`).
  - `Temperature`: Controls randomness (0 to 2, default: 0).
  - `Tools`: Optional collection of `Tool` objects for function calling.
  - `Tool_choice`: Controls tool usage (`"auto"`, `"required"`, `"none"`, or a specific function).

- **`Message`**:
  - Specific types: `SystemMessage`, `UserMessage`, `AssistantMessage`, `ToolMessage`, each with a `Content` property (and additional properties like `Tool_calls` for `AssistantMessage` or `Tool_call_id` for `ToolMessage`).

- **`ChatCompletionResponse`**:
  - `Id`: Unique completion ID.
  - `Choices`: List of response options (each with a `Message` and `Finish_reason`).
  - `Usage`: Token usage details (e.g., `Prompt_tokens`, `Completion_tokens`).

### Maintaining Conversation Context

The library supports multi-turn conversations by passing the full message history using specific message types:

```csharp
var messages = new Collection<Message>
{
    new SystemMessage { Content = "You are Grok, a helpful assistant." },
    new UserMessage { Content = "My name is TestUser. Remember that." }
};

var request1 = new ChatCompletionRequest
{
    Messages = messages,
    Model = "grok-2-latest",
    Stream = false,
    Temperature = 0f
};

var response1 = await client.CreateChatCompletionAsync(request1);
messages.Add(new AssistantMessage { Content = response1.Choices[0].Message.Content });

messages.Add(new UserMessage { Content = "What is my name?" });
var request2 = new ChatCompletionRequest
{
    Messages = messages,
    Model = "grok-2-latest",
    Stream = false,
    Temperature = 0f
};

var response2 = await client.CreateChatCompletionAsync(request2);
Console.WriteLine(response2.Choices[0].Message.Content); // Should recall "TestUser"
```

### Command-Based Interaction

You can instruct Grok to respond to commands, such as generating a roast:

```csharp
var request = new ChatCompletionRequest
{
    Messages = new Collection<Message>
    {
        new SystemMessage { Content = "You are a bot that responds to commands. When given '/roast \"name\"', generate a funny roast." },
        new UserMessage { Content = "/roast \"Dave\"" }
    },
    Model = "grok-2-latest",
    Stream = false,
    Temperature = 0f
};

var response = await client.CreateChatCompletionAsync(request);
Console.WriteLine(response.Choices[0].Message.Content); // Outputs a roast for "Dave"
```

### Streaming Client Example

The library supports streaming responses for real-time interactions using `GrokStreamingClient`:

```csharp
var streamingClient = client.GetStreamingClient();

streamingClient.OnStreamStarted += (s, e) => Console.WriteLine("Stream started.");
streamingClient.OnChunkReceived += (s, chunk) =>
{
    if (chunk.Choices[0].Delta.Content != null)
    {
        Console.Write(chunk.Choices[0].Delta.Content);
    }
};
streamingClient.OnStreamCompleted += (s, e) => Console.WriteLine("\nStream completed.");
streamingClient.OnStreamError += (s, ex) => Console.WriteLine($"Error: {ex.Message}");
streamingClient.OnStateChanged += (s, state) => Console.WriteLine($"State changed to: {state}");

var request = new ChatCompletionRequest
{
    Messages = new Collection<Message>
    {
        new SystemMessage { Content = "You are a helpful assistant." },
        new UserMessage { Content = "Tell me a short story." }
    },
    Model = "grok-2-latest",
    Stream = true,
    Temperature = 0f
};

await streamingClient.StartStreamAsync(request);
```

In this example:
- `OnStreamStarted`: Triggered when the stream begins.
- `OnChunkReceived`: Triggered for each chunk, allowing real-time display of the assistant’s reply.
- `OnStreamCompleted`: Triggered when the stream ends successfully.
- `OnStreamError`: Triggered if an error occurs.
- `OnStateChanged`: Tracks state changes (e.g., `Thinking`, `Streaming`, `Done`, `Error`).

>**Note:** The `Stream` property in the request must be `true` for streaming. `StartStreamAsync` enforces this automatically.

### Tool Choice Example

The library supports function calling with `tool_choice` to integrate external tools. Here’s an example where the user asks how many Starlink satellites are active, and the tool queries the N2YO API to fetch and count Starlink satellites above a location:

```csharp
using Newtonsoft.Json;

var httpClient = new HttpClient();
var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

var messages = new Collection<Message>
{
    new SystemMessage { Content = "You are an assistant with access to satellite data tools." },
    new UserMessage { Content = "How many Starlink satellites are out there? Category Code 52" }
};

// Define a tool to count Starlink satellites
var tools = new Collection<Tool>
{
    new Tool
    {
        Type = ToolType.Function,
        Function = new FunctionDefinition
        {
            Name = "get_satellite_data",
            Description = "Get the satellite data from n2yo website using the category code",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    categoryCode = new
                    {
                        type = "number",
                        description =
                            "The Category Code for n2yo website for a specific company (such as Starlink 52)"
                    }
                },
                required = new[] { "categoryCode" }
            }
        }
    }
};

// Step 1: Initial request to Grok
var request = new ChatCompletionRequest
{
    Messages = messages,
    Model = "grok-2-latest",
    Stream = false,
    Temperature = 0f,
    Tools = tools,
    Tool_choice = Tool_choice.Auto // Let Grok decide to use the tool
};

var response = await client.CreateChatCompletionAsync(request);
var choice = response.Choices.First();

const int maxDataSetToGrok = 100;

if (choice.Message.Tool_calls?.Count > 0)
{
    var toolCall = choice.Message.Tool_calls.First();
    if (toolCall.Function.Name == "get_satellite_data")
    {
        // Step 2: Call N2YO API to get Starlink satellite count
        var args = JsonConvert.DeserializeObject<Dictionary<string, int>>(toolCall.Function.Arguments);
        var categoryCode = args["categoryCode"];

        // Checked into the Test Classes if you want to snag it
        var data = await SatelliteHelper.GetSatellitesAsync(categoryCode);

        // Due to limits on the input of Grok (2 atm, we will limit the input to 100)
        var reducedData = data.Take(maxDataSetToGrok);

        var result = JsonConvert.SerializeObject(reducedData);

        // Add assistant message and tool result
        messages.Add(choice.Message);
        messages.Add(new ToolMessage
        {
            Content = result,
            Tool_call_id = toolCall.Id
        });
        
        // Step 3: Send back to Grok
        var followUpRequest = new ChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f,
            Tools = tools,
            Tool_choice = Tool_choice.Auto
        };

        var finalResponse = await client.CreateChatCompletionAsync(followUpRequest);
        Console.WriteLine(finalResponse.Choices.First().Message.Content); 
    }
}
```

In this example:
- The `get_satellite_data` tool queries N2YO website page, and parses the HTML (Did not see an API for getting all the data raw).
- Grok triggers the tool with `Tool_choice.Auto`, passing category ID (we could make another lookup to find the category ID, but i knew Starlink was 52).
- The tool fetches real-time data from N2YO, counts the returned satellites, and sends the result back to Grok.
- The final response explains the count to the user, e.g., "There are X Starlink satellites out there."

_There seems to be a Grok limit with the amount of json returned back - maybe it would be better to have a smaller dataset retured back to Grok with the exact answer - but still really cool with the huge payload_

## Important Notes

- **Swagger Generation:** Generated with NSwag v14.2.0.0 from a Grok-provided Swagger file. Since no official Swagger files were available, inaccuracies may exist. Refer to the [Grok API documentation](https://x.ai/api-docs) for official details.
- **JSON Serialization:** Uses `Newtonsoft.Json` due to enum case sensitivity issues with `System.Text.Json`. A future switch to `System.Text.Json` is planned.
- **Error Handling:** The library throws `GrokSdkException` for server-side errors (e.g., HTTP 400 for invalid requests). Check `StatusCode` and `Response` for details.

## Contributing

We’d love your input! To contribute:
- Report issues or suggest features on the [GitHub repository](https://github.com/twhidden/grok).
- Submit pull requests with enhancements or fixes.

Please include tests (e.g., using MSTest as shown in `GrokClientTests`) to validate changes.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.