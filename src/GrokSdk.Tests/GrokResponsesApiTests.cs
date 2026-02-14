using System.Collections.ObjectModel;
using System.Net.Http;
using GrokSdk;
using GrokSdk.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace GrokSdk.Tests;

[TestClass]
public class GrokResponsesApiTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    /// <summary>
    /// Tests a basic Responses API call without any tools.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateResponse_BasicQuestion_ReturnsCompletedResponse(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokResponseRequest
        {
            Input = new Collection<GrokResponseInputMessage>
            {
                new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.User, Content = "What is 2 + 2?" }
            },
            Model = model,
            Stream = false,
            Store = false
        };

        await WaitForRateLimitAsync();
        GrokResponseResponse? response = null;
        try
        {
            response = await client.CreateResponseAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.AreEqual("response", response.Object, "Object type should be 'response'.");
        Assert.AreEqual(GrokResponseResponseStatus.Completed, response.Status, "Status should be 'completed'.");
        Assert.IsNotNull(response.Output, "Output should not be null.");
        Assert.IsTrue(response.Output.Count > 0, "Output should have at least one item.");

        // Find the message output item
        var messageItem = response.Output.FirstOrDefault(o => o.Type == GrokResponseOutputItemType.Message);
        Assert.IsNotNull(messageItem, "Should have a message output item.");
        Assert.IsNotNull(messageItem.Content, "Message content should not be null.");
        Assert.IsTrue(messageItem.Content.Count > 0, "Message should have at least one content part.");

        var textContent = messageItem.Content.First();
        Assert.IsNotNull(textContent.Text, "Text content should not be null.");
        Assert.IsTrue(textContent.Text.Contains("4"), "Response should contain the answer '4'.");

        await WaitForRateLimitAsync();
    }

    /// <summary>
    /// Tests the Responses API with web_search tool.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateResponse_WithWebSearch_ReturnsSearchResults(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokResponseRequest
        {
            Input = new Collection<GrokResponseInputMessage>
            {
                new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.User, Content = "What are the latest updates from xAI?" }
            },
            Model = model,
            Tools = new Collection<GrokResponseTool>
            {
                new GrokResponseTool { Type = GrokResponseToolType.Web_search }
            },
            Stream = false,
            Store = false
        };

        await WaitForRateLimitAsync();
        GrokResponseResponse? response = null;
        try
        {
            response = await client.CreateResponseAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.AreEqual(GrokResponseResponseStatus.Completed, response.Status, "Status should be 'completed'.");
        Assert.IsNotNull(response.Output, "Output should not be null.");
        Assert.IsTrue(response.Output.Count > 0, "Output should have at least one item.");

        // Find the message output item  
        var messageItem = response.Output.FirstOrDefault(o => o.Type == GrokResponseOutputItemType.Message);
        Assert.IsNotNull(messageItem, "Should have a message output item.");
        Assert.IsNotNull(messageItem.Content, "Message content should not be null.");
        Assert.IsTrue(messageItem.Content.Count > 0, "Message should have at least one content part.");

        var textContent = messageItem.Content.First();
        Assert.IsNotNull(textContent.Text, "Text content should not be null.");
        Assert.IsTrue(textContent.Text.Length > 50, "Response should contain substantial search results.");

        await WaitForRateLimitAsync();
    }

    /// <summary>
    /// Tests the Responses API with x_search tool.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateResponse_WithXSearch_ReturnsSearchResults(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokResponseRequest
        {
            Input = new Collection<GrokResponseInputMessage>
            {
                new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.User, Content = "What are people saying about xAI on X?" }
            },
            Model = model,
            Tools = new Collection<GrokResponseTool>
            {
                new GrokResponseTool { Type = GrokResponseToolType.X_search }
            },
            Stream = false,
            Store = false
        };

        await WaitForRateLimitAsync();
        GrokResponseResponse? response = null;
        try
        {
            response = await client.CreateResponseAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.AreEqual(GrokResponseResponseStatus.Completed, response.Status, "Status should be 'completed'.");
        Assert.IsNotNull(response.Output, "Output should not be null.");
        Assert.IsTrue(response.Output.Count > 0, "Output should have at least one item.");

        await WaitForRateLimitAsync();
    }

    /// <summary>
    /// Tests the Responses API with combined web_search and x_search tools.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateResponse_WithCombinedSearch_ReturnsResults(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokResponseRequest
        {
            Input = new Collection<GrokResponseInputMessage>
            {
                new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.User, Content = "What is the latest news about space exploration?" }
            },
            Model = model,
            Tools = new Collection<GrokResponseTool>
            {
                new GrokResponseTool { Type = GrokResponseToolType.Web_search },
                new GrokResponseTool { Type = GrokResponseToolType.X_search }
            },
            Stream = false,
            Store = false
        };

        await WaitForRateLimitAsync();
        GrokResponseResponse? response = null;
        try
        {
            response = await client.CreateResponseAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.AreEqual(GrokResponseResponseStatus.Completed, response.Status, "Status should be 'completed'.");
        Assert.IsNotNull(response.Output, "Output should not be null.");
        Assert.IsTrue(response.Output.Count > 0, "Output should have at least one item.");

        await WaitForRateLimitAsync();
    }

    /// <summary>
    /// Tests the GrokToolWebSearch wrapper with a basic web search.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task GrokToolWebSearch_BasicWebSearch_ReturnsResults(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolWebSearch(client, model);
        var args = JsonConvert.SerializeObject(new { query = "latest tech news", search_type = "web" });

        await WaitForRateLimitAsync();
        string? result = null;
        try
        {
            result = await tool.ExecuteAsync(args);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(result, "Response should not be null.");
        var response = JsonConvert.DeserializeObject<GrokToolWebSearchResponse>(result);
        Assert.IsNotNull(response, "Deserialized response was null.");
        Assert.IsNull(response.Error, $"Error should be null for a valid request. Got: {response.Error}");
        Assert.AreEqual("completed", response.Status, "Status should be 'completed'.");
        Assert.IsNotNull(response.Text, "Text should not be null.");
        Assert.IsTrue(response.Text.Length > 0, "Text should not be empty.");

        await WaitForRateLimitAsync();
    }

    /// <summary>
    /// Tests the GrokToolWebSearch wrapper with X search.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task GrokToolWebSearch_XSearch_ReturnsResults(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolWebSearch(client, model);
        var args = JsonConvert.SerializeObject(new { query = "xAI announcements", search_type = "x" });

        await WaitForRateLimitAsync();
        string? result = null;
        try
        {
            result = await tool.ExecuteAsync(args);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(result, "Response should not be null.");
        var response = JsonConvert.DeserializeObject<GrokToolWebSearchResponse>(result);
        Assert.IsNotNull(response, "Deserialized response was null.");
        Assert.IsNull(response.Error, $"Error should be null for a valid request. Got: {response.Error}");
        Assert.AreEqual("completed", response.Status, "Status should be 'completed'.");
        Assert.IsNotNull(response.Text, "Text should not be null.");
        Assert.IsTrue(response.Text.Length > 0, "Text should not be empty.");

        await WaitForRateLimitAsync();
    }

    /// <summary>
    /// Tests the GrokToolWebSearch with the GrokThread integration.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task GrokThread_WithWebSearchTool_PerformsSearch(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var thread = new GrokThread(client);
        thread.RegisterTool(new GrokToolWebSearch(client, model));

        thread.AddSystemInstruction(
            "You are an assistant that can search the web using the 'grok_tool_web_search' tool. " +
            "When the user asks for current information, use the tool with appropriate parameters."
        );

        var userMessage = "Search the web for the latest news about space exploration.";

        await WaitForRateLimitAsync();

        var toolCalled = false;
        string? toolResponseJson = null;
        string? finalResponse = null;

        await foreach (var message in thread.AskQuestion(userMessage, model: model))
        {
            if (message is GrokToolResponse { ToolName: GrokToolWebSearch.ToolName } toolResponse)
            {
                toolCalled = true;
                toolResponseJson = toolResponse.ToolResponse;
            }
            else if (message is GrokTextMessage textMessage)
            {
                finalResponse = textMessage.Message;
            }
        }

        Assert.IsTrue(toolCalled, $"The '{GrokToolWebSearch.ToolName}' tool was not called.");
        Assert.IsNotNull(toolResponseJson, "Tool response JSON was null.");

        var toolResult = JsonConvert.DeserializeObject<GrokToolWebSearchResponse>(toolResponseJson);
        Assert.IsNotNull(toolResult, "Tool response deserialization failed.");
        Assert.IsNull(toolResult.Error, $"Tool returned an error: {toolResult.Error}");
        Assert.IsNotNull(toolResult.Text, "Search text is missing.");
        Assert.IsTrue(toolResult.Text.Length > 0, "Search text is empty.");

        Assert.IsNotNull(finalResponse, "Final response from assistant was null.");

        await WaitForRateLimitAsync();
    }

    /// <summary>
    /// Tests that missing query returns an error.
    /// </summary>
    [TestMethod]
    [TestCategory("Live")]
    public async Task GrokToolWebSearch_MissingQuery_ReturnsError()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolWebSearch(client);
        var args = JsonConvert.SerializeObject(new { search_type = "web" });

        var result = await tool.ExecuteAsync(args);

        Assert.IsNotNull(result, "Response should not be null.");
        var response = JsonConvert.DeserializeObject<GrokToolWebSearchResponse>(result);
        Assert.IsNotNull(response, "Deserialized response was null.");
        Assert.IsNotNull(response.Error, "Error should not be null for a missing query.");
        Assert.IsTrue(response.Error.Contains("missing query", StringComparison.OrdinalIgnoreCase),
            $"Error message should indicate missing query. Got: {response.Error}");
    }

    /// <summary>
    /// Tests the Responses API with code_interpreter tool.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateResponse_WithCodeInterpreter_ReturnsResults(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokResponseRequest
        {
            Input = new Collection<GrokResponseInputMessage>
            {
                new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.User, Content = "Calculate the compound interest for $10,000 at 5% annually for 10 years" }
            },
            Model = model,
            Tools = new Collection<GrokResponseTool>
            {
                new GrokResponseTool { Type = GrokResponseToolType.Code_interpreter }
            },
            Stream = false,
            Store = false
        };

        await WaitForRateLimitAsync();
        GrokResponseResponse? response = null;
        try
        {
            response = await client.CreateResponseAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.AreEqual(GrokResponseResponseStatus.Completed, response.Status, "Status should be 'completed'.");
        Assert.IsNotNull(response.Output, "Output should not be null.");
        Assert.IsTrue(response.Output.Count > 0, "Output should have at least one item.");

        await WaitForRateLimitAsync();
    }

    /// <summary>
    /// Tests the Responses API with function tool calling.
    /// </summary>
    [DataTestMethod]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task CreateResponse_WithFunctionTool_ReturnsFunctionCall(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokResponseRequest
        {
            Input = new Collection<GrokResponseInputMessage>
            {
                new GrokResponseInputMessage { Role = GrokResponseInputMessageRole.User, Content = "What is the temperature in San Francisco?" }
            },
            Model = model,
            Tools = new Collection<GrokResponseTool>
            {
                new GrokResponseTool
                {
                    Type = GrokResponseToolType.Function,
                    Name = "get_temperature",
                    Description = "Get the current temperature for a location",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            location = new { type = "string", description = "City name" },
                            unit = new { type = "string", @enum = new[] { "celsius", "fahrenheit" }, @default = "fahrenheit" }
                        },
                        required = new[] { "location" }
                    }
                }
            },
            Stream = false,
            Store = false
        };

        await WaitForRateLimitAsync();
        GrokResponseResponse? response = null;
        try
        {
            response = await client.CreateResponseAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Output, "Output should not be null.");
        Assert.IsTrue(response.Output.Count > 0, "Output should have at least one item.");

        // The model should request a function call
        var functionCallItem = response.Output.FirstOrDefault(o => o.Type == GrokResponseOutputItemType.Function_call);
        Assert.IsNotNull(functionCallItem, "Should have a function_call output item.");
        Assert.AreEqual("get_temperature", functionCallItem.Name, "Function name should be 'get_temperature'.");
        Assert.IsNotNull(functionCallItem.Arguments, "Function arguments should not be null.");

        await WaitForRateLimitAsync();
    }
}
