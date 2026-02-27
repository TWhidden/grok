using GrokSdk.Tools;
using Newtonsoft.Json;

namespace GrokSdk.Tests;

[TestClass]
public class GrokMcpToolTests : GrokClientTestBaseClass
{
    private const string DeepWikiMcpUrl = "https://mcp.deepwiki.com/mcp";

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task McpTool_DeepWikiQuery_ReturnsResponse()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolMcp(client, DeepWikiMcpUrl, "deepwiki");
        var args = JsonConvert.SerializeObject(new { query = "What does the microsoft/TypeScript GitHub repository do?" });

        await WaitForRateLimitAsync();
        string? result = null;
        try
        {
            result = await tool.ExecuteAsync(args);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"MCP API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(result, "Response should not be null.");
        var response = JsonConvert.DeserializeObject<GrokToolMcpResponse>(result);
        Assert.IsNotNull(response, "Deserialized response should not be null.");
        Assert.IsNull(response.Error, $"Error should be null but was: {response.Error}");
        Assert.IsFalse(string.IsNullOrEmpty(response.Text), "Response text should not be empty.");
        Assert.AreEqual("completed", response.Status, "Status should be 'completed'.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task McpTool_QueryAsync_DirectCall_ReturnsResponse()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolMcp(client, DeepWikiMcpUrl, "deepwiki");

        await WaitForRateLimitAsync();
        GrokToolMcpResponse? response = null;
        try
        {
            response = await tool.QueryAsync("What is the purpose of the dotnet/runtime GitHub repository?");
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"MCP QueryAsync failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNull(response.Error, $"Error should be null but was: {response.Error}");
        Assert.IsFalse(string.IsNullOrEmpty(response.Text), "Response text should not be empty.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task McpTool_WithServerConfig_ReturnsResponse()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var servers = new List<GrokMcpServerConfig>
        {
            new GrokMcpServerConfig
            {
                ServerUrl = DeepWikiMcpUrl,
                ServerLabel = "deepwiki",
                ServerDescription = "Search GitHub repository documentation"
            }
        };

        var tool = new GrokToolMcp(client, servers);
        var args = JsonConvert.SerializeObject(new { query = "What is the facebook/react GitHub repository about?" });

        await WaitForRateLimitAsync();
        string? result = null;
        try
        {
            result = await tool.ExecuteAsync(args);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"MCP API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(result, "Response should not be null.");
        var response = JsonConvert.DeserializeObject<GrokToolMcpResponse>(result);
        Assert.IsNotNull(response, "Deserialized response should not be null.");
        Assert.IsNull(response.Error, $"Error should be null but was: {response.Error}");
        Assert.IsFalse(string.IsNullOrEmpty(response.Text), "Response text should not be empty.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task McpTool_MissingQuery_ReturnsError()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolMcp(client, DeepWikiMcpUrl, "deepwiki");
        var args = JsonConvert.SerializeObject(new { });

        await WaitForRateLimitAsync();
        var result = await tool.ExecuteAsync(args);
        var response = JsonConvert.DeserializeObject<GrokToolMcpResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Error, "Error should not be null for missing query.");
        Assert.IsTrue(response.Error.Contains("Invalid or missing query"), "Error should indicate missing query.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    public void McpTool_NullClient_ThrowsException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new GrokToolMcp(null!, DeepWikiMcpUrl));
    }

    [TestMethod]
    public void McpTool_EmptyServerUrl_ThrowsException()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, "test-key");
        Assert.ThrowsException<ArgumentException>(() => new GrokToolMcp(client, ""));
    }

    [TestMethod]
    public void McpTool_EmptyServerList_ThrowsException()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, "test-key");
        Assert.ThrowsException<ArgumentException>(() => new GrokToolMcp(client, new List<GrokMcpServerConfig>()));
    }

    [TestMethod]
    public void McpTool_ToolProperties_AreCorrect()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, "test-key");
        var tool = new GrokToolMcp(client, DeepWikiMcpUrl);

        Assert.AreEqual(GrokToolMcp.ToolName, tool.Name);
        Assert.AreEqual("grok_tool_mcp", tool.Name);
        Assert.IsFalse(string.IsNullOrEmpty(tool.Description));
        Assert.IsNotNull(tool.Parameters);
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task McpTool_WithGrokThread_EndToEnd()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var thread = new GrokThread(client);
        thread.RegisterTool(new GrokToolMcp(client, DeepWikiMcpUrl, "deepwiki"));

        thread.AddSystemInstruction(
            "You are a helpful assistant with access to the 'grok_tool_mcp' tool that can search GitHub repository documentation. " +
            "When the user asks about a GitHub project, use this tool to look up information.");

        await WaitForRateLimitAsync();

        var toolCalled = false;
        string? responseText = null;

        await foreach (var message in thread.AskQuestion("Use the grok_tool_mcp tool to find out what the microsoft/vscode GitHub project is about."))
        {
            if (message is GrokToolResponse { ToolName: GrokToolMcp.ToolName })
            {
                toolCalled = true;
            }

            if (message is GrokTextMessage textMessage)
            {
                responseText = textMessage.Message;
            }
        }

        Assert.IsTrue(toolCalled, "The MCP tool should have been called.");
        Assert.IsFalse(string.IsNullOrEmpty(responseText), "Response text should not be empty.");

        await WaitForRateLimitAsync();
    }
}
