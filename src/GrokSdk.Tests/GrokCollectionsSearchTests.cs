using GrokSdk;
using GrokSdk.Tools;

namespace GrokSdk.Tests;

[TestClass]
public class GrokCollectionsSearchTests : GrokClientTestBaseClass
{
    private static GrokClient _client = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
        var httpClient = new HttpClient();
        _client = new GrokClient(httpClient, ApiToken);
    }

    [TestMethod]
    public void CollectionsSearchTool_Constructor_ValidatesVectorStoreIds()
    {
        // Empty list should throw
        Assert.ThrowsException<ArgumentException>(() =>
            new GrokToolCollectionsSearch(_client, new List<string>()));
    }

    [TestMethod]
    public void CollectionsSearchTool_Constructor_ValidatesSingleId()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new GrokToolCollectionsSearch(_client, string.Empty));
    }

    [TestMethod]
    public void CollectionsSearchTool_HasCorrectNameAndDescription()
    {
        var tool = new GrokToolCollectionsSearch(_client, "test-id-123");
        Assert.AreEqual(GrokToolCollectionsSearch.ToolName, tool.Name);
        Assert.IsFalse(string.IsNullOrEmpty(tool.Description));
    }

    [TestMethod]
    public void CollectionsSearchTool_OptionsCanBeSet()
    {
        var tool = new GrokToolCollectionsSearch(_client, "test-id-123");
        tool.Options = new GrokResponsesToolOptions
        {
            MaxOutputTokens = 500,
            MaxTurns = 3,
            PreviousResponseId = "resp_123",
            Include = new List<string> { "inline_citations" },
            Store = true
        };

        Assert.IsNotNull(tool.Options);
        Assert.AreEqual(500, tool.Options.MaxOutputTokens);
        Assert.AreEqual(3, tool.Options.MaxTurns);
        Assert.AreEqual("resp_123", tool.Options.PreviousResponseId);
        Assert.IsTrue(tool.Options.Store);
    }

    [TestMethod]
    public async Task CollectionsSearchTool_ExecuteWithInvalidArgs_ReturnsError()
    {
        var tool = new GrokToolCollectionsSearch(_client, "nonexistent-collection-id");
        var result = await tool.ExecuteAsync("{}");
        Assert.IsTrue(result.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    // Note: Live collections search tests require actual collection IDs created via the xAI console.
    // These tests validate the tool wrapper structure and error handling without needing real collections.
}
