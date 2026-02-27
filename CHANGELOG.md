# Changelog

All notable changes to GrokSdk will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.0] - 2026-02-16

### Added

- **MCP (Model Context Protocol) Support**: New `GrokToolMcp` class connects Grok to external MCP servers via the Responses API.
  - Single or multi-server configurations via `GrokMcpServerConfig`.
  - Supports `server_url`, `server_label`, `server_description`, `allowed_tool_names`, `authorization`, and `extra_headers`.
  - Direct `QueryAsync()` method for programmatic use.
  - Full `IGrokTool` integration for use with `GrokThread`.
- **Code Execution / Code Interpreter**: New `GrokToolCodeExecution` class runs code in xAI's sandboxed environment.
  - Mathematical calculations, data analysis, and general computation.
  - Returns text results with code block details.
  - Direct `QueryAsync()` method for programmatic use.
  - Full `IGrokTool` integration for use with `GrokThread`.
- **Video Generation**: New `GrokToolVideoGeneration` class for text-to-video, image-to-video, and video editing.
  - Asynchronous generation with automatic polling.
  - `StartAsync()` for manual polling, `GenerateAsync()` for automatic wait.
  - Configurable duration (1-15s), aspect ratio, and resolution.
  - Full `IGrokTool` integration for use with `GrokThread`.
- **Deferred Completions**: New `GrokDeferredCompletion` class for asynchronous chat completions.
  - `SubmitAsync()` returns a request ID for manual polling.
  - `CreateAndWaitAsync()` submits and waits for completion.
  - `AskDeferredAsync()` convenience method for simple prompts.
  - Results available for 24 hours via GET polling.
- **Structured Output Helper**: New `GrokStructuredOutput` static class for JSON schema-constrained outputs.
  - `CreateJsonFormat<T>()` auto-generates JSON schema from C# types.
  - `AskAsJsonAsync<T>()` sends a prompt and deserializes the response.
  - Type-safe responses with automatic schema generation via reflection.
- **Developer Role**: New `AddDeveloperInstruction()` on `GrokThread` for the `developer` message role.
  - Provides instruction-level context that persists through history compression.
  - Serializes as `developer` role in the API request (distinct from `system`).
- **Parallel Tool Execution**: `GrokThread` now executes multiple tool calls in parallel via `Task.WhenAll`.
  - Significant performance improvement when Grok requests multiple tools simultaneously.
  - Results are still processed in order for deterministic behavior.
- **Structured Output Support**: Added `response_format` to `GrokChatCompletionRequest` and `GrokResponseRequest` for JSON schema-constrained outputs via `GrokResponseFormat` and `GrokJsonSchemaDefinition`.
- **Deferred Property**: Added `deferred` boolean to `GrokChatCompletionRequest` for deferred processing.
- **Enhanced Responses API**: Added `include` (string[]) and `max_turns` (int?) parameters to `GrokResponseRequest`.
- **New Tool Types**: `GrokResponseToolType` now includes `File_search` and `Mcp` in addition to existing types.
- **File Search Properties**: `GrokResponseTool` now supports `vector_store_ids` and `max_num_results`.
- **New output item types**: Added `file_search_call` and `mcp_call` to `GrokResponseOutputItem` type enum.
- **Collections Search Tool**: New `GrokToolCollectionsSearch` class for file/vector store search via the Responses API.
  - Search uploaded documents using `vector_store_ids` and `max_num_results`.
  - Direct `QueryAsync()` method for programmatic use.
  - Full `IGrokTool` integration for use with `GrokThread`.
- **Responses API Streaming**: New `GrokResponsesStreamingClient` for Server-Sent Events (SSE) streaming from the Responses API.
  - Event-based streaming via `StartStreamAsync()` with events: `OnResponseCreated`, `OnTextDelta`, `OnTextDone`, `OnResponseCompleted`, `OnStreamError`.
  - `IAsyncEnumerable` support on .NET 8+ via `StreamAsync()`, callback pattern on netstandard2.0.
  - Factory method: `client.GetResponsesStreamingClient()`.
- **Streaming Tool Call Deltas**: Chat Completions streaming now supports incremental tool call accumulation.
  - New `ToolCallDelta`, `ToolCallFunctionDelta` classes for partial tool call data.
  - `StreamingToolCallAccumulator` accumulates argument fragments into complete `GrokToolCall` objects.
  - `OnToolCallsReceived` event on `GrokStreamingClient` fires when tool calls are fully accumulated.
  - `MessageDelta.ToolCalls` property for raw delta access.
- **Citation Support**: `GrokThread` now emits `GrokCitationMessage` records when citations are present in responses.
  - `GrokCitation` class with `Type`, `Url`, `Title`, `StartIndex`, `EndIndex` properties.
  - `GrokCitationMessage` record containing a list of citations, yielded in the async stream.
- **Tool Usage Tracking**: All Responses API tool wrappers now expose `LastToolUsages` for server-side tool call details.
  - `GrokToolUsage` class captures `Type`, `Id`, `Name`, `Status`, and `Action` details.
  - Available on `GrokToolWebSearch`, `GrokToolCodeExecution`, `GrokToolMcp`, `GrokToolCollectionsSearch`.
- **Conversation Continuation**: All Responses API tool wrappers now support `GrokResponsesToolOptions`.
  - `PreviousResponseId` for multi-turn conversation continuation.
  - `MaxOutputTokens` and `MaxTurns` for controlling response limits.
  - `Include` list for encrypted reasoning content.
  - `Store` flag for enabling response storage.
  - `LastResponseId` tracks the response ID from the most recent API call.
- **Comprehensive test coverage**: New live API tests for video generation (5 tests), deferred completions (3 tests), structured output (5 tests), developer role (3 tests), MCP tool (5 tests), Code Execution tool (5 tests), plus unit tests for streaming accumulator, tool options, citations, and collections search.

### Fixed

- **`GrokToolImageUnderstanding` default model**: Changed from deprecated `grok-2-vision-latest` to `grok-4-1-fast-reasoning`.

### Changed

- **Image Generation API default model**: Changed `GrokImageGenerationRequest.Model` default from discontinued `grok-2-image-1212` to `grok-imagine-image` in OpenAPI spec and generated client.
- Removed all remaining `grok-2-vision-latest` references from image understanding tests (replaced with `grok-4-1-fast-reasoning`).
- Regenerated `GrokClient.cs` from updated OpenAPI spec with all new types and enum values.
- `GrokThread.ProcessConversationAsync` now executes tool calls in parallel instead of sequentially.

## [1.4.0] - 2026-02-15

### Breaking Changes

- **Removed `GrokToolLiveSearch`**: The deprecated `search_parameters` API returned HTTP 410 (Gone). This class has been completely removed.
- **Removed deprecated schemas**: `GrokSearchParameters`, `GrokSearchParametersMode`, `GrokSource`, `GrokWebSource`, `GrokXSource`, `GrokNewsSource`, `GrokRssSource` have all been removed.
- **Removed `live_search` from `GrokToolType`**: The enum now only contains `Function`.
- **Removed `search_parameters` from `GrokChatCompletionRequest`**: No longer supported by the xAI API.

### Added

- **xAI Responses API** (`/v1/responses`): Full support with `CreateResponseAsync`, `GetResponseAsync`, `DeleteResponseAsync` methods.
- **`GrokToolWebSearch`**: New replacement tool for web and X/Twitter search using the Responses API.
  - Supports `web_search`, `x_search`, or both search types.
  - Configurable search filters: `allowed_domains`, `excluded_domains`, `allowed_x_handles`, `excluded_x_handles`, `from_date`, `to_date`.
  - Image and video understanding support.
  - Citation annotations in search results.
- **New generated types**: `GrokResponseRequest`, `GrokResponseInputMessage`, `GrokResponseTool`, `GrokResponseToolType` (web_search, x_search, code_interpreter, function), `GrokResponseResponse`, `GrokResponseOutputItem`, `GrokResponseOutputContent`, `GrokResponseAnnotation`, `GrokResponseUsage`, and more.
- **Function tools in Responses API**: Support for `name`, `description`, `parameters`, `strict` fields directly on `GrokResponseTool`.
- **Comprehensive test coverage**: 10 new live API tests covering basic responses, web search, X search, combined search, code interpreter, function calling, and the `GrokToolWebSearch` wrapper.

### Migration

Replace `GrokToolLiveSearch` with `GrokToolWebSearch`:

```csharp
// Before (removed):
thread.RegisterTool(new GrokToolLiveSearch(client));

// After:
thread.RegisterTool(new GrokToolWebSearch(client));
```

## [1.3.1] - 2026-02-02

### Changed - Model Deprecations

Updated to comply with xAI's deprecation notice (effective February 28, 2026):

- **Image Generation**: Changed default model from `grok-2-image-1212` to `grok-imagine-image`
  - New model delivers significantly superior image quality
- **Reasoning**: Changed `GrokToolReasoning` default model from `grok-3-mini` to `grok-4-1-fast-reasoning`
- **Vision/Image Understanding**: Updated image understanding to use `grok-4-1-fast-reasoning`
  - Grok 4.1 Fast models now support multimodal (text + image) inputs natively

### Updated

- All test files updated to use new model names
- README documentation updated with:
  - New supported models table (removed deprecated models)
  - Deprecation notice with February 28, 2026 deadline
  - Updated code examples to use `grok-4-1-fast-reasoning` for vision tasks

### Deprecated

The following models are deprecated by xAI and will stop working on February 28, 2026:

- `grok-2-image-1212` - Use `grok-imagine-image` instead
- `grok-3-mini` - Use `grok-4-1-fast-reasoning` instead  
- `grok-2-vision-1212` - Use `grok-4-1-fast-reasoning` instead

### Model Compatibility Notes

- `GrokToolReasoningArgs.Effort` / `reasoning_effort` parameter - Only supported by legacy models (grok-3-mini). Grok 4+ models have built-in reasoning and will return an error if this parameter is set. `GrokToolReasoning` now conditionally applies this parameter only for legacy models. See [xAI Models Documentation](https://docs.x.ai/docs/models).

### Added

- NuGet package icon (xai.png)

---

## [1.3.0] and earlier

See [GitHub Releases](https://github.com/twhidden/grok/releases) for previous version history.
