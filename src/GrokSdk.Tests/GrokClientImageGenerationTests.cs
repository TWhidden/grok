namespace GrokSdk.Tests;

[TestClass]
public class GrokClientImageGenerationTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task GenerateImagesAsync_LiveSimplePrompt_ReturnsValidImageUrl()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokImageGenerationRequest
        {
            Prompt = "A futuristic cityscape at sunset.",
            N = 1,
            Model = "grok-2-image-1212"
        };

        // Act
        await WaitForRateLimitAsync();
        GrokImageGenerationResponse? response = null;
        try
        {
            response = await client.GenerateImagesAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        // Assert
        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Data, "Data collection should not be null.");
        Assert.AreEqual(1, response.Data.Count, "Should return exactly one image.");
        var image = response.Data.First();
        Assert.IsFalse(string.IsNullOrEmpty(image.Url), "Image URL should not be null or empty.");
        Assert.IsTrue(image.Url.StartsWith("https://"), "Image URL should start with 'https://'.");
        Console.WriteLine(image.Url);

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task GenerateImagesAsync_LiveSimplePrompt_ReturnsValidBase64NoUrl()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var request = new GrokImageGenerationRequest
        {
            Prompt = "A futuristic cityscape at sunset.",
            N = 1,
            Model = "grok-2-image-1212",
            Response_format = GrokImageGenerationRequestResponse_format.B64_json // Request base64 format
        };

        // Act
        await WaitForRateLimitAsync();
        GrokImageGenerationResponse? response = null;
        try
        {
            response = await client.GenerateImagesAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        // Assert
        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Data, "Data collection should not be null.");
        Assert.AreEqual(1, response.Data.Count, "Should return exactly one image.");
        var image = response.Data.First();
        Assert.IsFalse(string.IsNullOrEmpty(image.B64_json), "Base64 image data should not be null or empty.");
        Assert.IsTrue(IsValidBase64(image.B64_json), "Base64 image data should be a valid base64 string.");
        Assert.IsTrue(string.IsNullOrEmpty(image.Url), "URL should be null or empty for base64 response.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    // Helper method to check if a string is valid base64
    private bool IsValidBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64))
            return false;

        // Check if the string is a valid base64 format
        try
        {
            Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}