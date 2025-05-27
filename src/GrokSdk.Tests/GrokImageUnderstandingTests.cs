using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GrokSdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Net.Mime.MediaTypeNames;

namespace GrokSdk.Tests;

[TestClass]
public class GrokImageUnderstandingTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task TestImageUnderstanding_WithBase64()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        // Use a base64-encoded image (small red square for this example)
        var imageData = GetResourceBytes("Resources.Image1.png");
        var base64Image = Convert.ToBase64String(imageData);
        var dataUri = $"data:image/png;base64,{base64Image}";

        // Create a message with the base64 image and a prompt
        var messages = new Collection<GrokMessage>
        {
            new GrokUserMessage
            {
                Content =
                [
                    new GrokImageUrlPart
                    {
                        Image_url = new GrokImageUrl() { Url = dataUri, Detail = GrokImageUrlDetail.High }
                    },
                    new GrokTextPart { Text = "What is in this image?" }
                ]
            }
        };

        // Set up the request
        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-vision-latest",
            Stream = false
        };

        // Send the request
        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        // Verify the response
        Assert.IsNotNull(response, "Response should not be null.");
        var choice = response.Choices.First();
        Assert.IsNotNull(choice.Message.Content, "Message content should not be null.");
        // Check for expected description (e.g., "red square")
        StringAssert.Contains(choice.Message.Content.ToLower(), "cadillac", "Response should mention Cadillac.");
        StringAssert.Contains(choice.Message.Content.ToLower(), "cts-v", "Response should mention a CTS-V.");

        // Confirm image tokens were used
        Assert.IsTrue(response.Usage.Prompt_tokens_details.Image_tokens > 0, "Image tokens should be used.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task TestImageUnderstanding_WithUrl()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var imageUrl = "https://i.postimg.cc/tZtF8qxL/1.jpg";
        var messages = new Collection<GrokMessage>
        {
            new GrokUserMessage
            {
                Content =
                [
                    new GrokImageUrlPart
                    {
                        Image_url = new GrokImageUrl() { Url = imageUrl, Detail = GrokImageUrlDetail.High }
                    },
                    new GrokTextPart { Text = "What animal is in this image?" }
                ]
            }
        };

        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-vision-latest",
            Stream = false
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        var choice = response.Choices.First();
        Assert.IsNotNull(choice.Message.Content, "Message content should not be null.");
        StringAssert.Contains(choice.Message.Content.ToLower(), "cat", "Response should mention a cat.");
        Assert.IsTrue(response.Usage.Prompt_tokens_details.Image_tokens > 0, "Image tokens should be used.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task TestImageUnderstanding_MultipleImages()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var imageUrl1 = "https://i.postimg.cc/tZtF8qxL/1.jpg";
        var imageUrl2 = "https://i.postimg.cc/XBj9bRKX/2.jpg";
        var messages = new Collection<GrokMessage>
        {
            new GrokUserMessage
            {
                Content =
                [
                    new GrokImageUrlPart
                    {
                        Image_url = new GrokImageUrl() { Url = imageUrl1, Detail = GrokImageUrlDetail.High }
                    },
                    new GrokImageUrlPart
                    {
                        Image_url = new GrokImageUrl() { Url = imageUrl2, Detail = GrokImageUrlDetail.High }
                    },
                    new GrokTextPart { Text = "What animals are in these images?" }
                ]
            }
        };

        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = "grok-2-vision-latest",
            Stream = false
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        var choice = response.Choices.First();
        Assert.IsNotNull(choice.Message.Content, "Message content should not be null.");
        var contentLower = choice.Message.Content.ToLower();
        StringAssert.Contains(contentLower, "cat", "Response should mention a cat.");
        StringAssert.Contains(contentLower, "dog", "Response should mention a dog.");
        Assert.IsTrue(response.Usage.Prompt_tokens_details.Image_tokens > 0, "Image tokens should be used.");

        await WaitForRateLimitAsync();
    }

    
}