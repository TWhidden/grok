using System.Collections.ObjectModel;

namespace GrokSdk.Tests;

[TestClass]
public class GrokClientReasoningTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [DataRow("grok-3-mini")] // Legacy reasoning model - only model that supports reasoning_effort
    [TestCategory("Live")]
    [TestCategory("Deprecated")] // This test uses deprecated functionality
    public async Task CreateChatCompletionAsync_LiveReasoningEffort_ComparesLowAndHigh(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var prompt =
            "A complex logistics problem: A delivery company has 5 trucks with different capacities (10, 15, 20, 25, 30 tons). They need to deliver packages to 12 cities with varying distances (ranging from 50 to 500 miles) and different delivery time windows. Each truck has different fuel efficiency rates, and fuel costs vary by location. The company wants to minimize total cost while ensuring all deliveries are completed on time. Additionally, some trucks require maintenance after certain mileage, and driver work-hour regulations must be considered. Calculate the optimal delivery strategy and explain the reasoning behind each decision, including alternative approaches that were considered and why they were rejected.";

        // Helper method to get response for a given reasoning effort
        async Task<(string content, string reasoningContent, int reasoningTokens)> GetResponseAsync(
            string reasoningEffort)
        {
            var request = new GrokChatCompletionRequest
            {
                Messages = new Collection<GrokMessage>
                {
                    new GrokSystemMessage { Content = "You are a helpful assistant." },
                    new GrokUserMessage { Content = [new GrokTextPart { Text = prompt }] }
                },
                Model = model,
                Stream = false,
                Temperature = 0f,
                Reasoning_effort = reasoningEffort == "low"
                    ? GrokChatCompletionRequestReasoning_effort.Low
                    : GrokChatCompletionRequestReasoning_effort.High
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

            var choice = response.Choices.First();
            var content = choice.Message.Content;
            var reasoningContent = choice.Message.Reasoning_content;
            var reasoningTokens = response.Usage.Completion_tokens_details.Reasoning_tokens;

            return (content, reasoningContent, reasoningTokens);
        }

        // Get response for low reasoning effort
        var (contentLow, reasoningContentLow, reasoningTokensLow) = await GetResponseAsync("low");

        // Get response for high reasoning effort
        var (contentHigh, reasoningContentHigh, reasoningTokensHigh) = await GetResponseAsync("high");

        // Assert that both responses contain reasoning about optimization or strategy
        Assert.IsTrue(contentLow.Contains("cost") || contentLow.Contains("optimal") || contentLow.Contains("strategy") || contentLow.Contains("delivery"), 
            "Low effort response should contain optimization-related terms.");
        Assert.IsTrue(contentHigh.Contains("cost") || contentHigh.Contains("optimal") || contentHigh.Contains("strategy") || contentHigh.Contains("delivery"), 
            "High effort response should contain optimization-related terms.");

        // Assert that high effort uses more reasoning tokens
        Assert.IsTrue(reasoningTokensHigh > reasoningTokensLow,
            $"High effort should use more reasoning tokens than low effort. reasoningTokensHigh ({reasoningTokensHigh}) > reasoningTokensLow ({reasoningTokensLow})");

        // Check that reasoning_content is present for both
        Assert.IsFalse(string.IsNullOrEmpty(reasoningContentLow),
            "Reasoning content for low effort should not be empty.");
        Assert.IsFalse(string.IsNullOrEmpty(reasoningContentHigh),
            "Reasoning content for high effort should not be empty.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }
}