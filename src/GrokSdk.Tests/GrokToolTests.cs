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
            "You are an assistant that can generate images using the 'grok_tool_generate_images' tool. " +
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


    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_LiveWebSearch_ReturnsSearchResults()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolLiveSearch(client);
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
        var response = JsonConvert.DeserializeObject<GrokToolLiveSearchResponse>(result);
        Assert.IsNotNull(response, "Response was null.");
        Assert.IsNull(response.Error, "Error should be null for a valid request.");
        Assert.IsNotNull(response.Summary, "Summary should not be null.");
        Assert.IsTrue(response.Summary.Length > 0, "Summary should not be empty.");
        Assert.IsNotNull(response.Citations, "Citations should not be null.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_LiveNewsSearchWithDateRange_ReturnsSearchResults()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolLiveSearch(client);
        var args = JsonConvert.SerializeObject(new
        {
            query = "latest news about the president",
            search_type = "news",
            from_date = "2023-01-01",
            to_date = "2023-12-31"
        });
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
        var response = JsonConvert.DeserializeObject<GrokToolLiveSearchResponse>(result);
        Assert.IsNotNull(response, "Response was null.");
        Assert.IsNull(response.Error, "Error should be null for a valid request.");
        Assert.IsNotNull(response.Summary, "Summary should not be null.");
        Assert.IsTrue(response.Summary.Length > 0, "Summary should not be empty.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_LiveXSearchWithHandles_ReturnsSearchResults()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolLiveSearch(client);
        var args = JsonConvert.SerializeObject(new
        {
            query = "latest posts",
            search_type = "x",
            x_handles = new[] { "elonmusk", "OpenAI" }
        });
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
        var response = JsonConvert.DeserializeObject<GrokToolLiveSearchResponse>(result);
        Assert.IsNotNull(response, "Response was null.");
        Assert.IsNull(response.Error, "Error should be null for a valid request.");
        Assert.IsNotNull(response.Summary, "Summary should not be null.");
        Assert.IsTrue(response.Summary.Length > 0, "Summary should not be empty.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_LiveRssSearchWithFeedUrl_ReturnsSearchResults()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolLiveSearch(client);
        var args = JsonConvert.SerializeObject(new
        {
            query = "latest space news",
            search_type = "rss",
            rss_links = new[] { "https://www.nasa.gov/news-release/feed/" }
        });
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
        var response = JsonConvert.DeserializeObject<GrokToolLiveSearchResponse>(result);
        Assert.IsNotNull(response, "Response was null.");
        Assert.IsNull(response.Error, "Error should be null for a valid request.");
        Assert.IsNotNull(response.Summary, "Summary should not be null.");
        Assert.IsTrue(response.Summary.Length > 0, "Summary should not be empty.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_InvalidSearchType_ReturnsError()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolLiveSearch(client);
        var args = JsonConvert.SerializeObject(new { query = "test", search_type = "invalid" });
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
        var response = JsonConvert.DeserializeObject<GrokToolLiveSearchResponse>(result);
        Assert.IsNotNull(response, "Response was null.");
        Assert.IsNotNull(response.Error, "Error should not be null for an invalid search_type.");
        Assert.IsTrue(response.Error.Contains("Invalid search_type"),
            "Error message should indicate invalid search_type.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_MissingQuery_ReturnsError()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolLiveSearch(client);
        var args = JsonConvert.SerializeObject(new { search_type = "web" });
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
        var response = JsonConvert.DeserializeObject<GrokToolLiveSearchResponse>(result);
        Assert.IsNotNull(response, "Response was null.");
        Assert.IsNotNull(response.Error, "Error should not be null for a missing query.");
        Assert.IsTrue(response.Error.Contains("missing query"), "Error message should indicate missing query.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task ExecuteAsync_InvalidDateFormat_ReturnsError()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var tool = new GrokToolLiveSearch(client);
        var args = JsonConvert.SerializeObject(new
        {
            query = "test",
            search_type = "news",
            from_date = "invalid-date"
        });
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
        var response = JsonConvert.DeserializeObject<GrokToolLiveSearchResponse>(result);
        Assert.IsNotNull(response, "Response was null.");
        Assert.IsNotNull(response.Error, "Error should not be null for an invalid date format.");
        Assert.IsTrue(response.Error.Contains("from_date must be in YYYY-MM-DD format"),
            "Error message should indicate invalid date format.");
    }

    [TestMethod]
    [DataRow("grok-2-latest")]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [TestCategory("Live")]
    public async Task GrokThread_WithLiveSearchTool_PerformsWebSearch(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var thread = new GrokThread(client);
        thread.RegisterTool(new GrokToolLiveSearch(client, model));

        thread.AddSystemInstruction(
            "You are an assistant that can perform live searches using the 'grok_tool_live_search' tool. " +
            "When the user asks for information that requires a search, use the tool with appropriate parameters."
        );

        var userMessage = "Tell me the latest news about the president today.";

        await WaitForRateLimitAsync();

        var toolCalled = false;
        string? toolResponseJson = null;

        await foreach (var message in thread.AskQuestion(userMessage, model: model))
        {
            if (message is GrokToolResponse { ToolName: GrokToolLiveSearch.ToolName } toolResponse1)
            {
                toolCalled = true;
                toolResponseJson = toolResponse1.ToolResponse;
                break; // Assuming only one tool call for simplicity
            }
        }

        Assert.IsTrue(toolCalled, $"The '{GrokToolLiveSearch.ToolName}' tool was not called.");
        Assert.IsNotNull(toolResponseJson, "Tool response JSON was null.");

        var toolResponse = JsonConvert.DeserializeObject<GrokToolLiveSearchResponse>(toolResponseJson);
        Assert.IsNotNull(toolResponse, "Tool response was null.");
        Assert.IsNull(toolResponse.Error, "Tool returned an error.");
        Assert.IsNotNull(toolResponse.Summary, "Summary is missing.");
        Assert.IsTrue(toolResponse.Summary.Length > 0, "Summary is empty.");

        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-2-vision-latest")]
    [TestCategory("Live")]
    public async Task ImageUnderstandingCatImage_WhatAnimal_LowDetail(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var tool = new GrokToolImageUnderstanding(client, model);
        var args = JsonConvert.SerializeObject(new
        {
            prompt = "What animal is in this image?",
            image_url = "https://i.postimg.cc/tZtF8qxL/1.jpg",
            image_detail = "low"
        });

        await WaitForRateLimitAsync();
        string result = await ExecuteToolAsync(tool, args);
        var response = JsonConvert.DeserializeObject<GrokToolImageUnderstandingResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNull(response.Error, "Error should be null for a valid request.");
        Assert.IsNotNull(response.Description, "Description should not be null.");
        Assert.IsTrue(response.Description.ToLower().Contains("cat"), "Description should contain 'cat'.");
    }

    [DataTestMethod]
    [DataRow("grok-2-vision-latest")]
    [TestCategory("Live")]
    public async Task ImageUnderstandingCatImage_WhatColor_HighDetail(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var tool = new GrokToolImageUnderstanding(client, model);
        var args = JsonConvert.SerializeObject(new
        {
            prompt = "What color is the cat?",
            image_url = "https://i.postimg.cc/tZtF8qxL/1.jpg",
            image_detail = "high"
        });

        await WaitForRateLimitAsync();
        string result = await ExecuteToolAsync(tool, args);
        var response = JsonConvert.DeserializeObject<GrokToolImageUnderstandingResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNull(response.Error, "Error should be null for a valid request.");
        Assert.IsNotNull(response.Description, "Description should not be null.");
        Assert.IsFalse(string.IsNullOrEmpty(response.Description), "Description should not be empty.");
    }

    [DataTestMethod]
    [DataRow("grok-2-vision-latest")]
    [TestCategory("Live")]
    public async Task ImageUnderstandingDogImage_WhatAnimal_LowDetail(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var tool = new GrokToolImageUnderstanding(client, model);
        var args = JsonConvert.SerializeObject(new
        {
            prompt = "What animal is in this image?",
            image_url = "https://i.postimg.cc/XBj9bRKX/2.jpg",
            image_detail = "low"
        });

        await WaitForRateLimitAsync();
        string result = await ExecuteToolAsync(tool, args);
        var response = JsonConvert.DeserializeObject<GrokToolImageUnderstandingResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNull(response.Error, "Error should be null for a valid request.");
        Assert.IsNotNull(response.Description, "Description should not be null.");
        Assert.IsTrue(response.Description.ToLower().Contains("dog"), "Description should contain 'dog'.");
    }

    [DataTestMethod]
    [DataRow("grok-2-vision-latest")]
    [TestCategory("Live")]
    public async Task ImageUnderstandingDogImage_WhatBreed_HighDetail(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var tool = new GrokToolImageUnderstanding(client, model);
        var args = JsonConvert.SerializeObject(new
        {
            prompt = "What breed is the dog?",
            image_url = "https://i.postimg.cc/XBj9bRKX/2.jpg",
            image_detail = "high"
        });

        await WaitForRateLimitAsync();
        string result = await ExecuteToolAsync(tool, args);
        var response = JsonConvert.DeserializeObject<GrokToolImageUnderstandingResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNull(response.Error, "Error should be null for a valid request.");
        Assert.IsNotNull(response.Description, "Description should not be null.");
        Assert.IsFalse(string.IsNullOrEmpty(response.Description), "Description should not be empty.");
    }

    [DataTestMethod]
    [DataRow("grok-2-vision-latest")]
    [TestCategory("Live")]
    public async Task ImageUnderstandingMissingPrompt(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var tool = new GrokToolImageUnderstanding(client, model);
        var args = JsonConvert.SerializeObject(new
        {
            image_url = "https://i.postimg.cc/tZtF8qxL/1.jpg"
        });

        await WaitForRateLimitAsync();
        string result = await ExecuteToolAsync(tool, args);
        var response = JsonConvert.DeserializeObject<GrokToolImageUnderstandingResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Error, "Error should not be null for missing prompt.");
        Assert.IsTrue(response.Error.Contains("Prompt and image_url are required."), "Error should indicate missing prompt.");
    }

    [DataTestMethod]
    [DataRow("grok-2-vision-latest")]
    [TestCategory("Live")]
    public async Task ImageUnderstandingMissingImageUrl(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var tool = new GrokToolImageUnderstanding(client, model);
        var args = JsonConvert.SerializeObject(new
        {
            prompt = "What is in this image?"
        });

        await WaitForRateLimitAsync();
        string result = await ExecuteToolAsync(tool, args);
        var response = JsonConvert.DeserializeObject<GrokToolImageUnderstandingResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Error, "Error should not be null for missing image_url.");
        Assert.IsTrue(response.Error.Contains("Prompt and image_url are required."), "Error should indicate missing image_url.");
    }

    [DataTestMethod]
    [DataRow("grok-2-vision-latest")]
    [TestCategory("Live")]
    public async Task ImageUnderstandingInvalidImageDetail(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));
        var tool = new GrokToolImageUnderstanding(client, model);
        var args = JsonConvert.SerializeObject(new
        {
            prompt = "What animal is in this image?",
            image_url = "https://i.postimg.cc/tZtF8qxL/1.jpg",
            image_detail = "medium"
        });

        await WaitForRateLimitAsync();
        string result = await ExecuteToolAsync(tool, args);
        var response = JsonConvert.DeserializeObject<GrokToolImageUnderstandingResponse>(result);

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Error, "Error should not be null for invalid image_detail.");
        Assert.IsTrue(response.Error.Contains("image_detail must be 'low' or 'high'."), "Error should indicate invalid image_detail.");
    }

    private async Task<string> ExecuteToolAsync(GrokToolImageUnderstanding tool, string args)
    {
        try
        {
            return await tool.ExecuteAsync(args);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            return null; // Unreachable due to Assert.Fail, but required for return type
        }
    }
}