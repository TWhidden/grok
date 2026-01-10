using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GrokSdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GrokSdk.Tests;

[TestClass]
public class GrokSearchFeaturesTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task TestLiveSearch_ModeOn(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var messages = new Collection<GrokMessage>
        {
            new GrokUserMessage
            {
                Content =
                [
                    new GrokTextPart { Text = "What is the latest news about space exploration?" }
                ]
            }
        };

        var searchParams = new GrokSearchParameters
        {
            Mode = GrokSearchParametersMode.On,
            Return_citations = true
        };

        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Search_parameters = searchParams
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
        Assert.IsNotNull(response.Citations, "Citations should be present.");
        Assert.IsTrue(response.Citations.Count > 0, "Citations list should not be empty.");

        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task TestLiveSearch_ModeOff(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var messages = new Collection<GrokMessage>
        {
            new GrokUserMessage
            {
                Content =
                [
                    new GrokTextPart { Text = "What is the latest news about space exploration?" }
                ]
            }
        };

        var searchParams = new GrokSearchParameters
        {
            Mode = GrokSearchParametersMode.Off,
            Return_citations = true
        };

        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Search_parameters = searchParams
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
        Assert.IsTrue(response.Citations == null || response.Citations.Count == 0, "Citations should be absent or empty when mode is off.");

        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-4-latest")]
    [DataRow("grok-4-fast")]
    [TestCategory("Live")]
    public async Task TestLiveSearch_XSource(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var messages = new Collection<GrokMessage>
        {
            new GrokUserMessage
            {
                Content =
                [
                    new GrokTextPart { Text = "What are people saying about space exploration on X recently?" }
                ]
            }
        };

        var searchParams = new GrokSearchParameters
        {
            Mode = GrokSearchParametersMode.On,
            Return_citations = true,
            Sources = new List<GrokSource>
            {
                new GrokXSource { X_handles = new List<string> { "NASA" } }
            }
        };

        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Search_parameters = searchParams
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
        Assert.IsNotNull(response.Citations, "Citations should be present.");
        Assert.IsTrue(response.Citations.Count > 0, "Citations list should not be empty.");

        await WaitForRateLimitAsync();
    }
}