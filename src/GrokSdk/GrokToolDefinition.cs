namespace GrokSdk;

/// <summary>
///     Defines a tool that can be registered with GrokThread, including its metadata and execution logic.
/// </summary>
public class GrokToolDefinition
{
    private readonly Func<string, Task<string>> _execute;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GrokToolDefinition" /> class.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">The description of the tool.</param>
    /// <param name="parameters">The JSON schema for the tool's parameters.</param>
    /// <param name="execute">The function to execute when the tool is called.</param>
    public GrokToolDefinition(
        string name,
        string description,
        object parameters,
        Func<string, Task<string>> execute)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <summary>
    ///     Gets the name of the tool.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the description of the tool.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Gets the JSON schema for the tool's parameters.
    /// </summary>
    public object Parameters { get; }

    /// <summary>
    ///     Executes the tool with the given arguments.
    /// </summary>
    /// <param name="arguments">The JSON-serialized arguments for the tool.</param>
    /// <returns>
    ///     A <see cref="ValueTask{TResult}" /> representing the asynchronous operation, containing the JSON-serialized
    ///     result.
    /// </returns>
    public Task<string> Execute(string arguments)
    {
        return _execute(arguments);
    }
}