using GrokSdk.Tools;
using Newtonsoft.Json;

namespace GrokSdk.Tests;

[TestClass]
public class GrokToolTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_LiveImageGeneration_ReturnsImageData()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        // Test with 'url' response format
        var toolUrl = new GrokToolImageGeneration(client);
        var argsUrl = JsonConvert.SerializeObject(new
            { prompt = "A serene mountain landscape", n = 1, response_format = "url" });
        await WaitForRateLimitAsync();
        string? resultUrl = null;
        try
        {
            resultUrl = await toolUrl.ExecuteAsync(argsUrl);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(resultUrl, "Response should not be null.");
        var responseUrl = JsonConvert.DeserializeObject<GrokToolImageGenerationResponse>(resultUrl);
        Assert.IsNotNull(responseUrl, "responseUrl is null");
        Assert.IsNull(responseUrl.Error, "Error should be null.");
        Assert.IsNotNull(responseUrl.Images, "Images should not be null.");
        Assert.AreEqual(1, responseUrl.Images.Count, "Expected 1 image.");
        Assert.IsNotNull(responseUrl.Images[0].Url, "Url returned was null!");
        Assert.IsTrue(Uri.IsWellFormedUriString(responseUrl.Images[0].Url, UriKind.Absolute),
            "Image URL should be valid.");

        // Test with 'base64' response format
        var toolBase64 = new GrokToolImageGeneration(client);
        var argsBase64 =
            JsonConvert.SerializeObject(new { prompt = "A futuristic robot", n = 1, response_format = "base64" });
        await WaitForRateLimitAsync();
        string? resultBase64 = null;
        try
        {
            resultBase64 = await toolBase64.ExecuteAsync(argsBase64);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(resultBase64, "Response should not be null.");
        var responseBase64 = JsonConvert.DeserializeObject<GrokToolImageGenerationResponse>(resultBase64);
        Assert.IsNotNull(responseBase64, "responseBase64 is null");
        Assert.IsNull(responseBase64.Error, "Error should be null.");
        Assert.IsNotNull(responseBase64.Images, "Images should not be null.");
        Assert.AreEqual(1, responseBase64.Images.Count, "Expected 1 image.");
        Assert.IsNotNull(responseBase64.Images[0].B64Json, "Base64 image data should not be null.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task GrokThread_WithImageTool_GeneratesTwoImageUrls()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var thread = new GrokThread(client);
        thread.RegisterTool(new GrokToolImageGeneration(client));

        thread.AddSystemInstruction(
            "You are an assistant that can generate images using the 'generate_image' tool. " +
            "When the user asks to generate images, use the tool with the specified prompt and number of images, " +
            "and include the image URLs in your response."
        );

        var userMessage =
            "Use the generate_image tool to generate two images of a sunset with response_format 'url' and return their URLs.";

        // Act
        await WaitForRateLimitAsync();

        var toolCalled = false;
        string? toolResponseJson = null;

        await foreach (var message in thread.AskQuestion(userMessage))
            if (message is GrokToolResponse { ToolName: GrokToolImageGeneration.ToolName } toolResponse1)
            {
                toolCalled = true;
                toolResponseJson = toolResponse1.ToolResponse;
                break; // Assuming only one tool call for simplicity
            }

        Assert.IsNotNull(toolResponseJson, "toolResponseJson was null");
        Assert.IsTrue(toolCalled, $"The '{GrokToolImageGeneration.ToolName}' tool was not called.");

        var toolResponse = GrokToolImageGenerationResponse.DeserializeResponse(toolResponseJson);

        Assert.IsNotNull(toolResponse, "toolResponse was null");
        
        if (toolResponse.Error != null) Assert.Fail($"Tool returned an error: {toolResponse.Error}");

        var images = toolResponse.Images;
        Assert.IsNotNull(images, "Images array is missing.");
        Assert.AreEqual(2, images.Count, "Expected two images.");

        foreach (var image in images)
        {
            Assert.IsNotNull(image.Url, "Image URL is missing.");
            Assert.IsTrue(Uri.IsWellFormedUriString(image.Url, UriKind.Absolute), "Image URL is invalid.");
        }

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_LiveReasoning_ReturnsReasoningResult()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        // Test with effort "low"
        var tool = new GrokToolReasoning(client);
        var argsLow = JsonConvert.SerializeObject(new { problem = "Why is the sky blue?", effort = "low" });
        await WaitForRateLimitAsync();
        string? resultLow = null;
        try
        {
            resultLow = await tool.ExecuteAsync(argsLow);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(resultLow, "Response should not be null.");
        var responseLow = JsonConvert.DeserializeObject<GrokToolReasoningResponse>(resultLow);
        Assert.IsNotNull(responseLow, "responseLow was null");
        Assert.IsNull(responseLow.Error, "Error should be null for valid request.");
        Assert.IsNotNull(responseLow.Reasoning, "Reasoning should not be null.");
        Assert.IsTrue(responseLow.Reasoning.Length > 0, "Reasoning should not be empty.");

        // Test with effort "high"
        var argsHigh =
            JsonConvert.SerializeObject(new { problem = "Explain quantum mechanics briefly.", effort = "high" });
        await WaitForRateLimitAsync();
        string? resultHigh = null;
        try
        {
            resultHigh = await tool.ExecuteAsync(argsHigh);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(resultHigh, "Response should not be null.");
        var responseHigh = JsonConvert.DeserializeObject<GrokToolReasoningResponse>(resultHigh);
        Assert.IsNotNull(responseHigh, "responseHigh was null");
        Assert.IsNull(responseHigh.Error, "Error should be null for valid request.");
        Assert.IsNotNull(responseHigh.Reasoning, "Reasoning should not be null.");
        Assert.IsTrue(responseHigh.Reasoning.Length > 0, "Reasoning should not be empty.");

        // Test with invalid effort
        var argsInvalidEffort = JsonConvert.SerializeObject(new { problem = "Some problem", effort = "medium" });
        await WaitForRateLimitAsync();
        string? resultInvalidEffort = null;
        try
        {
            resultInvalidEffort = await tool.ExecuteAsync(argsInvalidEffort);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(resultInvalidEffort, "Response should not be null.");
        var responseInvalidEffort = JsonConvert.DeserializeObject<GrokToolReasoningResponse>(resultInvalidEffort);
        Assert.IsNotNull(responseInvalidEffort, "responseInvalidEffort was null");
        Assert.IsNotNull(responseInvalidEffort.Error, "Error should not be null for invalid effort.");
        Assert.AreEqual("Invalid effort level. Must be 'low' or 'high'.", responseInvalidEffort.Error);

        // Test with missing problem
        var argsMissingProblem = JsonConvert.SerializeObject(new { effort = "low" });
        await WaitForRateLimitAsync();
        string? resultMissingProblem = null;
        try
        {
            resultMissingProblem = await tool.ExecuteAsync(argsMissingProblem);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(resultMissingProblem, "Response should not be null.");
        var responseMissingProblem = JsonConvert.DeserializeObject<GrokToolReasoningResponse>(resultMissingProblem);
        Assert.IsNotNull(responseMissingProblem, "responseMissingProblem was null");
        Assert.IsNotNull(responseMissingProblem.Error, "Error should not be null for missing problem.");
        Assert.AreEqual("Problem cannot be empty.", responseMissingProblem.Error);

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }
}