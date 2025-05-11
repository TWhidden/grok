namespace GrokSdk.Tools;

/// <summary>
/// Defines the contract for tools that can be registered with <see cref="GrokThread"/>.
/// Tools implementing this interface can be invoked by Grok during a conversation to extend functionality.
/// </summary>
public interface IGrokTool
{
    /// <summary>
    /// Gets the unique name of the tool, used by Grok to identify and call it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a brief description of what the tool does, aiding Grok in deciding when to use it.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the JSON schema defining the structure and types of the tool's input parameters.
    /// </summary>
    object Parameters { get; }

    /// <summary>
    /// Executes the tool with the provided arguments and returns a result.
    /// </summary>
    /// <param name="arguments">A JSON-serialized string containing the input arguments for the tool.</param>
    /// <returns>A task that resolves to a JSON-serialized string representing the tool's output.</returns>
    Task<string> ExecuteAsync(string arguments);
}