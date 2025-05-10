using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;

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
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveReasoningEffort_ComparesLowAndHigh()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var prompt =
            "A car travels 60 miles per hour for 2 hours, then 30 miles per hour for 1 hour. What is the average speed for the entire trip?";
        var model = "grok-3-mini-beta"; // Reasoning-capable model

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

        // Assert that both responses contain the correct answer
        Assert.IsTrue(contentLow.Contains("50"), "Low effort response should contain '50'.");
        Assert.IsTrue(contentHigh.Contains("50"), "High effort response should contain '50'.");

        // Assert that high effort uses more reasoning tokens
        Assert.IsTrue(reasoningTokensHigh > reasoningTokensLow,
            "High effort should use more reasoning tokens than low effort.");

        // Check that reasoning_content is present for both
        Assert.IsFalse(string.IsNullOrEmpty(reasoningContentLow),
            "Reasoning content for low effort should not be empty.");
        Assert.IsFalse(string.IsNullOrEmpty(reasoningContentHigh),
            "Reasoning content for high effort should not be empty.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }
}