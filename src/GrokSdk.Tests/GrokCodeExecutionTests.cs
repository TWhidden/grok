using GrokSdk.Tools;
using Newtonsoft.Json;

namespace GrokSdk.Tests;

[TestClass]
public class GrokCodeExecutionTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CodeExecution_MathCalculation_ReturnsResult()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolCodeExecution(client);
        var args = JsonConvert.SerializeObject(new { query = "Calculate the factorial of 20 using Python code." });

        await WaitForRateLimitAsync();
        string? result = null;
        try
        {
            result = await tool.ExecuteAsync(args);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"Code execution API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(result, "Response should not be null.");
        var response = JsonConvert.DeserializeObject<GrokToolCodeExecutionResponse>(result);
        Assert.IsNotNull(response, "Deserialized response should not be null.");
        Assert.IsNull(response.Error, $"Error should be null but was: {response.Error}");
        Assert.IsFalse(string.IsNullOrEmpty(response.Text), "Response text should not be empty.");
        Assert.AreEqual("completed", response.Status, "Status should be 'completed'.");
        // Factorial of 20 is 2432902008176640000 (may be formatted with commas)
        var responseTextNoCommas = response.Text.Replace(",", "");
        Assert.IsTrue(responseTextNoCommas.Contains("2432902008176640000"), 
            $"Response should contain factorial of 20 (2432902008176640000). Got: {response.Text}");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CodeExecution_QueryAsync_DirectCall_ReturnsResult()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolCodeExecution(client);

        await WaitForRateLimitAsync();
        GrokToolCodeExecutionResponse? response = null;
        try
        {
            response = await tool.QueryAsync("What is 2 to the power of 100? Calculate it precisely.");
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"Code execution QueryAsync failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNull(response.Error, $"Error should be null but was: {response.Error}");
        Assert.IsFalse(string.IsNullOrEmpty(response.Text), "Response text should not be empty.");
        // 2^100 = 1267650600228229401496703205376 (may be formatted with commas)
        var normalizedText = response.Text.Replace(",", "");
        Assert.IsTrue(normalizedText.Contains("1267650600228229401496703205376"),
            $"Response should contain 2^100. Got: {response.Text}");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CodeExecution_MissingQuery_ReturnsError()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolCodeExecution(client);
        var args = JsonConvert.SerializeObject(new { });

        await WaitForRateLimitAsync();
        var result = await tool.ExecuteAsync(args);
        var response = JsonConvert.DeserializeObject<GrokToolCodeExecutionResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Error, "Error should not be null for missing query.");
        Assert.IsTrue(response.Error.Contains("Invalid or missing query"), "Error should indicate missing query.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    public void CodeExecution_NullClient_ThrowsException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new GrokToolCodeExecution(null!));
    }

    [TestMethod]
    public void CodeExecution_ToolProperties_AreCorrect()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, "test-key");
        var tool = new GrokToolCodeExecution(client);

        Assert.AreEqual(GrokToolCodeExecution.ToolName, tool.Name);
        Assert.AreEqual("grok_tool_code_execution", tool.Name);
        Assert.IsFalse(string.IsNullOrEmpty(tool.Description));
        Assert.IsNotNull(tool.Parameters);
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CodeExecution_WithGrokThread_EndToEnd()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var thread = new GrokThread(client);
        thread.RegisterTool(new GrokToolCodeExecution(client));

        thread.AddSystemInstruction(
            "You are a helpful assistant with access to the 'grok_tool_code_execution' tool for running code. " +
            "When the user asks for a calculation, use this tool to compute the answer precisely.");

        await WaitForRateLimitAsync();

        var toolCalled = false;
        string? responseText = null;

        await foreach (var message in thread.AskQuestion("Use the grok_tool_code_execution tool to calculate the sum of the first 100 prime numbers."))
        {
            if (message is GrokToolResponse { ToolName: GrokToolCodeExecution.ToolName })
            {
                toolCalled = true;
            }

            if (message is GrokTextMessage textMessage)
            {
                responseText = textMessage.Message;
            }
        }

        Assert.IsTrue(toolCalled, "The code execution tool should have been called.");
        Assert.IsFalse(string.IsNullOrEmpty(responseText), "Response text should not be empty.");

        await WaitForRateLimitAsync();
    }
}
