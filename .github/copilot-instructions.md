# GrokSdk Development Guide

## Coding Principles & Standards

**You are an expert-level C# developer** with deep knowledge of SOLID principles, design patterns, and .NET best practices. All code contributions must adhere to professional software engineering standards.

### Core Development Principles

**Single Responsibility Principle (SRP)**: Each class, method, and component should have one clear responsibility. If a class does more than one thing, refactor it into multiple focused components.

**DRY (Don't Repeat Yourself)**: Eliminate code duplication through proper abstractions, base classes, extension methods, or shared utilities. Repeated logic should be extracted into reusable components.

**API Stability**: Preserve existing public APIs unless explicitly requested to make breaking changes. When adding features:
- Extend via new methods/overloads rather than modifying signatures
- Use optional parameters for backward compatibility
- Mark obsolete APIs with `[Obsolete]` before removal
- Follow semantic versioning principles

**Simplicity First**: Keep changes minimal and focused. Prefer simple, readable solutions over clever complexity. Each change should do one thing well.

**Documentation Synchronization**: When modifying public APIs or adding new features, update the README.md files to stay in sync with the code. Documentation should reflect:
- New public methods, classes, and interfaces
- Changed method signatures or behavior
- New features and usage examples
- Breaking changes and migration guidance

### Expert C# Design Patterns

**Abstractions & Interfaces**: 
- Define contracts via interfaces (`IGrokTool`, `IGrokClient`) to enable testability and extensibility
- Use abstract base classes for shared implementation (`GrokMessageBase`)
- Favor composition over inheritance where appropriate
- Apply dependency injection for loose coupling

**Type Safety**: 
- Leverage C# record types for immutable data transfer objects (`GrokTextMessage`, `GrokStreamState`)
- Use discriminated unions via inheritance hierarchies for type-safe message handling
- Prefer strongly-typed parameters over stringly-typed dictionaries
- Apply nullable reference types to eliminate null reference exceptions

**Async/Await Best Practices**:
- All I/O operations must be async (`IAsyncEnumerable<T>` for streams)
- Avoid `async void` except for event handlers
- Use `ConfigureAwait(false)` in library code to prevent deadlocks
- Properly dispose async resources with `await using`

**SOLID Principles**:
- **Open/Closed**: Tools system extensible via `IGrokTool` without modifying core library
- **Liskov Substitution**: Message types interchangeable via `GrokMessageBase`
- **Interface Segregation**: Focused interfaces like `IGrokTool` vs. monolithic contracts
- **Dependency Inversion**: Depend on abstractions (`IGrokTool`) not concretions

**Defensive Programming**:
- Validate inputs at public API boundaries
- Provide meaningful exception messages
- Use guard clauses to fail fast
- Document preconditions and invariants

**Code Organization**:
- Namespace structure matches folder hierarchy
- Internal implementation details marked `internal` or `private`
- Public API surface kept minimal and intentional
- Related functionality grouped into cohesive classes

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

## Testing Requirements

**CRITICAL**: All API changes MUST have passing tests before work is considered complete.

### When to Write/Update Tests
- **New API features**: Create tests that validate the new functionality works with the live API
- **API changes**: Update existing tests and add new ones to cover changed behavior
- **Bug fixes**: Add regression tests that would have caught the bug

### Test Execution Checklist
Before marking any API-related work as complete:

1. **Build the solution**: `dotnet build` must succeed with no errors
2. **Run affected tests**: Execute tests related to your changes
3. **Run full test suite**: `dotnet test` to ensure no regressions
4. **Verify Live tests**: Tests marked `[TestCategory("Live")]` must pass against the actual xAI API

### Test File Conventions
- Test files follow pattern: `Grok{Feature}Tests.cs`
- Inherit from `GrokClientTestBaseClass` for API key handling and rate limiting
- Use `WaitForRateLimitAsync()` between API calls to prevent rate limiting
- Wrap API calls in try-catch with `Assert.Fail` for meaningful error messages:
  ```csharp
  try
  {
      response = await client.CreateChatCompletionAsync(request);
  }
  catch (GrokSdkException ex)
  {
      Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
  }
  ```

### Test Categories
- `[TestCategory("Live")]`: Requires live API access (uses real API key)

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

## Versioning & Release Notes

**IMPORTANT**: When adding, changing, or removing features, the following must be updated:

1. **Version Number**: Update `<version>` in both:
   - `GrokSdk.nuspec`
   - `GrokSdk.csproj`

2. **Release Notes**: Update the `<releaseNotes>` section in `GrokSdk.nuspec` with:
   - Breaking changes (model deprecations, API changes)
   - New features added
   - Bug fixes
   - Updated dependencies

3. **CHANGELOG.md**: Add a new version entry following the Keep a Changelog format

4. **README.md**: Update documentation to reflect any user-facing changes

This ensures the NuGet package metadata stays in sync with the codebase.
