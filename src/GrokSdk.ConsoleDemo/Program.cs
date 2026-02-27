using GrokSdk;
using GrokSdk.Tools;
using Newtonsoft.Json;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Initialize the HTTP client and GrokClient
        string apiKey = GetApiKey();
        var httpClient = new HttpClient();
        var sdk = new GrokClient(httpClient, apiKey);

        // Create a GrokThread instance to manage the conversation
        var thread = new GrokThread(sdk);

        // Set initial model
        string currentModel = "grok-4-fast";

        // Register built-in tools
        thread.RegisterTool(new GrokToolImageGeneration(sdk));
        thread.RegisterTool(new GrokToolReasoning(sdk));
        thread.RegisterTool(new GrokToolWebSearch(sdk, currentModel));
        thread.RegisterTool(new GrokToolImageUnderstanding(sdk));
        thread.RegisterTool(new GrokToolCodeExecution(sdk));
        thread.RegisterTool(new GrokToolVideoGeneration(sdk));
        thread.RegisterTool(new GrokToolMcp(sdk, "https://mcp.deepwiki.com/mcp", "deepwiki"));

        // Welcome message with instructions
        Console.WriteLine("=== Grok Chat Console ===");
        Console.WriteLine($"Model: {currentModel}");
        Console.WriteLine("Commands: 'quit' to exit, 'm' to switch model");
        Console.WriteLine("Registered tools: Image Generation, Reasoning, Web Search,");
        Console.WriteLine("  Image Understanding, Code Execution, Video Generation, MCP (DeepWiki)");
        Console.WriteLine();

        // Main interaction loop
        while (true)
        {
            // Prompt for user input with color
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("You: ");
            Console.ResetColor();
            var input = Console.ReadLine();

            // Check for exit condition
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "quit") break;

            // Check for model switch hotkey
            if (input.Trim().ToLower() == "m")
            {
                currentModel = currentModel == "grok-3" ? "grok-4-fast" : "grok-3";
                Console.WriteLine($"Switched to model: {currentModel}");
                continue;
            }

            try
            {
                // Ask the question using the current model and get the streaming response
                var messages = thread.AskQuestion(input, model: currentModel);

                // Stream the response parts with colors
                await foreach (var message in messages)
                {
                    switch (message)
                    {
                        case GrokStreamState state:
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
                                    Console.WriteLine("Done.");
                                    break;
                                case StreamState.Error:
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Error Processing...");
                                    break;
                                case StreamState.CallingTool:
                                    Console.ForegroundColor = ConsoleColor.Magenta;
                                    Console.WriteLine("Calling Tool...");
                                    break;
                            }
                            Console.ResetColor();
                            break;

                        case GrokTextMessage textMessage:
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.Write(textMessage.Message);
                            Console.ResetColor();
                            break;

                        case GrokToolResponse toolResponse:
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"\n[Tool: {toolResponse.ToolName}]");
                            // Truncate long tool output for display
                            var toolOutput = toolResponse.ToolResponse;
                            if (toolOutput.Length > 500)
                                toolOutput = toolOutput[..500] + "... (truncated)";
                            Console.WriteLine(toolOutput);
                            Console.ResetColor();
                            break;

                        case GrokCitationMessage citationMessage:
                            Console.ForegroundColor = ConsoleColor.DarkCyan;
                            Console.WriteLine("\nSources:");
                            foreach (var citation in citationMessage.Citations)
                            {
                                var title = citation.Title ?? citation.Url;
                                Console.WriteLine($"  [{title}] {citation.Url}");
                            }
                            Console.ResetColor();
                            break;

                        case GrokServiceMessage serviceMessage:
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"[Service] {serviceMessage.Message}");
                            Console.ResetColor();
                            break;

                        case GrokError error:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[Error] {error.Exception.Message}");
                            Console.ResetColor();
                            break;
                    }
                }

                Console.ResetColor();
                Console.WriteLine(); // Add a new line after the full response
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        // Farewell message
        Console.WriteLine("Goodbye!");
    }

    /// <summary>
    /// Retrieves the xAI API key, either from a stored file in the current directory or by prompting the user.
    /// </summary>
    /// <returns>The xAI API key.</returns>
    private static string GetApiKey()
    {
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), ".xai_key");
        if (File.Exists(filePath))
        {
            string key = File.ReadAllText(filePath).Trim();
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }
        }

        Console.WriteLine("Please enter your xAI API key:");
        string enteredKey = Console.ReadLine()?.Trim() ?? throw new Exception("Failed to get Console ReadLine");
        while (string.IsNullOrEmpty(enteredKey))
        {
            Console.WriteLine("API key cannot be empty. Please enter a valid key:");
            enteredKey = Console.ReadLine()?.Trim() ?? throw new Exception("Failed to get Console ReadLine");
        }

        File.WriteAllText(filePath, enteredKey);
        return enteredKey;
    }
}