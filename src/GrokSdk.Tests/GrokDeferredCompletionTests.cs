namespace GrokSdk.Tests;

[TestClass]
public class GrokDeferredCompletionTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task SubmitAsync_ReturnsRequestId()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var deferred = new GrokDeferredCompletion(client);

        var request = new GrokChatCompletionRequest
        {
            Messages = new List<GrokMessage>
            {
                new GrokUserMessage
                {
                    Content = new System.Collections.ObjectModel.Collection<GrokContent>
                    {
                        new GrokTextPart { Text = "What is it like in outer space?" }
                    }
                }
            },
            Model = "grok-3-mini-fast",
            Stream = false
        };

        await WaitForRateLimitAsync();

        string? requestId = null;
        try
        {
            requestId = await deferred.SubmitAsync(request);
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
    public async Task AskDeferredAsync_ReturnsTextResponse()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var deferred = new GrokDeferredCompletion(client,
            timeout: TimeSpan.FromMinutes(2),
            pollInterval: TimeSpan.FromSeconds(5));

        await WaitForRateLimitAsync();

        string? result = null;
        try
        {
            result = await deferred.AskDeferredAsync(
                "What is the speed of light in meters per second?",
                model: "grok-3-mini-fast");
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }
        catch (TimeoutException)
        {
            Assert.Inconclusive("Deferred completion timed out — may be expected under load.");
        }

        Assert.IsNotNull(result, "Result should not be null.");
        Assert.IsTrue(result.Contains("299", StringComparison.OrdinalIgnoreCase),
            "Response should mention 299 (speed of light ~299,792,458 m/s).");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task CreateAndWaitAsync_ReturnsCompletionResponse()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var deferred = new GrokDeferredCompletion(client,
            timeout: TimeSpan.FromMinutes(2),
            pollInterval: TimeSpan.FromSeconds(5));

        var request = new GrokChatCompletionRequest
        {
            Messages = new List<GrokMessage>
            {
                new GrokUserMessage
                {
                    Content = new System.Collections.ObjectModel.Collection<GrokContent>
                    {
                        new GrokTextPart { Text = "What is 42 * 2?" }
                    }
                }
            },
            Model = "grok-3-mini-fast",
            Stream = false
        };

        await WaitForRateLimitAsync();

        GrokChatCompletionResponse? response = null;
        try
        {
            response = await deferred.CreateAndWaitAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }
        catch (TimeoutException)
        {
            Assert.Inconclusive("Deferred completion timed out.");
        }

        Assert.IsNotNull(response, "Response should not be null.");
        Assert.IsNotNull(response.Choices, "Choices should not be null.");
        Assert.IsTrue(response.Choices.Count > 0, "Should have at least one choice.");
        Assert.IsNotNull(response.Choices.First().Message?.Content, "Message content should not be null.");
        Assert.IsTrue(response.Choices.First().Message.Content.Contains("84"),
            "Response should contain the answer 84.");

        await WaitForRateLimitAsync();
    }
}
