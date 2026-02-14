# Changelog

All notable changes to GrokSdk will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
