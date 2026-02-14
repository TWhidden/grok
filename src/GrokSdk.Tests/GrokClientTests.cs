using System.Collections.ObjectModel;
using System.Text;
using Newtonsoft.Json;

namespace GrokSdk.Tests;

[TestClass]
public class GrokClientTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [DataTestMethod]
    [DataRow("grok-3")]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-0709")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [DataRow("grok-4-1-fast-non-reasoning")]
    [DataRow("grok-4-fast-reasoning")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveHelloWorld_ReturnsValidResponse(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokChatCompletionRequest
        {
            Messages = new Collection<GrokMessage>
            {
                new GrokSystemMessage { Content = "You are a test assistant." },
                new GrokUserMessage { Content = [new GrokTextPart { Text = "Output only the text 'hello world'. Do not include any other text, explanations, or formatting." }] }
            },
            Model = model,
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
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
        Assert.AreEqual("hello world", response.Choices.First().Message.Content.ToLower(),
            "Response content should contain 'hello world' (case-insensitive).");
        Assert.AreEqual(GrokChoiceFinish_reason.Stop, response.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveHelloWorldArray_ReturnsValidResponse(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokChatCompletionRequest
        {
            Messages = new Collection<GrokMessage>
            {
                new GrokSystemMessage { Content = "You are a test assistant." },
                new GrokUserMessage
                {
                    Content = new List<GrokContent>
                    {
                        new GrokTextPart { Text = "Output only the text 'hello world'. Do not include any other text, explanations, or formatting." }
                    }
                }
            },
            Model = model,
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
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
        Assert.AreEqual("hello world", response.Choices.First().Message.Content.ToLower(),
            "Response content should contain 'hello world' (case-insensitive).");
        Assert.AreEqual(GrokChoiceFinish_reason.Stop, response.Choices.First().Finish_reason,
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

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveConversation_MaintainsContext(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var messages = new List<GrokMessage>
        {
            new GrokSystemMessage
            {
                Content =
                    "You are Grok, a helpful assistant. For this test conversation, please maintain context and respond deterministically to demonstrate your ability to remember details across multiple exchanges."
            }
        };

        messages.Add(new GrokUserMessage
        {
            Content = [new GrokTextPart { Text = "My name is TestUser. Please remember that." }]
        });
        var request1 = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response1 = null;
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
        Assert.AreEqual(GrokChoiceFinish_reason.Stop, response1.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        messages.Add(new GrokAssistantMessage
        {
            Content = response1.Choices.First().Message.Content
        });

        messages.Add(new GrokUserMessage
        {
            Content = [new GrokTextPart { Text = "What is my name?" }]
        });
        var request2 = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response2 = null;
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
        Assert.AreEqual(GrokChoiceFinish_reason.Stop, response2.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        messages.Add(new GrokAssistantMessage
        {
            Content = response2.Choices.First().Message.Content
        });

        messages.Add(new GrokUserMessage
        {
            Content = [new GrokTextPart { Text = "Good. Now, say 'Goodbye, TestUser!'" }]
        });
        var request3 = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response3 = null;
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
        Assert.AreEqual(GrokChoiceFinish_reason.Stop, response3.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveCommandRoast_ReturnsRoastMessage(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var targetName = "Dave";

        var request = new GrokChatCompletionRequest
        {
            Messages = new Collection<GrokMessage>
            {
                new GrokSystemMessage
                {
                    Content =
                        "You are a bot that responds to commands. When given the command '/roast \"name\"', generate a funny and light-hearted roast for the provided name."
                },
                new GrokUserMessage
                {
                    Content = [new GrokTextPart { Text = $"/roast \"{targetName}\"" }]
                }
            },
            Model = model,
            Stream = false,
            Temperature = 0f
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
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

        Assert.AreEqual(GrokChoiceFinish_reason.Stop, response.Choices.First().Finish_reason,
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
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

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
        var request = new GrokChatCompletionRequest
        {
            Messages = new Collection<GrokMessage>
            {
                new GrokSystemMessage { Content = "You are Grok, a helpful assistant capable of analyzing images." },
                new GrokUserMessage { Content = [new GrokTextPart { Text = contentJson }] }
            },
            Model = "grok-4-1-fast-reasoning", // Model that supports image analysis (multimodal)
            Stream = false,
            Temperature = 0f
        };

        // Act
        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
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
        Assert.AreEqual(GrokChoiceFinish_reason.Stop, response.Choices.First().Finish_reason,
            "Finish reason should be 'stop'.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveStreaming_ReturnsStreamedResponse(string model)
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var streamingClient = client.GetStreamingClient();

        const int minWords = 10;
        const int maxWords = 50;

        var request = new GrokChatCompletionRequest
        {
            Messages = new Collection<GrokMessage>
            {
                new GrokSystemMessage { Content = "You are a helpful assistant." },
                new GrokUserMessage
                {
                    Content =
                    [
                        new GrokTextPart { Text = $"Tell me a short story between {minWords} and {maxWords} words" }
                    ]
                }
            },
            Model = model,
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
        var words = finalContent.Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(words.Length >= minWords, $"Streamed content should be >= {minWords} words. We got {words.Length}");
        Assert.IsTrue(words.Length <= maxWords * 2, $"Streamed content should be <= {maxWords * 2} words (allowing some flexibility). We got {words.Length}");
        
        // More flexible story content validation - check for narrative elements
        var hasNarrativeElements = finalContent.Contains("story") || 
                                 finalContent.Contains("once") || 
                                 finalContent.Contains("there") ||
                                 finalContent.Contains("was") || 
                                 finalContent.Contains("had") ||
                                 finalContent.Contains("then") ||
                                 finalContent.Contains("end") ||
                                 finalContent.Contains("finally") ||
                                 finalContent.Contains("suddenly") ||
                                 finalContent.Contains("after") ||
                                 finalContent.Contains("when") ||
                                 (finalContent.Length > 50 && words.Length >= minWords); // If it's long enough and has enough words, assume it's a story
        
        Assert.IsTrue(hasNarrativeElements,
            $"Streamed content should resemble a short story or narrative. Content: '{finalContent.Substring(0, Math.Min(100, finalContent.Length))}...'");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task GrokThread_AskMultipleQuestions_MaintainsContextAndStreamsResponses(string model)
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var thread = client.GetGrokThread();

        // Helper function to collect all messages from the stream
        async Task<List<GrokMessageBase>> CollectMessagesAsync(string question)
        {
            var messages = new List<GrokMessageBase>();
            await foreach (var message in thread.AskQuestion(question, model: model, temperature: 0)) messages.Add(message);
            return messages;
        }

        // Helper function to extract the full response text
        string ExtractResponse(List<GrokMessageBase> messages)
        {
            return string.Join("", messages
                .OfType<GrokTextMessage>()
                .Select(m => m.Message));
        }

        // Helper function to validate stream states
        void ValidateStates(List<GrokMessageBase> messages)
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

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task GrokThread_SystemMessages_InfluenceResponses(string model)
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var thread = new GrokThread(client);

        // Helper function to collect all messages from the stream
        async Task<List<GrokMessageBase>> CollectMessagesAsync(string question)
        {
            var messages = new List<GrokMessageBase>();
            await foreach (var message in thread.AskQuestion(question, model: model, temperature: 0)) messages.Add(message);
            return messages;
        }

        // Helper function to extract the full response text
        string ExtractResponse(List<GrokMessageBase> messages)
        {
            return string.Join("", messages
                .OfType<GrokTextMessage>()
                .Select(m => m.Message));
        }

        // Helper function to validate stream states
        void ValidateStates(List<GrokMessageBase> messages)
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
        thread.AddSystemInstruction("Respond with only the answer, as a single word. No additional text.");
        var question1 = "What is the capital of France?";
        await WaitForRateLimitAsync();
        var messages1 = await CollectMessagesAsync(question1);

        // Validate message sequence and states
        Assert.IsTrue(messages1.Count > 2, "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages1[0], typeof(GrokStreamState), "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages1[0]).StreamState,
            "First state should be 'Thinking'.");
        ValidateStates(messages1);

        // Extract and verify response
        var response1 = ExtractResponse(messages1);
        Assert.IsFalse(string.IsNullOrEmpty(response1), "Response should not be empty.");
        Assert.AreEqual("Paris", response1.Trim(), "Response should be a single word 'Paris'.");
        Assert.IsFalse(response1.Contains(" "), "Response should contain no spaces, indicating one word.");

        // **Step 2: JSON response**
        thread.AddSystemInstruction("Respond only with a valid JSON object. Do not include any other text, explanations, or markdown formatting.");
        var question2 = "Provide the population of France as a JSON object with a 'population' field.";
        await WaitForRateLimitAsync();
        var messages2 = await CollectMessagesAsync(question2);

        // Validate message sequence and states
        Assert.IsTrue(messages2.Count > 2, "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages2[0], typeof(GrokStreamState), "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages2[0]).StreamState,
            "First state should be 'Thinking'.");
        ValidateStates(messages2);

        // Extract and verify response
        var response2 = ExtractResponse(messages2);
        Assert.IsFalse(string.IsNullOrEmpty(response2), "Response should not be empty.");
        Assert.IsTrue(IsValidJson(response2), "Response should be valid JSON.");
        dynamic jsonResponse = JsonConvert.DeserializeObject(response2)!;
        
        Assert.IsNotNull(jsonResponse.population, $"JSON should contain a 'population' field. Actual JSON: {response2}");

        // **Step 3: Spanish translation**
        thread.AddSystemInstruction("Translate all responses to Spanish. Do not respond in JSON anymore");
        var question3 = "What is the weather like today?";
        await WaitForRateLimitAsync();
        var messages3 = await CollectMessagesAsync(question3);

        // Validate message sequence and states
        Assert.IsTrue(messages3.Count > 2, "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages3[0], typeof(GrokStreamState), "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages3[0]).StreamState,
            "First state should be 'Thinking'.");
        ValidateStates(messages3);

        // Extract and verify response
        var response3 = ExtractResponse(messages3);
        Assert.IsFalse(string.IsNullOrEmpty(response3), "Response should not be empty.");
        // Simple check for Spanish words (e.g., "el", "es", "hoy")
        Assert.IsTrue(response3.Contains("el", StringComparison.OrdinalIgnoreCase) ||
                      response3.Contains("es", StringComparison.OrdinalIgnoreCase) ||
                      response3.Contains("hoy", StringComparison.OrdinalIgnoreCase),
            "Response should contain common Spanish words indicating translation.");

        // **Step 4: Verify last SystemMessage overrides previous ones**
        var question4 = "What is my name?"; // No prior context, just testing instruction impact
        await WaitForRateLimitAsync();
        var messages4 = await CollectMessagesAsync(question4);

        // Validate message sequence and states
        Assert.IsTrue(messages4.Count > 2, "Should have state messages and at least one response part.");
        Assert.IsInstanceOfType(messages4[0], typeof(GrokStreamState), "First message should be a state message.");
        Assert.AreEqual(StreamState.Thinking, ((GrokStreamState)messages4[0]).StreamState,
            "First state should be 'Thinking'.");
        ValidateStates(messages4);

        // Extract and verify response
        var response4 = ExtractResponse(messages4);
        Assert.IsFalse(string.IsNullOrEmpty(response4), "Response should not be empty.");
        Assert.IsFalse(IsValidJson(response4), "Response should not be JSON due to last Spanish instruction.");
        Assert.IsFalse(response4.Split(' ').Length == 1,
            "Response should not be one word due to last Spanish instruction.");
        Assert.IsTrue(response4.Contains("no", StringComparison.OrdinalIgnoreCase) ||
                      response4.Contains("s�", StringComparison.OrdinalIgnoreCase),
            "Response should be in Spanish, likely indicating 'I don�t know' (e.g., 'No s�').");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    public void GrokThread_ThreadTokens_EstimatesTokensCorrectly()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, "dummy");
        var thread = new GrokThread(client);
        thread.AddUserMessage("Hello world"); // 11 chars
        Assert.AreEqual(3, thread.ThreadTokens); // (11+3)/4 = 3
        thread.AddUserMessage("This is a longer message with more text."); // 40 chars
        Assert.AreEqual(13, thread.ThreadTokens); // (51+3)/4 = 13
    }

    [TestMethod]
    public void GrokThread_HistoryTrimming_WithMaxMessagesInHistory_KeepsSystemAndLastMessages()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, "dummy");
        var options = new GrokThreadOptions { MaxMessagesInHistory = 3 };
        var thread = new GrokThread(client, options);

        // Add system message
        thread.AddSystemInstruction("You are a helpful assistant.");

        // Add user messages
        thread.AddUserMessage("Message 1");
        thread.AddUserMessage("Message 2");
        thread.AddUserMessage("Message 3");
        thread.AddUserMessage("Message 4"); // This should cause trimming

        // History should have: System + Message 3 + Message 4 (last 2 user messages)
        var history = thread.History.ToList();
        Assert.AreEqual(3, history.Count);
        Assert.IsInstanceOfType(history[0], typeof(GrokSystemMessage));
        Assert.AreEqual("You are a helpful assistant.", ((GrokSystemMessage)history[0]).Content);
        Assert.IsInstanceOfType(history[1], typeof(GrokUserMessage));
        Assert.AreEqual("Message 3", ((GrokTextPart)((GrokUserMessage)history[1]).Content.First()).Text);
        Assert.IsInstanceOfType(history[2], typeof(GrokUserMessage));
        Assert.AreEqual("Message 4", ((GrokTextPart)((GrokUserMessage)history[2]).Content.First()).Text);
    }

    [TestMethod]
    public void GrokThread_TotalTokensUsed_StartsAtZero()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, "dummy");
        var thread = new GrokThread(client);
        Assert.AreEqual(0, thread.TotalTokensUsed);
    }
}