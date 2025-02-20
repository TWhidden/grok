using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;

namespace GrokSdk.Tests;

[TestClass]
public class GrokClientTests
{
    private static string? _apiToken;
    private static readonly object Lock = new object();
    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
    private static double _lastCallElapsedSeconds;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _apiToken = GetApiKeyFromFileOrEnv();
    }

    private static string GetApiKeyFromFileOrEnv()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GROK_API_KEY")))
        {
            return Environment.GetEnvironmentVariable("GROK_API_KEY")!;
        }

        string outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new Exception("Failed to get assembly location");
        string filePath = Path.Combine(outputDirectory, "apikey.txt");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("API key file 'apikey.txt' not found in the test output directory.");
        }

        string apiKey = File.ReadAllText(filePath).Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("API key file 'apikey.txt' is empty.");
        }

        return apiKey;
    }

    private static async Task WaitForRateLimitAsync()
    {
        double currentElapsed = Stopwatch.Elapsed.TotalSeconds;
        double timeSinceLastCall;
        lock (Lock)
        {
            timeSinceLastCall = currentElapsed - _lastCallElapsedSeconds;
        }
        if (timeSinceLastCall < 1)
        {
            double delaySeconds = 1 - timeSinceLastCall;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            currentElapsed = Stopwatch.Elapsed.TotalSeconds;
        }
        lock (Lock)
        {
            _lastCallElapsedSeconds = currentElapsed;
        }
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveHelloWorld_ReturnsValidResponse()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        var request = new ChatCompletionRequest
        {
            Messages = new Collection<Message>
            {
                new() { Role = MessageRole.System, Content = "You are a test assistant." },
                new() { Role = MessageRole.User, Content = "Say hello world." }
            },
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (ApiException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Id, "Response ID should not be null.");
        Assert.AreEqual("chat.completion", response.Object, "Response object type should be 'chat.completion'.");
        Assert.IsTrue(response.Choices.Count > 0, "Response should have at least one choice.");
        Assert.AreEqual("hello world!", response.Choices.First().Message.Content.ToLower(),
            "Response content should contain 'hello world' (case-insensitive).");
        Assert.AreEqual(ChoiceFinish_reason.Stop, response.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");
    }

    [TestMethod]
    public void GrokClient_Constructor_WithNullApiToken_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new GrokClient(new HttpClient(), null!),
            "Constructor should throw when API token is null.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveConversation_MaintainsContext()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        var messages = new List<Message>
        {
            new Message
            {
                Role = MessageRole.System,
                Content = "You are Grok, a helpful assistant. For this test conversation, please maintain context and respond deterministically to demonstrate your ability to remember details across multiple exchanges."
            }
        };

        messages.Add(new Message
        {
            Role = MessageRole.User,
            Content = "My name is TestUser. Please remember that."
        });
        var request1 = new ChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? response1 = null;
        try
        {
            response1 = await client.CreateChatCompletionAsync(request1);
        }
        catch (ApiException ex)
        {
            Assert.Fail($"First API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response1, "First response should not be null.");
        Assert.IsTrue(response1.Choices.Count > 0, "First response should have at least one choice.");
        string assistantResponse1 = response1.Choices.First().Message.Content.ToLower();
        Assert.IsTrue(assistantResponse1.Contains("testuser") || assistantResponse1.Contains("remember"),
            "First response should acknowledge the name 'TestUser' or indicate remembering it.");
        Assert.AreEqual(ChoiceFinish_reason.Stop, response1.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        messages.Add(new Message
        {
            Role = MessageRole.Assistant,
            Content = response1.Choices.First().Message.Content
        });

        messages.Add(new Message
        {
            Role = MessageRole.User,
            Content = "What is my name?"
        });
        var request2 = new ChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? response2 = null;
        try
        {
            response2 = await client.CreateChatCompletionAsync(request2);
        }
        catch (ApiException ex)
        {
            Assert.Fail($"Second API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response2, "Second response should not be null.");
        Assert.IsTrue(response2.Choices.Count > 0, "Second response should have at least one choice.");
        string assistantResponse2 = response2.Choices.First().Message.Content.ToLower();
        Assert.IsTrue(assistantResponse2.Contains("testuser"),
            "Second response should correctly recall the name 'TestUser'.");
        Assert.AreEqual(ChoiceFinish_reason.Stop, response2.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        messages.Add(new Message
        {
            Role = MessageRole.Assistant,
            Content = response2.Choices.First().Message.Content
        });

        messages.Add(new Message
        {
            Role = MessageRole.User,
            Content = "Good. Now, say 'Goodbye, TestUser!'"
        });
        var request3 = new ChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? response3 = null;
        try
        {
            response3 = await client.CreateChatCompletionAsync(request3);
        }
        catch (ApiException ex)
        {
            Assert.Fail($"Third API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response3, "Third response should not be null.");
        Assert.IsTrue(response3.Choices.Count > 0, "Third response should have at least one choice.");
        string assistantResponse3 = response3.Choices.First().Message.Content.ToLower();
        Assert.IsTrue(assistantResponse3.Contains("goodbye") && assistantResponse3.Contains("testuser"),
            "Third response should say 'Goodbye, TestUser!' or a close variation.");
        Assert.AreEqual(ChoiceFinish_reason.Stop, response3.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveCommandRoast_ReturnsRoastMessage()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        string targetName = "Dave";

        var request = new ChatCompletionRequest
        {
            Messages = new Collection<Message>
            {
                new Message
                {
                    Role = MessageRole.System,
                    Content = "You are a bot that responds to commands. When given the command '/roast \"name\"', generate a funny and light-hearted roast for the provided name."
                },
                new Message
                {
                    Role = MessageRole.User,
                    Content = $"/roast \"{targetName}\""
                }
            },
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (ApiException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Id, "Response ID should not be null.");
        Assert.AreEqual("chat.completion", response.Object, "Response object type should be 'chat.completion'.");
        Assert.IsTrue(response.Choices.Count > 0, "Response should have at least one choice.");

        string assistantResponse = response.Choices.First().Message.Content.ToLower();
        bool isRoast = assistantResponse.Contains("roast") || assistantResponse.Contains("funny") || assistantResponse.Contains(targetName.ToLower());
        Assert.IsTrue(isRoast, "Response should contain a roast-like message for the given name.");

        Assert.AreEqual(ChoiceFinish_reason.Stop, response.Choices.First().Finish_reason, "Finish reason should be 'stop'.");
    }
}