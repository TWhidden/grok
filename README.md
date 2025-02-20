# GrokSdk 🚀

[![NuGet](https://img.shields.io/nuget/v/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GrokSdk.svg)](https://www.nuget.org/packages/GrokSdk/)
[![Build Status](https://github.com/twhidden/grok/actions/workflows/ci.yml/badge.svg)](https://github.com/twhidden/grok/actions/workflows/ci.yml)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue.svg)](https://dotnet.microsoft.com/en-us/platform/dotnet-standard)




An unofficial .NET library for interacting with Grok's API, generated using [NSwag](http://NSwag.org) v14.2.0.0 based on a Swagger file generated by Grok. This library enables developers to create chat completions with Grok’s API seamlessly in .NET applications.

**Disclaimer:** This is an unofficial library and comes with no warranty. Use at your own risk. Feedback and contributions are encouraged!

## Installation

Install the library via NuGet by running the following command in your project directory:

```bash
dotnet add package GrokSdk
```

Ensure you have the following dependencies in your project:
- `Newtonsoft.Json` (v13.0.0.0 or compatible) for JSON serialization.
- .NET-compatible `HttpClient` (e.g., from `System.Net.Http`).

## Usage

The library provides a `GrokClient` class to interact with Grok’s API. Below are examples demonstrating how to authenticate and make API calls.

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

Here’s how to create a chat completion:

```csharp
using System.Collections.ObjectModel;

var request = new ChatCompletionRequest
{
    Messages = new Collection<Message>
    {
        new Message { Role = MessageRole.System, Content = "You are a helpful assistant." },
        new Message { Role = MessageRole.User, Content = "Say hello world." }
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
catch (ApiException ex)
{
    Console.WriteLine($"Error {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
}
```

#### Key Classes and Properties
- **`ChatCompletionRequest`**:
  - `Messages`: A collection of `Message` objects (required).
  - `Model`: The Grok model (e.g., `"grok-2-latest"`) (required).
  - `Stream`: Boolean to enable streaming (default: `false`).
  - `Temperature`: Controls randomness (0 to 2, default: 0).

- **`Message`**:
  - `Role`: Enum (`System`, `User`, or `Assistant`).
  - `Content`: The message text.

- **`ChatCompletionResponse`**:
  - `Id`: Unique completion ID.
  - `Choices`: List of response options (each with a `Message` and `Finish_reason`).
  - `Usage`: Token usage details (e.g., `Prompt_tokens`, `Completion_tokens`).

### Example: Maintaining Conversation Context

The library supports multi-turn conversations by passing the full message history:

```csharp
var messages = new Collection<Message>
{
    new Message { Role = MessageRole.System, Content = "You are Grok, a helpful assistant." },
    new Message { Role = MessageRole.User, Content = "My name is TestUser. Remember that." }
};

var request1 = new ChatCompletionRequest
{
    Messages = messages,
    Model = "grok-2-latest",
    Stream = false,
    Temperature = 0f
};

var response1 = await client.CreateChatCompletionAsync(request1);
messages.Add(new Message { Role = MessageRole.Assistant, Content = response1.Choices[0].Message.Content });

messages.Add(new Message { Role = MessageRole.User, Content = "What is my name?" });
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

### Example: Command-Based Interaction

You can instruct Grok to respond to commands, such as generating a roast:

```csharp
var request = new ChatCompletionRequest
{
    Messages = new Collection<Message>
    {
        new Message { Role = MessageRole.System, Content = "You are a bot that responds to commands. When given '/roast \"name\"', generate a funny roast." },
        new Message { Role = MessageRole.User, Content = "/roast \"Dave\"" }
    },
    Model = "grok-2-latest",
    Stream = false,
    Temperature = 0f
};

var response = await client.CreateChatCompletionAsync(request);
Console.WriteLine(response.Choices[0].Message.Content); // Outputs a roast for "Dave"
```

## Important Notes

- **Unofficial Status:** This library is not officially supported by Grok or xAI. It’s a community-driven effort.
- **No Warranty:** Provided as-is with no guarantees. Use cautiously in production environments.
- **Swagger Generation:** Generated with NSwag v14.2.0.0 from a Grok-provided Swagger file. Since no official Swagger files were available, inaccuracies may exist. Refer to the [Grok API documentation](https://x.ai/api-docs) for official details.
- **JSON Serialization:** Uses `Newtonsoft.Json` due to enum case sensitivity issues with `System.Text.Json`. A future switch to `System.Text.Json` is planned.
- **Error Handling:** The library throws `ApiException` for server-side errors (e.g., HTTP 400 for invalid requests). Check `StatusCode` and `Response` for details.

## Contributing

We’d love your input! To contribute:
- Report issues or suggest features on the [GitHub repository](https://github.com/twhidden/grok).
- Submit pull requests with enhancements or fixes.

Please include tests (e.g., using MSTest as shown in `GrokClientTests`) to validate changes.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.