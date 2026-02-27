namespace GrokSdk.Tests;

[TestClass]
public class GrokDeveloperRoleTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task AddDeveloperInstruction_WithSimplePrompt_ReturnsResponse()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var thread = client.GetGrokThread();
        thread.AddDeveloperInstruction("You are a helpful assistant. Always respond in exactly one sentence.");

        await WaitForRateLimitAsync();

        string? responseText = null;
        try
        {
            await foreach (var msg in thread.AskQuestion("What is the capital of France?", model: "grok-3-mini-fast"))
            {
                if (msg is GrokTextMessage textMsg)
                    responseText = textMsg.Message;
            }
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseText, "Response should not be null.");
        Assert.IsTrue(responseText.Contains("Paris", StringComparison.OrdinalIgnoreCase),
            "Response should mention Paris.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task DeveloperInstruction_SerializesAsCorrectRole()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var thread = client.GetGrokThread();
        thread.AddDeveloperInstruction("Always respond with JSON.");

        // After setting developer instruction, verify history contains a GrokDeveloperMessage
        var history = thread.History;
        Assert.AreEqual(1, history.Count, "Should have 1 message in history (developer instruction).");
        Assert.IsInstanceOfType(history.First(), typeof(GrokDeveloperMessage),
            "First message should be a GrokDeveloperMessage.");

        // Now make a call to ensure it works end-to-end
        await WaitForRateLimitAsync();

        string? responseText = null;
        try
        {
            await foreach (var msg in thread.AskQuestion("What is 2+2?", model: "grok-3-mini-fast"))
            {
                if (msg is GrokTextMessage textMsg)
                    responseText = textMsg.Message;
            }
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseText, "Response should not be null.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task SwitchFromSystemToDeveloper_UpdatesCorrectly()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var thread = client.GetGrokThread();

        // Start with system instruction
        thread.AddSystemInstruction("You are a weather bot.");
        Assert.IsInstanceOfType(thread.History.First(), typeof(GrokSystemMessage));

        // Switch to developer instruction
        thread.AddDeveloperInstruction("You are a math tutor.");
        Assert.IsInstanceOfType(thread.History.First(), typeof(GrokDeveloperMessage));

        await WaitForRateLimitAsync();

        string? responseText = null;
        try
        {
            await foreach (var msg in thread.AskQuestion("What is 3 * 7?", model: "grok-3-mini-fast"))
            {
                if (msg is GrokTextMessage textMsg)
                    responseText = textMsg.Message;
            }
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseText, "Response should not be null.");
        Assert.IsTrue(responseText.Contains("21"), "Response should contain the answer 21.");

        await WaitForRateLimitAsync();
    }
}
