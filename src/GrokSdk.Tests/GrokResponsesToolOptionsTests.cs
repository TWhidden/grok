using GrokSdk;
using GrokSdk.Tools;

namespace GrokSdk.Tests;

[TestClass]
public class GrokResponsesToolOptionsTests : GrokClientTestBaseClass
{
    private static GrokClient _client = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
        var httpClient = new HttpClient();
        _client = new GrokClient(httpClient, ApiToken);
    }

    // ==========================================
    // GrokResponsesToolOptions Unit Tests
    // ==========================================

    [TestMethod]
    public void GrokResponsesToolOptions_DefaultValues()
    {
        var options = new GrokResponsesToolOptions();
        Assert.IsNull(options.PreviousResponseId);
        Assert.IsNull(options.MaxOutputTokens);
        Assert.IsNull(options.MaxTurns);
        Assert.IsFalse(options.Store);
        Assert.IsNull(options.Include);
    }

    [TestMethod]
    public void GrokResponsesToolOptions_SetAllValues()
    {
        var options = new GrokResponsesToolOptions
        {
            PreviousResponseId = "resp_abc123",
            MaxOutputTokens = 1024,
            MaxTurns = 5,
            Store = true,
            Include = new List<string> { "reasoning.encrypted_content", "inline_citations" }
        };

        Assert.AreEqual("resp_abc123", options.PreviousResponseId);
        Assert.AreEqual(1024, options.MaxOutputTokens);
        Assert.AreEqual(5, options.MaxTurns);
        Assert.IsTrue(options.Store);
        Assert.AreEqual(2, options.Include!.Count);
    }

    [TestMethod]
    public void WebSearchTool_SupportsOptions()
    {
        var tool = new GrokToolWebSearch(_client);
        Assert.IsNull(tool.Options);
        Assert.IsNull(tool.LastResponseId);
        Assert.IsNotNull(tool.LastToolUsages);
        Assert.AreEqual(0, tool.LastToolUsages.Count);

        tool.Options = new GrokResponsesToolOptions { MaxOutputTokens = 500 };
        Assert.IsNotNull(tool.Options);
    }

    [TestMethod]
    public void CodeExecutionTool_SupportsOptions()
    {
        var tool = new GrokToolCodeExecution(_client);
        Assert.IsNull(tool.Options);
        Assert.IsNull(tool.LastResponseId);
        Assert.IsNotNull(tool.LastToolUsages);

        tool.Options = new GrokResponsesToolOptions { MaxTurns = 3 };
        Assert.IsNotNull(tool.Options);
    }

    [TestMethod]
    public void McpTool_SupportsOptions()
    {
        var tool = new GrokToolMcp(_client, "https://example.com/mcp");
        Assert.IsNull(tool.Options);
        Assert.IsNull(tool.LastResponseId);
        Assert.IsNotNull(tool.LastToolUsages);

        tool.Options = new GrokResponsesToolOptions { Store = true };
        Assert.IsNotNull(tool.Options);
    }

    // ==========================================
    // GrokCitation Unit Tests
    // ==========================================

    [TestMethod]
    public void GrokCitation_DefaultValues()
    {
        var citation = new GrokCitation();
        Assert.AreEqual(string.Empty, citation.Type);
        Assert.AreEqual(string.Empty, citation.Url);
        Assert.IsNull(citation.Title);
        Assert.IsNull(citation.StartIndex);
        Assert.IsNull(citation.EndIndex);
    }

    [TestMethod]
    public void GrokCitation_SetValues()
    {
        var citation = new GrokCitation
        {
            Type = "url_citation",
            Url = "https://example.com",
            Title = "Example",
            StartIndex = 10,
            EndIndex = 20
        };

        Assert.AreEqual("url_citation", citation.Type);
        Assert.AreEqual("https://example.com", citation.Url);
        Assert.AreEqual("Example", citation.Title);
        Assert.AreEqual(10, citation.StartIndex);
        Assert.AreEqual(20, citation.EndIndex);
    }

    [TestMethod]
    public void GrokCitationMessage_ContainsCitations()
    {
        var citations = new List<GrokCitation>
        {
            new GrokCitation { Url = "https://a.com", Type = "url_citation" },
            new GrokCitation { Url = "https://b.com", Type = "url_citation" }
        };

        var message = new GrokCitationMessage(citations);
        Assert.AreEqual(2, message.Citations.Count);
        Assert.IsInstanceOfType(message, typeof(GrokMessageBase));
    }

    // ==========================================
    // GrokToolUsage Unit Tests
    // ==========================================

    [TestMethod]
    public void GrokToolUsage_DefaultValues()
    {
        var usage = new GrokToolUsage();
        Assert.AreEqual(string.Empty, usage.Type);
        Assert.IsNull(usage.Id);
        Assert.IsNull(usage.Name);
        Assert.IsNull(usage.Status);
        Assert.IsNull(usage.Action);
    }

    [TestMethod]
    public void GrokToolUsage_SetValues()
    {
        var usage = new GrokToolUsage
        {
            Type = "web_search_call",
            Id = "call_123",
            Name = "web_search",
            Status = "completed",
            Action = new Dictionary<string, object> { { "query", "test" } }
        };

        Assert.AreEqual("web_search_call", usage.Type);
        Assert.AreEqual("call_123", usage.Id);
        Assert.AreEqual("completed", usage.Status);
        Assert.IsNotNull(usage.Action);
    }

    // ==========================================
    // Live Tests: Tool Options With Real API
    // ==========================================

    [TestMethod]
    [TestCategory("Live")]
    public async Task WebSearchTool_WithOptions_TracksResponseIdAndToolUsage()
    {
        await WaitForRateLimitAsync();

        var tool = new GrokToolWebSearch(_client);
        tool.Options = new GrokResponsesToolOptions
        {
            Store = true, // Enable storage so we get a response ID we can reference
            MaxOutputTokens = 500
        };

        var argsJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { query = "What is the capital of France?" });
        string resultJson;
        try
        {
            resultJson = await tool.ExecuteAsync(argsJson);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            return;
        }

        var result = Newtonsoft.Json.JsonConvert.DeserializeObject<GrokToolWebSearchResponse>(resultJson);
        Assert.IsNotNull(result);
        Assert.IsNull(result!.Error, $"Search returned error: {result.Error}");
        Assert.IsFalse(string.IsNullOrEmpty(result.Text), "Expected text in web search response");
        Assert.IsNotNull(tool.LastResponseId, "Expected a response ID to be tracked");
        Assert.IsFalse(string.IsNullOrEmpty(tool.LastResponseId), "Response ID should not be empty");

        // Web search should have tool usages (web_search_call)
        Assert.IsTrue(tool.LastToolUsages.Count > 0, "Expected at least one tool usage event");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CodeExecutionTool_WithOptions_TracksResponseIdAndToolUsage()
    {
        await WaitForRateLimitAsync();

        var tool = new GrokToolCodeExecution(_client);
        tool.Options = new GrokResponsesToolOptions
        {
            Store = true,
            MaxOutputTokens = 1000
        };

        GrokToolCodeExecutionResponse result;
        try
        {
            // Use a complex prompt that forces the model to actually invoke the code interpreter
            result = await tool.QueryAsync("Write and run Python code to compute the sum of all prime numbers less than 200. Return only the numeric result.");
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            return;
        }

        Assert.IsFalse(string.IsNullOrEmpty(result.Text), "Expected text in code execution response");
        Assert.IsNotNull(tool.LastResponseId, "Expected a response ID");
        Assert.IsFalse(string.IsNullOrEmpty(tool.LastResponseId), "Response ID should not be empty");

        // Code execution should have tool usages (code_interpreter_call)
        // Note: not all prompts trigger code execution; this prompt is designed to force it
        Assert.IsTrue(tool.LastToolUsages.Count > 0, $"Expected at least one tool usage event. Response text: {result.Text}");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task WebSearchTool_ConversationContinuation_UsesPreviousResponseId()
    {
        await WaitForRateLimitAsync();

        var tool = new GrokToolWebSearch(_client);
        tool.Options = new GrokResponsesToolOptions { Store = true };

        // First request
        var args1 = Newtonsoft.Json.JsonConvert.SerializeObject(new { query = "What is React.js?" });
        string resultJson1;
        try
        {
            resultJson1 = await tool.ExecuteAsync(args1);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call 1 failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            return;
        }

        var result1 = Newtonsoft.Json.JsonConvert.DeserializeObject<GrokToolWebSearchResponse>(resultJson1);
        Assert.IsNotNull(result1);
        Assert.IsFalse(string.IsNullOrEmpty(result1!.Text));

        var firstResponseId = tool.LastResponseId;
        Assert.IsNotNull(firstResponseId);

        await WaitForRateLimitAsync();

        // Second request using previous response ID for continuation
        tool.Options = new GrokResponsesToolOptions
        {
            Store = true,
            PreviousResponseId = firstResponseId
        };

        var args2 = Newtonsoft.Json.JsonConvert.SerializeObject(new { query = "What about Vue.js?" });
        string resultJson2;
        try
        {
            resultJson2 = await tool.ExecuteAsync(args2);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call 2 failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            return;
        }

        var result2 = Newtonsoft.Json.JsonConvert.DeserializeObject<GrokToolWebSearchResponse>(resultJson2);
        Assert.IsNotNull(result2);
        Assert.IsFalse(string.IsNullOrEmpty(result2!.Text));
        Assert.IsNotNull(tool.LastResponseId);
        // The second response should have a different ID
        Assert.AreNotEqual(firstResponseId, tool.LastResponseId);
    }
}
