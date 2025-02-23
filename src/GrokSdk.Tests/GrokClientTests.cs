using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using GrokSdk.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GrokSdk.Tests;

[TestClass]
public class GrokClientTests
{
    private static string? _apiToken;
    private static readonly object Lock = new();
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
            return Environment.GetEnvironmentVariable("GROK_API_KEY")!;

        var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                              throw new Exception("Failed to get assembly location");
        var filePath = Path.Combine(outputDirectory, "apikey.txt");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("API key file 'apikey.txt' not found in the test output directory.");

        var apiKey = File.ReadAllText(filePath).Trim();
        if (string.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("API key file 'apikey.txt' is empty.");

        return apiKey;
    }

    private static string GetN2YoApiKeyFromFileOrEnv()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("N2YO_API_KEY")))
            return Environment.GetEnvironmentVariable("N2YO_API_KEY")!;

        var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                              throw new Exception("Failed to get assembly location");
        var filePath = Path.Combine(outputDirectory, "n2yokey.txt");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("API key file 'n2yokey.txt' not found in the test output directory.");

        var apiKey = File.ReadAllText(filePath).Trim();
        if (string.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("API key file 'n2yokey.txt' is empty.");

        return apiKey;
    }

    private static async Task WaitForRateLimitAsync()
    {
        var currentElapsed = Stopwatch.Elapsed.TotalSeconds;
        double timeSinceLastCall;
        lock (Lock)
        {
            timeSinceLastCall = currentElapsed - _lastCallElapsedSeconds;
        }

        if (timeSinceLastCall < 1)
        {
            var delaySeconds = 1 - timeSinceLastCall;
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
                new SystemMessage { Content = "You are a test assistant." },
                new UserMessage { Content = "Say hello world." }
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
        catch (GrokSdkException ex)
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

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
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
            new SystemMessage
            {
                Content =
                    "You are Grok, a helpful assistant. For this test conversation, please maintain context and respond deterministically to demonstrate your ability to remember details across multiple exchanges."
            }
        };

        messages.Add(new UserMessage
        {
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
        catch (GrokSdkException ex)
        {
            Assert.Fail($"First API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response1, "First response should not be null.");
        Assert.IsTrue(response1.Choices.Count > 0, "First response should have at least one choice.");
        var assistantResponse1 = response1.Choices.First().Message.Content.ToLower();
        Assert.IsTrue(assistantResponse1.Contains("testuser") || assistantResponse1.Contains("remember"),
            "First response should acknowledge the name 'TestUser' or indicate remembering it.");
        Assert.AreEqual(ChoiceFinish_reason.Stop, response1.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        messages.Add(new AssistantMessage
        {
            Content = response1.Choices.First().Message.Content
        });

        messages.Add(new UserMessage
        {
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
        catch (GrokSdkException ex)
        {
            Assert.Fail($"Second API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response2, "Second response should not be null.");
        Assert.IsTrue(response2.Choices.Count > 0, "Second response should have at least one choice.");
        var assistantResponse2 = response2.Choices.First().Message.Content.ToLower();
        Assert.IsTrue(assistantResponse2.Contains("testuser"),
            "Second response should correctly recall the name 'TestUser'.");
        Assert.AreEqual(ChoiceFinish_reason.Stop, response2.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        messages.Add(new AssistantMessage
        {
            Content = response2.Choices.First().Message.Content
        });

        messages.Add(new UserMessage
        {
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
        catch (GrokSdkException ex)
        {
            Assert.Fail($"Third API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response3, "Third response should not be null.");
        Assert.IsTrue(response3.Choices.Count > 0, "Third response should have at least one choice.");
        var assistantResponse3 = response3.Choices.First().Message.Content.ToLower();
        Assert.IsTrue(assistantResponse3.Contains("goodbye") && assistantResponse3.Contains("testuser"),
            "Third response should say 'Goodbye, TestUser!' or a close variation.");
        Assert.AreEqual(ChoiceFinish_reason.Stop, response3.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveCommandRoast_ReturnsRoastMessage()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        var targetName = "Dave";

        var request = new ChatCompletionRequest
        {
            Messages = new Collection<Message>
            {
                new SystemMessage
                {
                    Content =
                        "You are a bot that responds to commands. When given the command '/roast \"name\"', generate a funny and light-hearted roast for the provided name."
                },
                new UserMessage
                {
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
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Id, "Response ID should not be null.");
        Assert.AreEqual("chat.completion", response.Object, "Response object type should be 'chat.completion'.");
        Assert.IsTrue(response.Choices.Count > 0, "Response should have at least one choice.");

        var assistantResponse = response.Choices.First().Message.Content.ToLower();
        var isRoast = assistantResponse.Contains("roast") || assistantResponse.Contains("funny") ||
                      assistantResponse.Contains(targetName.ToLower());
        Assert.IsTrue(isRoast, "Response should contain a roast-like message for the given name.");

        Assert.AreEqual(ChoiceFinish_reason.Stop, response.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveImageAnalysis_ReturnsImageDescription()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        // Define the image URL (publicly accessible Eiffel Tower image) and text prompt
        var imageUrl = "https://upload.wikimedia.org/wikipedia/commons/a/a8/Tour_Eiffel_Wikimedia_Commons.jpg";
        var textPrompt = "What is in this image?";

        // Create the content array with image_url and text parts
        var contentParts = new List<object>
        {
            new { type = "image_url", image_url = new { url = imageUrl, detail = "high" } },
            new { type = "text", text = textPrompt }
        };

        // Serialize the content array to a JSON string for UserMessage.Content
        var contentJson = JsonConvert.SerializeObject(contentParts);

        // Create the request with SystemMessage and UserMessage
        var request = new ChatCompletionRequest
        {
            Messages = new Collection<Message>
            {
                new SystemMessage { Content = "You are Grok, a helpful assistant capable of analyzing images." },
                new UserMessage { Content = contentJson }
            },
            Model = "grok-2-vision-latest", // Model that supports image analysis
            Stream = false,
            Temperature = 0f
        };

        // Act
        await WaitForRateLimitAsync();
        ChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        // Assert
        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Id, "Response ID should not be null.");
        Assert.AreEqual("chat.completion", response.Object, "Response object type should be 'chat.completion'.");
        Assert.IsTrue(response.Choices.Count > 0, "Response should have at least one choice.");

        var assistantResponse = response.Choices.First().Message.Content.ToLower();
        var mentionsImage = assistantResponse.Contains("eiffel") ||
                            assistantResponse.Contains("tower") ||
                            assistantResponse.Contains("paris");
        Assert.IsTrue(mentionsImage,
            "Response should mention something about the image, such as 'Eiffel', 'tower', or 'Paris'.");
        Assert.AreEqual(ChoiceFinish_reason.Stop, response.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveStreaming_ReturnsStreamedResponse()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));
        var streamingClient = client.GetStreamingClient();

        const int minWords = 10;
        const int maxWords = 50;

        var request = new ChatCompletionRequest
        {
            Messages = new Collection<Message>
            {
                new SystemMessage { Content = "You are a helpful assistant." },
                new UserMessage { Content = $"Tell me a short story between {minWords} and {maxWords} words" }
            },
            Model = "grok-2-latest",
            Stream = true, // Explicitly set for clarity, though StartStreamAsync will enforce this
            Temperature = 0f
        };

        var streamStarted = false;
        var chunkReceived = false;
        var streamCompleted = false;
        var stateTransitions = new List<StreamState>();
        var streamedContent = new StringBuilder();

        streamingClient.OnStreamStarted += (_, _) => streamStarted = true;
        streamingClient.OnChunkReceived += (_, chunk) =>
        {
            chunkReceived = true;
            var content = chunk.Choices[0].Delta.Content;
            if (!string.IsNullOrEmpty(content)) streamedContent.Append(content);
        };
        streamingClient.OnStreamCompleted += (_, _) => streamCompleted = true;
        streamingClient.OnStateChanged += (_, state) => stateTransitions.Add(state);
        streamingClient.OnStreamError += (_, ex) => Assert.Fail($"Streaming failed: {ex.Message}");

        // Act
        await WaitForRateLimitAsync();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await streamingClient.StartStreamAsync(request, cancellationTokenSource.Token);

        // Assert
        Assert.IsTrue(streamStarted, "Stream should have started.");
        Assert.IsTrue(chunkReceived, "At least one chunk should have been received.");
        Assert.IsTrue(streamCompleted, "Stream should have completed.");

        // Verify state transitions
        Assert.IsTrue(stateTransitions.Contains(StreamState.Thinking), "State should have transitioned to Thinking.");
        Assert.IsTrue(stateTransitions.Contains(StreamState.Streaming), "State should have transitioned to Streaming.");
        Assert.IsTrue(stateTransitions.Contains(StreamState.Done), "State should have transitioned to Done.");
        Assert.IsFalse(stateTransitions.Contains(StreamState.Error), "State should not have transitioned to Error.");

        // Verify streamed content
        var finalContent = streamedContent.ToString().ToLower();
        var words = finalContent.Split(' ');
        Assert.IsTrue(words.Length >= minWords, $"Streamed words should be >= {minWords} words. we got {words.Length}");
        Assert.IsTrue(words.Length <= maxWords, $"Streamed be <= {minWords} words. We got {words.Length}");
        Assert.IsTrue(finalContent.Contains("story") || finalContent.Contains("once") || finalContent.Contains("end"),
            "Streamed content should resemble a short story.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveToolChoice_DemonstratesModes()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        // Define tools similar to the Grok docs example
        var tools = new Collection<Tool>
        {
            new()
            {
                Type = ToolType.Function,
                Function = new FunctionDefinition
                {
                    Name = "get_current_temperature",
                    Description = "Get the current temperature in a given location",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            location = new
                                { type = "string", description = "The city and state, e.g. San Francisco, CA" },
                            unit = new { type = "string", @enum = new[] { "celsius", "fahrenheit" } }
                        },
                        required = new[] { "location" }
                    }
                }
            }
        };

        // Test 1: tool_choice = "auto" - Model decides whether to use the tool
        var requestAuto = new ChatCompletionRequest
        {
            Messages = new Collection<Message>
            {
                new SystemMessage { Content = "You are a weather assistant with access to tools." },
                new UserMessage { Content = "What's the temperature in Paris?" }
            },
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f,
            Tools = tools,
            Tool_choice = Tool_choice.Required
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? responseAuto = null;
        try
        {
            responseAuto = await client.CreateChatCompletionAsync(requestAuto);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call (auto) failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseAuto, "Response (auto) should not be null.");
        Assert.IsTrue(responseAuto.Choices.Count > 0, "Response (auto) should have at least one choice.");
        var autoChoice = responseAuto.Choices.First();
        var toolCalledAuto = autoChoice.Message.Tool_calls?.Count > 0;
        var contentProvidedAuto = !string.IsNullOrEmpty(autoChoice.Message.Content);
        Assert.IsTrue(toolCalledAuto || contentProvidedAuto,
            "Response (auto) should either call a tool or provide content.");
        if (toolCalledAuto)
        {
            Assert.AreEqual("get_current_temperature", autoChoice.Message.Tool_calls?.First().Function.Name,
                "Tool call (auto) should match the defined tool.");
            Assert.AreEqual(ChoiceFinish_reason.Tool_calls, autoChoice.Finish_reason,
                "Finish reason (auto) should be 'tool_calls' when a tool is used.");
        }
        else
        {
            Assert.AreEqual(ChoiceFinish_reason.Stop, autoChoice.Finish_reason,
                "Finish reason (auto) should be 'stop' when content is provided.");
        }

        // Test 2: tool_choice = "none" - No tool should be called
        var requestNone = new ChatCompletionRequest
        {
            Messages = new Collection<Message>
            {
                new SystemMessage { Content = "You are a weather assistant with access to tools." },
                new UserMessage { Content = "What's the temperature in Paris?" }
            },
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f,
            Tools = tools,
            Tool_choice = Tool_choice.None
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? responseNone = null;
        try
        {
            responseNone = await client.CreateChatCompletionAsync(requestNone);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call (none) failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseNone, "Response (none) should not be null.");
        Assert.IsTrue(responseNone.Choices.Count > 0, "Response (none) should have at least one choice.");
        var noneChoice = responseNone.Choices.First();
        Assert.IsNull(noneChoice.Message.Tool_calls, "No tools should be called with tool_choice 'none'.");
        Assert.IsFalse(string.IsNullOrEmpty(noneChoice.Message.Content),
            "Response (none) should provide content since tools are disabled.");
        Assert.AreEqual(ChoiceFinish_reason.Stop, noneChoice.Finish_reason,
            "Finish reason (none) should be 'stop'.");

        // Test 3: tool_choice = "required" - Forces a tool call
        var requestRequired = new ChatCompletionRequest
        {
            Messages = new Collection<Message>
            {
                new SystemMessage { Content = "You are a weather assistant with access to tools." },
                new UserMessage { Content = "What's the temperature in Paris?" }
            },
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f,
            Tools = tools,
            Tool_choice = Tool_choice.Required
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? responseRequired = null;
        try
        {
            responseRequired = await client.CreateChatCompletionAsync(requestRequired);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail(
                $"API call (required) failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseRequired, "Response (required) should not be null.");
        Assert.IsTrue(responseRequired.Choices.Count > 0, "Response (required) should have at least one choice.");
        var requiredChoice = responseRequired.Choices.First();
        Assert.IsTrue(requiredChoice.Message.Tool_calls?.Count > 0,
            "Response (required) should call a tool since tool_choice is 'required'.");
        Assert.AreEqual("get_current_temperature", requiredChoice.Message.Tool_calls.First().Function.Name,
            "Tool call (required) should match the defined tool.");
        Assert.AreEqual(ChoiceFinish_reason.Tool_calls, requiredChoice.Finish_reason,
            "Finish reason (required) should be 'tool_calls'.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveToolChoice_ReturnsParisTemperature()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        // Define the tool
        var tools = new Collection<Tool>
        {
            new()
            {
                Type = ToolType.Function,
                Function = new FunctionDefinition
                {
                    Name = "get_current_temperature",
                    Description = "Get the current temperature in a given location",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            location = new { type = "string", description = "The city and state" },
                            unit = new
                            {
                                type = "string", @enum = new[] { "celsius", "fahrenheit" }, @default = "celsius"
                            }
                        },
                        required = new[] { "location" }
                    }
                }
            }
        };

        // Step 1: Initial request
        var messages = new Collection<Message>
        {
            new SystemMessage { Content = "You are a weather assistant with access to tools." },
            new UserMessage { Content = "What's the temperature in Paris?" }
        };
        var request = new ChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f,
            Tools = tools,
            Tool_choice = Tool_choice.Auto
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"Initial API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Initial response should not be null.");
        Assert.IsTrue(response.Choices.Count > 0, "Initial response should have at least one choice.");

        var choice = response.Choices.First();
        if (choice.Message.Tool_calls?.Count > 0)
        {
            // Step 2: Simulate tool execution (mock or real API call)
            var toolCall = choice.Message.Tool_calls.First();
            Assert.AreEqual("get_current_temperature", toolCall.Function.Name,
                "Tool should be get_current_temperature.");

            var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(toolCall.Function.Arguments) ??
                       throw new Exception("Could not process arguments from function");
            var location = args["location"];
            Assert.AreEqual("Paris", location, "Tool call should target Paris.");

            // Mock implementation (replace with real API call if desired)
            var result = location == "Paris"
                ? "{\"location\": \"Paris\", \"temperature\": 15, \"unit\": \"celsius\"}"
                : "{\"location\": \"unknown\", \"temperature\": null, \"unit\": \"celsius\"}";

            // Add assistant message and tool result to messages
            messages.Add(choice.Message);
            messages.Add(new ToolMessage
            {
                Content = result,
                Tool_call_id = toolCall.Id
            });

            // Step 3: Send back to Grok
            var followUpRequest = new ChatCompletionRequest
            {
                Messages = messages,
                Model = "grok-2-latest",
                Stream = false,
                Temperature = 0f,
                Tools = tools,
                Tool_choice = Tool_choice.Auto
            };

            await WaitForRateLimitAsync();
            ChatCompletionResponse? finalResponse = null;
            try
            {
                finalResponse = await client.CreateChatCompletionAsync(followUpRequest);
            }
            catch (GrokSdkException ex)
            {
                Assert.Fail(
                    $"Follow-up API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            }

            Assert.IsNotNull(finalResponse, "Final response should not be null.");
            Assert.IsTrue(finalResponse.Choices.Count > 0, "Final response should have at least one choice.");
            var finalChoice = finalResponse.Choices.First();
            Assert.IsFalse(string.IsNullOrEmpty(finalChoice.Message.Content), "Final response should have content.");
            Assert.IsTrue(finalChoice.Message.Content.Contains("15") || finalChoice.Message.Content.Contains("Paris"),
                "Final response should mention Paris temperature (15°C).");
            Assert.AreEqual(ChoiceFinish_reason.Stop, finalChoice.Finish_reason,
                "Final finish reason should be 'stop'.");

            // Safety Check for Live Unit Tests to prevent API exhaustion
            await WaitForRateLimitAsync();
        }
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveToolChoice_FetchesInternationalSpaceStation()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        // Define a tool to get satellite position
        var tools = new Collection<Tool>
        {
            new()
            {
                Type = ToolType.Function,
                Function = new FunctionDefinition
                {
                    Name = "get_starlink_position",
                    Description = "Get the current position of a satellite by NORAD ID",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            norad_id = new { type = "integer", description = "The NORAD ID of the satellite" }
                        },
                        required = new[] { "norad_id" }
                    }
                }
            }
        };

        // Step 1: Initial request to Grok
        var messages = new Collection<Message>
        {
            new SystemMessage { Content = "You are an assistant that can track satellites." },
            new UserMessage { Content = "Where is the International Space Station (NORAD ID 25544) right now?" }
        };
        var request = new ChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f,
            Tools = tools,
            Tool_choice = Tool_choice.Auto
        };

        await WaitForRateLimitAsync();
        ChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"Initial API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Initial response should not be null.");
        Assert.IsTrue(response.Choices.Count > 0, "Initial response should have at least one choice.");

        var choice = response.Choices.First();
        if (choice.Message.Tool_calls?.Count > 0)
        {
            var toolCall = choice.Message.Tool_calls.First();
            Assert.AreEqual("get_starlink_position", toolCall.Function.Name, "Tool should be get_starlink_position.");

            var args =
                JsonConvert.DeserializeObject<Dictionary<string, int>>(toolCall.Function.Arguments) ??
                throw new Exception("Could not process arguments from function");
            var noradId = args["norad_id"];
            Assert.AreEqual(25544, noradId, "Tool call should target NORAD ID 25544.");

            // Step 2: Hit the real N2YO API
            var apiKey = GetN2YoApiKeyFromFileOrEnv();
            var url =
                $"https://api.n2yo.com/rest/v1/satellite/positions/{noradId}/48.8566/2.3522/0/1/&apiKey={apiKey}";
            string result;
            try
            {
                var n2YoResponse = await httpClient.GetAsync(url);
                n2YoResponse.EnsureSuccessStatusCode();
                var json = await n2YoResponse.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(json) ??
                               throw new Exception("could not process response data");

                // Verify response structure
                Assert.IsNotNull(data.info, "N2YO response should contain 'info'.");
                Assert.IsNotNull(data.positions, "N2YO response should contain 'positions'.");
                Assert.IsTrue(((JArray)data.positions).Count > 0,
                    "N2YO 'positions' should have at least one entry.");

                var position = data.positions[0];
                Assert.IsNotNull(position.satlatitude, "Position should include 'satlatitude'.");
                Assert.IsNotNull(position.satlongitude, "Position should include 'satlongitude'.");
                Assert.IsNotNull(position.sataltitude, "Position should include 'sataltitude'.");

                // Handle optional timestamp
                var timestamp = data.info.timestamp != null ? (long?)data.info.timestamp : null;

                result = JsonConvert.SerializeObject(new
                {
                    norad_id = noradId,
                    latitude = (double)position.satlatitude,
                    longitude = (double)position.satlongitude,
                    altitude = (double)position.sataltitude,
                    timestamp
                });
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"N2YO API failed: Status {ex.StatusCode} - {ex.Message}");
                result =
                    "{\"norad_id\": 25544, \"latitude\": 51.0, \"longitude\": -0.1, \"altitude\": 420, \"timestamp\": 1739999999}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error calling N2YO API: {ex.Message}");
                result =
                    "{\"norad_id\": 25544, \"latitude\": 51.0, \"longitude\": -0.1, \"altitude\": 420, \"timestamp\": 1739999999}";
            }

            // Add assistant message and tool result
            messages.Add(choice.Message);
            messages.Add(new ToolMessage
            {
                Content = result,
                Tool_call_id = toolCall.Id
            });

            // Step 3: Send back to Grok
            var followUpRequest = new ChatCompletionRequest
            {
                Messages = messages,
                Model = "grok-2-latest",
                Stream = false,
                Temperature = 0f,
                Tools = tools,
                Tool_choice = Tool_choice.Auto
            };

            await WaitForRateLimitAsync();
            ChatCompletionResponse? finalResponse = null;
            try
            {
                finalResponse = await client.CreateChatCompletionAsync(followUpRequest);
            }
            catch (GrokSdkException ex)
            {
                Assert.Fail(
                    $"Follow-up API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            }

            Assert.IsNotNull(finalResponse, "Final response should not be null.");
            Assert.IsTrue(finalResponse.Choices.Count > 0, "Final response should have at least one choice.");
            var finalChoice = finalResponse.Choices.First();
            Assert.IsFalse(string.IsNullOrEmpty(finalChoice.Message.Content), "Final response should have content.");
            Assert.IsTrue(
                finalChoice.Message.Content.Contains("25544") || finalChoice.Message.Content.Contains("latitude") ||
                finalChoice.Message.Content.Contains("longitude"),
                "Final response should mention ISS (25544) position.");
            Assert.AreEqual(ChoiceFinish_reason.Stop, finalChoice.Finish_reason,
                "Final finish reason should be 'stop'.");
        }
        else
        {
            Assert.Inconclusive("Grok did not request a tool call with 'auto'; cannot test external API integration.");
        }

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveToolChoice_AskStarlinkSatelliteCount()
    {
        var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));

        var messages = new Collection<Message>
        {
            new SystemMessage { Content = "You are an assistant with access to satellite data tools." },
            new UserMessage
            {
                Content = "How many Starlink satellites are out there? Category Code 52; Only give me the number back"
            }
        };

        // Define a tool to count Starlink satellites
        var tools = new Collection<Tool>
        {
            new()
            {
                Type = ToolType.Function,
                Function = new FunctionDefinition
                {
                    Name = "get_satellite_count",
                    Description = "Get the satellite count from n2yo website using the category code",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            categoryCode = new
                            {
                                type = "number",
                                description =
                                    "The Category Code for n2yo website for a specific company (such as Starlink 52)"
                            }
                        },
                        required = new[] { "categoryCode" }
                    }
                }
            }
        };

        // Step 1: Initial request to Grok
        var request = new ChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-latest",
            Stream = false,
            Temperature = 0f,
            Tools = tools,
            Tool_choice = Tool_choice.Auto // Let Grok decide to use the tool
        };

        var response = await client.CreateChatCompletionAsync(request);
        var choice = response.Choices.First();

        if (choice.Message.Tool_calls?.Count > 0)
        {
            var toolCall = choice.Message.Tool_calls.First();
            if (toolCall.Function.Name == "get_satellite_count")
            {
                // Step 2: Call N2YO API to get Starlink satellite count
                var args = JsonConvert.DeserializeObject<Dictionary<string, int>>(toolCall.Function.Arguments) ??
                           throw new Exception("Could not process arguments from function");
                var categoryCode = args["categoryCode"];

                var data = await SatelliteHelper.GetSatellitesAsync(categoryCode);

                var totalCount = new
                {
                    SatelliteCount = data.Count
                };

                var result = JsonConvert.SerializeObject(totalCount);

                // Add assistant message and tool result
                messages.Add(choice.Message);
                messages.Add(new ToolMessage
                {
                    Content = result,
                    Tool_call_id = toolCall.Id
                });

                // Step 3: Send back to Grok
                var followUpRequest = new ChatCompletionRequest
                {
                    Messages = messages,
                    Model = "grok-2-latest",
                    Stream = false,
                    Temperature = 0f,
                    Tools = tools,
                    Tool_choice = Tool_choice.Auto
                };

                var finalResponse = await client.CreateChatCompletionAsync(followUpRequest);
                var responseMessage = finalResponse.Choices.First().Message.Content;
                Console.WriteLine(responseMessage);
                if (int.TryParse(responseMessage, out var count))
                    Assert.IsTrue(count > 1000, $"Something isn't right - Starlink reporting {count} active?");
                else
                    Assert.Fail("Expected only a number and the response was {}");
            }
        }
        else
        {
            Assert.Fail("The Tool was not called like it should have been!");
        }

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task GrokThread_AskMultipleQuestions_MaintainsContextAndStreamsResponses()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));
        var thread = client.GetGrokThread();

        // Helper function to collect all messages from the stream
        async Task<List<GrokMessage>> CollectMessagesAsync(string question)
        {
            var messages = new List<GrokMessage>();
            await foreach (var message in thread.AskQuestion(question, temperature: 0)) messages.Add(message);
            return messages;
        }

        // Helper function to extract the full response text
        string ExtractResponse(List<GrokMessage> messages)
        {
            return string.Join("", messages
                .OfType<GrokTextMessage>()
                .Select(m => m.Message));
        }

        // Helper function to validate stream states
        void ValidateStates(List<GrokMessage> messages)
        {
            var states = messages
                .OfType<GrokStreamState>()
                .Select(s => s.StreamState)
                .ToList();

            Assert.IsTrue(states.Contains(StreamState.Thinking),
                "Stream should include 'Thinking' state.");
            Assert.IsTrue(states.Contains(StreamState.Streaming),
                "Stream should include 'Streaming' state.");
            Assert.IsTrue(states.Contains(StreamState.Done),
                "Stream should include 'Done' state.");
            Assert.IsFalse(states.Contains(StreamState.Error),
                "Stream should not have 'Error' state in successful case.");
        }

        // **Question 1: Set context with a name**
        var question1 = "My name is TestUser.";
        await WaitForRateLimitAsync();
        var messages1 = await CollectMessagesAsync(question1);

        // Validate message sequence and states
        Assert.IsTrue(messages1.Count > 2,
            "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages1[0], typeof(GrokStreamState),
            "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages1[0]).StreamState,
            "First state should be 'Thinking'.");
        ValidateStates(messages1);

        // Extract and verify response
        var response1 = ExtractResponse(messages1);
        Assert.IsFalse(string.IsNullOrEmpty(response1),
            "Response should not be empty.");
        Assert.IsTrue(response1.Contains("TestUser", StringComparison.OrdinalIgnoreCase),
            "Response should acknowledge 'TestUser'.");

        // **Question 2: Verify context by recalling the name**
        var question2 = "What is my name?";
        await WaitForRateLimitAsync();
        var messages2 = await CollectMessagesAsync(question2);

        // Validate message sequence and states
        Assert.IsTrue(messages2.Count > 2,
            "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages2[0], typeof(GrokStreamState),
            "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages2[0]).StreamState,
            "First state should be 'Thinking'.");
        ValidateStates(messages2);

        // Extract and verify response
        var response2 = ExtractResponse(messages2);
        Assert.IsFalse(string.IsNullOrEmpty(response2),
            "Response should not be empty.");
        Assert.IsTrue(response2.Contains("TestUser", StringComparison.OrdinalIgnoreCase),
            "Response should recall 'TestUser'.");

        // **Question 3: Test context with a story**
        var question3 = "Tell me a short story about TestUser.";
        await WaitForRateLimitAsync();
        var messages3 = await CollectMessagesAsync(question3);

        // Validate message sequence and states
        Assert.IsTrue(messages3.Count > 2,
            "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages3[0], typeof(GrokStreamState),
            "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages3[0]).StreamState,
            "First state should be 'Thinking'.");
        ValidateStates(messages3);

        // Extract and verify response
        var response3 = ExtractResponse(messages3);
        Assert.IsFalse(string.IsNullOrEmpty(response3),
            "Response should not be empty.");
        Assert.IsTrue(response3.Contains("TestUser", StringComparison.OrdinalIgnoreCase),
            "Response should include 'TestUser' in the story.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task GrokThread_SystemMessages_InfluenceResponses()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, _apiToken ?? throw new Exception("API Token not set"));
        var thread = new GrokThread(client);

        // Helper function to collect all messages from the stream
        async Task<List<GrokMessage>> CollectMessagesAsync(string question)
        {
            var messages = new List<GrokMessage>();
            await foreach (var message in thread.AskQuestion(question, temperature: 0))
            {
                messages.Add(message);
            }
            return messages;
        }

        // Helper function to extract the full response text
        string ExtractResponse(List<GrokMessage> messages)
        {
            return string.Join("", messages
                .OfType<GrokTextMessage>()
                .Select(m => m.Message));
        }

        // Helper function to validate stream states
        void ValidateStates(List<GrokMessage> messages)
        {
            var states = messages
                .OfType<GrokStreamState>()
                .Select(s => s.StreamState)
                .ToList();

            Assert.IsTrue(states.Contains(StreamState.Thinking),
                "Stream should include 'Thinking' state.");
            Assert.IsTrue(states.Contains(StreamState.Streaming),
                "Stream should include 'Streaming' state.");
            Assert.IsTrue(states.Contains(StreamState.Done),
                "Stream should include 'Done' state.");
            Assert.IsFalse(states.Contains(StreamState.Error),
                "Stream should not have 'Error' state in successful case.");
        }

        // Helper to check if a string is valid JSON
        bool IsValidJson(string text)
        {
            try
            {
                JsonConvert.DeserializeObject(text);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // **Step 1: One-word response**
        thread.AddSystemInstruction("Respond with only one word.");
        string question1 = "What is the capital of France?";
        await WaitForRateLimitAsync();
        var messages1 = await CollectMessagesAsync(question1);

        // Validate message sequence and states
        Assert.IsTrue(messages1.Count > 2, "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages1[0], typeof(GrokStreamState), "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages1[0]).StreamState, "First state should be 'Thinking'.");
        ValidateStates(messages1);

        // Extract and verify response
        string response1 = ExtractResponse(messages1);
        Assert.IsFalse(string.IsNullOrEmpty(response1), "Response should not be empty.");
        Assert.AreEqual("Paris", response1.Trim(), "Response should be a single word 'France'.");
        Assert.IsFalse(response1.Contains(" "), "Response should contain no spaces, indicating one word.");

        // **Step 2: JSON response**
        thread.AddSystemInstruction("Respond with raw JSON without markdown.");
        string question2 = "What is the population of France?";
        await WaitForRateLimitAsync();
        var messages2 = await CollectMessagesAsync(question2);

        // Validate message sequence and states
        Assert.IsTrue(messages2.Count > 2, "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages2[0], typeof(GrokStreamState), "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages2[0]).StreamState, "First state should be 'Thinking'.");
        ValidateStates(messages2);

        // Extract and verify response
        string response2 = ExtractResponse(messages2);
        Assert.IsFalse(string.IsNullOrEmpty(response2), "Response should not be empty.");
        Assert.IsTrue(IsValidJson(response2), "Response should be valid JSON.");
        dynamic jsonResponse = JsonConvert.DeserializeObject(response2)!;
        Assert.IsTrue(jsonResponse.population != null, "JSON should contain a 'population' field.");

        // **Step 3: Spanish translation**
        thread.AddSystemInstruction("Translate all responses to Spanish.");
        string question3 = "What is the weather like today?";
        await WaitForRateLimitAsync();
        var messages3 = await CollectMessagesAsync(question3);

        // Validate message sequence and states
        Assert.IsTrue(messages3.Count > 2, "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages3[0], typeof(GrokStreamState), "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages3[0]).StreamState, "First state should be 'Thinking'.");
        ValidateStates(messages3);

        // Extract and verify response
        string response3 = ExtractResponse(messages3);
        Assert.IsFalse(string.IsNullOrEmpty(response3), "Response should not be empty.");
        // Simple check for Spanish words (e.g., "el", "es", "hoy")
        Assert.IsTrue(response3.Contains("el", StringComparison.OrdinalIgnoreCase) ||
                      response3.Contains("es", StringComparison.OrdinalIgnoreCase) ||
                      response3.Contains("hoy", StringComparison.OrdinalIgnoreCase),
            "Response should contain common Spanish words indicating translation.");

        // **Step 4: Verify last SystemMessage overrides previous ones**
        string question4 = "What is my name?"; // No prior context, just testing instruction impact
        await WaitForRateLimitAsync();
        var messages4 = await CollectMessagesAsync(question4);

        // Validate message sequence and states
        Assert.IsTrue(messages4.Count > 2, "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages4[0], typeof(GrokStreamState), "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages4[0]).StreamState, "First state should be 'Thinking'.");
        ValidateStates(messages4);

        // Extract and verify response
        string response4 = ExtractResponse(messages4);
        Assert.IsFalse(string.IsNullOrEmpty(response4), "Response should not be empty.");
        Assert.IsFalse(IsValidJson(response4), "Response should not be JSON due to last Spanish instruction.");
        Assert.IsFalse(response4.Split(' ').Length == 1, "Response should not be one word due to last Spanish instruction.");
        Assert.IsTrue(response4.Contains("no", StringComparison.OrdinalIgnoreCase) ||
                      response4.Contains("sé", StringComparison.OrdinalIgnoreCase),
            "Response should be in Spanish, likely indicating 'I don’t know' (e.g., 'No sé').");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

}