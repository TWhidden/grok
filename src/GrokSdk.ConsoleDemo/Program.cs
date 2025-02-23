using GrokSdk;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Initialize the HTTP client and GrokClient
        var httpClient = new HttpClient();
        var sdk = new GrokClient(httpClient,
            "xai-Key-Here");

        // Create a GrokThread instance to manage the conversation
        var thread = new GrokThread(sdk);

        // Welcome message
        Console.WriteLine("Welcome to the Grok Chat Console. Type your questions below. Type 'quit' to exit.");

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

            try
            {
                // Ask the question and get the streaming response
                var messages = thread.AskQuestion(input);

                // Stream the response parts with colors
                await foreach (var message in messages)
                    if (message is GrokStreamState state)
                    {
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
                                Console.WriteLine("Done processing...");
                                break;
                            case StreamState.Error:
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Error Processing...");
                                break;
                        }

                        Console.ResetColor();
                    }
                    else if (message is GrokTextMessage textMessage)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(textMessage.Message); // Print each response chunk
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
}