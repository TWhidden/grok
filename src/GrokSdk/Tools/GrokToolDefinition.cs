namespace GrokSdk.Tools;

/// <summary>
/// Provides a basic implementation of <see cref="IGrokTool"/> for creating custom tools with minimal boilerplate.
/// This class is ideal for simple tools defined via a function.
/// </summary>
public class GrokToolDefinition : IGrokTool
{
    private readonly Func<string, Task<string>> _execute;

    /// <summary>
    /// Initializes a new instance of <see cref="GrokToolDefinition"/> with the specified properties.
    /// </summary>
    /// <param name="name">The unique name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="parameters">The JSON schema for the tool's parameters.</param>
    /// <param name="execute">The function that executes the tool's logic.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public GrokToolDefinition(string name, string description, object parameters, Func<string, Task<string>> execute)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <summary>
    /// Gets the unique name of the tool.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a brief description of the tool's purpose.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the JSON schema defining the tool's input parameters.
    /// </summary>
    public object Parameters { get; }

    /// <summary>
    /// Executes the tool with the given arguments.
    /// </summary>
    /// <param name="arguments">A JSON-serialized string containing the input arguments.</param>
    /// <returns>A task resolving to a JSON-serialized string with the execution result.</returns>
    public Task<string> ExecuteAsync(string arguments) => _execute(arguments);
}