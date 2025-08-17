# GrokSdk Development Guide

## Architecture Overview

GrokSdk is a .NET library providing a streaming conversation interface to xAI's Grok API. The architecture centers around three core components:

- **GrokClient**: Auto-generated API client from OpenAPI spec (`grok-api.yaml`) using NSwag
- **GrokThread**: Conversation state manager with tool integration and async streaming
- **Tool System**: Extensible plugin architecture via `IGrokTool` interface

### Key Design Patterns

**Conversation Management**: `GrokThread` maintains message history and handles multi-turn conversations with automatic tool execution. Messages are typed records (`GrokTextMessage`, `GrokServiceMessage`, `GrokError`, etc.).

**Streaming Architecture**: All API interactions use async streams (`IAsyncEnumerable<GrokMessageBase>`). Stream states are communicated via `GrokStreamState` records with explicit `StreamState` enum values.

**Tool Integration**: Tools implement `IGrokTool` with JSON schema parameters. Built-in tools include image generation, reasoning, live search, and image understanding. Use `GrokToolDefinition` for simple function-based tools.

## Project Structure

```
src/
├── GrokSdk/                 # Main library
│   ├── GrokClient.cs        # Auto-generated API client (don't edit directly)
│   ├── GrokThread.cs        # Conversation manager
│   ├── GrokStreaming.cs     # Streaming client
│   ├── Tools/               # Tool system
│   └── grok-api.yaml        # OpenAPI specification
├── GrokSdk.Tests/           # Unit tests with live API integration
└── GrokSdk.ConsoleDemo/     # Working example implementation
```

## Development Workflows

### Client Generation
Regenerate API client when OpenAPI spec changes:
```powershell
cd src/GrokSdk
.\GenerateClient.ps1
```

### Building & Testing
```bash
cd src
dotnet restore
dotnet build
dotnet test  # Requires GROK_API_KEY environment variable
```

### NuGet Packaging
Uses custom `.nuspec` file with multi-targeting (net8.0, netstandard2.0):
```bash
dotnet pack --configuration Release
```

## Key Conventions

### Message Flow
1. User input → `thread.AskQuestion()` → async enumerable stream
2. Stream yields typed messages: text content, state changes, tool responses, errors
3. Tools execute automatically when Grok requests them via function calling

### Error Handling
- API errors wrapped in `GrokError` records within the stream
- Rate limiting handled in tests via `WaitForRateLimitAsync()`
- Streaming errors propagated through `StreamState.Error`

### Tool Development
```csharp
// Simple function-based tool
thread.RegisterTool(new GrokToolDefinition(
    "tool_name",
    "Description for Grok to understand when to use this",
    jsonSchemaObject,
    async args => { /* implementation */ return jsonResult; }
));

// Complex tool inheriting IGrokTool
public class CustomTool : IGrokTool
{
    public string Name => "custom_tool";
    public string Description => "Tool description";
    public object Parameters => new { /* JSON schema */ };
    public async Task<string> ExecuteAsync(string arguments) { /* implementation */ }
}
```

### Testing Patterns
- Tests use `[TestCategory("Live")]` for API integration tests
- API keys loaded from files (`apikey.txt`) or environment variables
- Use `DataTestMethod` with multiple model versions for compatibility testing

## Integration Points

- **Newtonsoft.Json**: Primary serialization for all API communication
- **System.Threading.Channels**: Used for async streaming in netstandard2.0
- **NSwag**: Code generation from OpenAPI specs
- **GitHub Actions**: CI/CD with secret-based API key management

## Critical Files

- `GrokThread.cs`: Core conversation logic and tool orchestration
- `GrokStreaming.cs`: Low-level streaming implementation
- `Tools/IGrokTool.cs`: Tool contract definition
- `GenerateClient.ps1`: Client regeneration script
- `GrokSdk.nuspec`: Custom NuGet packaging configuration
