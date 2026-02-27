using GrokSdk.Tools;
using Newtonsoft.Json;

namespace GrokSdk.Tests;

[TestClass]
public class GrokVideoGenerationTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task StartAsync_WithTextPrompt_ReturnsRequestId()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolVideoGeneration(client);

        await WaitForRateLimitAsync();

        string? requestId = null;
        try
        {
            requestId = await tool.StartAsync(new GrokToolVideoGenerationArgs { Prompt = "A cat playing with a ball of yarn" });
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(requestId, "Request ID should not be null.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(requestId), "Request ID should not be empty.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task GetStatusAsync_WithValidRequestId_ReturnsStatus()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolVideoGeneration(client);

        await WaitForRateLimitAsync();

        string? requestId = null;
        try
        {
            requestId = await tool.StartAsync(new GrokToolVideoGenerationArgs { Prompt = "A dog running through a field" });
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API start call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(requestId, "Request ID should not be null.");

        await WaitForRateLimitAsync();

        GrokToolVideoGenerationResponse? status = null;
        try
        {
            status = await tool.GetStatusAsync(requestId);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API status call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(status, "Status response should not be null.");
        Assert.IsNotNull(status.Status, "Status field should not be null.");
        // Status should be one of: pending, done, expired
        Assert.IsTrue(
            status.Status == "pending" || status.Status == "done" || status.Status == "expired",
            $"Unexpected status: {status.Status}");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task GenerateAsync_WithTextPrompt_ReturnsVideoUrl()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        // Use a longer timeout for generation since video takes time
        var tool = new GrokToolVideoGeneration(client, timeout: TimeSpan.FromMinutes(5), pollInterval: TimeSpan.FromSeconds(10));

        await WaitForRateLimitAsync();

        GrokToolVideoGenerationResponse? result = null;
        try
        {
            result = await tool.GenerateAsync(new GrokToolVideoGenerationArgs { Prompt = "A peaceful ocean wave crashing on a beach" });
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }
        catch (TimeoutException)
        {
            Assert.Inconclusive("Video generation timed out - this may be expected for slow generation.");
        }

        Assert.IsNotNull(result, "Result should not be null.");
        if (result.Status == "timeout")
            Assert.Inconclusive($"Video generation timed out: {result.Error}");
        if (result.Error != null && result.Status != "done")
            Assert.Fail($"Video generation error: {result.Error}");
        Assert.AreEqual("done", result.Status, "Status should be done.");
        Assert.IsNotNull(result.Url, "Video URL should not be null.");
        Assert.IsTrue(Uri.IsWellFormedUriString(result.Url, UriKind.Absolute), "Video URL should be valid.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_ViaIGrokTool_ReturnsJsonResponse()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolVideoGeneration(client, timeout: TimeSpan.FromMinutes(5), pollInterval: TimeSpan.FromSeconds(10));

        var args = JsonConvert.SerializeObject(new { prompt = "A butterfly flying through a garden" });

        await WaitForRateLimitAsync();

        string? resultJson = null;
        try
        {
            resultJson = await tool.ExecuteAsync(args);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }
        catch (TimeoutException)
        {
            Assert.Inconclusive("Video generation timed out.");
        }

        Assert.IsNotNull(resultJson, "Result JSON should not be null.");
        var response = JsonConvert.DeserializeObject<GrokToolVideoGenerationResponse>(resultJson);
        Assert.IsNotNull(response, "Deserialized response should not be null.");
        if (response.Status == "timeout")
            Assert.Inconclusive($"Video generation timed out: {response.Error}");
        if (response.Error != null && response.Status != "done")
            Assert.Fail($"Video generation error: {response.Error}");
        Assert.AreEqual("done", response.Status, "Status should be done.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    public void ToolProperties_AreCorrectlyConfigured()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, "dummy-key");

        var tool = new GrokToolVideoGeneration(client);

        Assert.AreEqual("grok_tool_video_generation", tool.Name, "Tool name should be grok_tool_video_generation.");
        Assert.IsNotNull(tool.Description, "Description should not be null.");
        Assert.IsNotNull(tool.Parameters, "Parameters schema should not be null.");
    }
}
