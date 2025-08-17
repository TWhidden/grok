using System.Collections.ObjectModel;
using System.Reflection;
using GrokSdk.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GrokSdk.Tests;

[TestClass]
public class GrokClientToolChoiceTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    private static string GetN2YoApiKeyFromFileOrEnv()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("N2YO_API_KEY")))
            return Environment.GetEnvironmentVariable("N2YO_API_KEY")!;

        var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                              throw new Exception("Failed to get assembly location");
        var filePath = Path.Combine(outputDirectory, "n2yokey.txt");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("API key file 'n2yokey.txt' not found in the test output directory.");

        var apiKey = File.ReadAllText(filePath).Trim();
        if (string.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("API key file 'n2yokey.txt' is empty.");

        return apiKey;
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-2-latest")]
    [DataRow("grok-4-latest")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveToolChoice_DemonstratesModes(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        // Define GrokTools similar to the Grok docs example
        var grokTools = new Collection<GrokTool>
        {
            new()
            {
                Type = GrokToolType.Function,
                Function = new GrokFunctionDefinition
                {
                    Name = "get_current_temperature",
                    Description = "Get the current temperature in a given location",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            location = new
                                { type = "string", description = "The city and state, e.g. San Francisco, CA" },
                            unit = new { type = "string", @enum = new[] { "celsius", "fahrenheit" } }
                        },
                        required = new[] { "location" }
                    }
                }
            }
        };

        // Test 1: GrokTool_choice = "auto" - Model decides whether to use the GrokTool
        var requestAuto = new GrokChatCompletionRequest
        {
            Messages = new Collection<GrokMessage>
            {
                new GrokSystemMessage { Content = "You are a weather assistant with access to GrokTools." },
                new GrokUserMessage { Content = [new GrokTextPart { Text = "What's the current temperature in Paris right now?" }] }
            },
            Model = model,
            Stream = false,
            Temperature = 0f,
            Tools = grokTools,
            Tool_choice = Tool_choice.Auto
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? responseAuto = null;
        try
        {
            responseAuto = await client.CreateChatCompletionAsync(requestAuto);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call (auto) failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseAuto, "Response (auto) should not be null.");
        Assert.IsTrue(responseAuto.Choices.Count > 0, "Response (auto) should have at least one choice.");
        var autoChoice = responseAuto.Choices.First();
        var grokToolCalledAuto = autoChoice.Message.Tool_calls?.Count > 0;
        var contentProvidedAuto = !string.IsNullOrEmpty(autoChoice.Message.Content);
        Assert.IsTrue(grokToolCalledAuto || contentProvidedAuto,
            "Response (auto) should either call a GrokTool or provide content.");
        if (grokToolCalledAuto)
        {
            Assert.AreEqual("get_current_temperature", autoChoice.Message.Tool_calls?.First().Function.Name,
                "GrokTool call (auto) should match the defined GrokTool.");
            Assert.AreEqual(GrokChoiceFinish_reason.Tool_calls, autoChoice.Finish_reason,
                "Finish reason (Tool_choice = auto) should be 'Tool_calls' when a GrokTool is used.");
        }
        else
        {
            Assert.AreEqual(GrokChoiceFinish_reason.Stop, autoChoice.Finish_reason,
                "Finish reason (auto) should be 'stop' when content is provided.");
        }

        // Test 2: GrokTool_choice = "none" - No GrokTool should be called
        var requestNone = new GrokChatCompletionRequest
        {
            Messages = new Collection<GrokMessage>
            {
                new GrokSystemMessage { Content = "You are a weather assistant with access to GrokTools." },
                new GrokUserMessage { Content = [new GrokTextPart { Text = "What's the current temperature in Paris right now?" }] }
            },
            Model = model,
            Stream = false,
            Temperature = 0f,
            Tools = grokTools,
            Tool_choice = Tool_choice.None
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? responseNone = null;
        try
        {
            responseNone = await client.CreateChatCompletionAsync(requestNone);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call (none) failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseNone, "Response (none) should not be null.");
        Assert.IsTrue(responseNone.Choices.Count > 0, "Response (none) should have at least one choice.");
        var noneChoice = responseNone.Choices.First();
        Assert.IsNull(noneChoice.Message.Tool_calls, "No GrokTools should be called with GrokTool_choice 'none'.");
        Assert.IsFalse(string.IsNullOrEmpty(noneChoice.Message.Content),
            "Response (none) should provide content since GrokTools are disabled.");
        Assert.AreEqual(GrokChoiceFinish_reason.Stop, noneChoice.Finish_reason,
            "Finish reason (none) should be 'stop'.");

        // Test 3: GrokTool_choice = "required" - Forces a GrokTool call
        var requestRequired = new GrokChatCompletionRequest
        {
            Messages = new Collection<GrokMessage>
            {
                new GrokSystemMessage { Content = "You are a weather assistant with access to GrokTools." },
                new GrokUserMessage { Content = [new GrokTextPart { Text = "What's the current temperature in Paris right now?" }] }
            },
            Model = model,
            Stream = false,
            Temperature = 0f,
            Tools = grokTools,
            Tool_choice = Tool_choice.Required
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? responseRequired = null;
        try
        {
            responseRequired = await client.CreateChatCompletionAsync(requestRequired);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail(
                $"API call (required) failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(responseRequired, "Response (required) should not be null.");
        Assert.IsTrue(responseRequired.Choices.Count > 0, "Response (required) should have at least one choice.");
        var requiredChoice = responseRequired.Choices.First();
        Assert.IsTrue(requiredChoice.Message.Tool_calls?.Count > 0,
            "Response (required) should call a GrokTool since GrokTool_choice is 'required'.");
        Assert.AreEqual("get_current_temperature", requiredChoice.Message.Tool_calls.First().Function.Name,
            "GrokTool call (required) should match the defined GrokTool.");
        Assert.AreEqual(GrokChoiceFinish_reason.Tool_calls, requiredChoice.Finish_reason,
            "Finish reason (required) should be 'GrokTool_calls'.");

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-2-latest")]
    [DataRow("grok-4-latest")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveToolChoice_ReturnsParisTemperature(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        // Define the GrokTool
        var grokTools = new Collection<GrokTool>
        {
            new()
            {
                Type = GrokToolType.Function,
                Function = new GrokFunctionDefinition
                {
                    Name = "get_current_temperature",
                    Description = "Get the current temperature in a given location",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            location = new { type = "string", description = "The city and state" },
                            unit = new
                            {
                                type = "string", @enum = new[] { "celsius", "fahrenheit" }, @default = "celsius"
                            }
                        },
                        required = new[] { "location" }
                    }
                }
            }
        };

        // Step 1: Initial request
        var messages = new Collection<GrokMessage>
        {
            new GrokSystemMessage { Content = "You are a weather assistant with access to GrokTools." },
            new GrokUserMessage { Content = [new GrokTextPart { Text = "What's the current temperature in Paris right now?" }] }
        };
        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Temperature = 0f,
            Tools = grokTools,
            Tool_choice = Tool_choice.Auto
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"Initial API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Initial response should not be null.");
        Assert.IsTrue(response.Choices.Count > 0, "Initial response should have at least one choice.");

        var choice = response.Choices.First();
        if (choice.Message.Tool_calls?.Count > 0)
        {
            // Step 2: Simulate GrokTool execution (mock or real API call)
            var grokToolCall = choice.Message.Tool_calls.First();
            Assert.AreEqual("get_current_temperature", grokToolCall.Function.Name,
                "GrokTool should be get_current_temperature.");

            var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(grokToolCall.Function.Arguments) ??
                       throw new Exception("Could not process arguments from function");
            var location = args["location"];
            Assert.IsTrue(location.ToLower().Contains("paris"), "GrokTool call should target Paris.");

            // Mock implementation (replace with real API call if desired)
            var result = location.ToLower().Contains("paris")
                ? "{\"location\": \"Paris\", \"temperature\": 15, \"unit\": \"celsius\"}"
                : "{\"location\": \"unknown\", \"temperature\": null, \"unit\": \"celsius\"}";

            // Add assistant message and GrokTool result to messages
            messages.Add(choice.Message);
            messages.Add(new GrokToolMessage
            {
                Content = result,
                Tool_call_id = grokToolCall.Id
            });

            // Step 3: Send back to Grok
            var followUpRequest = new GrokChatCompletionRequest
            {
                Messages = messages,
                Model = model,
                Stream = false,
                Temperature = 0f,
                Tools = grokTools,
                Tool_choice = Tool_choice.Auto
            };

            await WaitForRateLimitAsync();
            GrokChatCompletionResponse? finalResponse = null;
            try
            {
                finalResponse = await client.CreateChatCompletionAsync(followUpRequest);
            }
            catch (GrokSdkException ex)
            {
                Assert.Fail(
                    $"Follow-up API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            }

            Assert.IsNotNull(finalResponse, "Final response should not be null.");
            Assert.IsTrue(finalResponse.Choices.Count > 0, "Final response should have at least one choice.");
            var finalChoice = finalResponse.Choices.First();
            Assert.IsFalse(string.IsNullOrEmpty(finalChoice.Message.Content), "Final response should have content.");
            Assert.IsTrue(finalChoice.Message.Content.Contains("15") || finalChoice.Message.Content.Contains("Paris"),
                "Final response should mention Paris temperature (15°C).");
            Assert.AreEqual(GrokChoiceFinish_reason.Stop, finalChoice.Finish_reason,
                "Final finish reason should be 'stop'.");

            // Safety Check for Live Unit Tests to prevent API exhaustion
            await WaitForRateLimitAsync();
        }
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-2-latest")]
    [DataRow("grok-4-latest")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveToolChoice_FetchesInternationalSpaceStation(string model)
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        // Define a GrokTool to get satellite position
        var grokTools = new Collection<GrokTool>
        {
            new()
            {
                Type = GrokToolType.Function,
                Function = new GrokFunctionDefinition
                {
                    Name = "get_starlink_position",
                    Description = "Get the current position of a satellite by NORAD ID",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            norad_id = new { type = "integer", description = "The NORAD ID of the satellite" }
                        },
                        required = new[] { "norad_id" }
                    }
                }
            }
        };

        // Step 1: Initial request to Grok
        var messages = new Collection<GrokMessage>
        {
            new GrokSystemMessage { Content = "You are an assistant that can track satellites." },
            new GrokUserMessage
            {
                Content =
                [
                    new GrokTextPart { Text = "Where is the International Space Station (NORAD ID 25544) right now?" }
                ]
            }
        };
        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Temperature = 0f,
            Tools = grokTools,
            Tool_choice = Tool_choice.Auto
        };

        await WaitForRateLimitAsync();
        GrokChatCompletionResponse? response = null;
        try
        {
            response = await client.CreateChatCompletionAsync(request);
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"Initial API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(response, "Initial response should not be null.");
        Assert.IsTrue(response.Choices.Count > 0, "Initial response should have at least one choice.");

        var choice = response.Choices.First();
        if (choice.Message.Tool_calls?.Count > 0)
        {
            var grokToolCall = choice.Message.Tool_calls.First();
            Assert.AreEqual("get_starlink_position", grokToolCall.Function.Name,
                "GrokTool should be get_starlink_position.");

            var args =
                JsonConvert.DeserializeObject<Dictionary<string, int>>(grokToolCall.Function.Arguments) ??
                throw new Exception("Could not process arguments from function");
            var noradId = args["norad_id"];
            Assert.AreEqual(25544, noradId, "GrokTool call should target NORAD ID 25544.");

            // Step 2: Hit the real N2YO API
            var apiKey = GetN2YoApiKeyFromFileOrEnv();
            var url =
                $"https://api.n2yo.com/rest/v1/satellite/positions/{noradId}/48.8566/2.3522/0/1/&apiKey={apiKey}";
            string result;
            try
            {
                var n2YoResponse = await httpClient.GetAsync(url);
                n2YoResponse.EnsureSuccessStatusCode();
                var json = await n2YoResponse.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(json) ??
                               throw new Exception("could not process response data");

                // Verify response structure
                Assert.IsNotNull(data.info, "N2YO response should contain 'info'.");
                Assert.IsNotNull(data.positions, "N2YO response should contain 'positions'.");
                Assert.IsTrue(((JArray)data.positions).Count > 0,
                    "N2YO 'positions' should have at least one entry.");

                var position = data.positions[0];
                Assert.IsNotNull(position.satlatitude, "Position should include 'satlatitude'.");
                Assert.IsNotNull(position.satlongitude, "Position should include 'satlongitude'.");
                Assert.IsNotNull(position.sataltitude, "Position should include 'sataltitude'.");

                // Handle optional timestamp
                var timestamp = data.info.timestamp != null ? (long?)data.info.timestamp : null;

                result = JsonConvert.SerializeObject(new
                {
                    norad_id = noradId,
                    latitude = (double)position.satlatitude,
                    longitude = (double)position.satlongitude,
                    altitude = (double)position.sataltitude,
                    timestamp
                });
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"N2YO API failed: Status {ex.StatusCode} - {ex.Message}");
                result =
                    "{\"norad_id\": 25544, \"latitude\": 51.0, \"longitude\": -0.1, \"altitude\": 420, \"timestamp\": 1739999999}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error calling N2YO API: {ex.Message}");
                result =
                    "{\"norad_id\": 25544, \"latitude\": 51.0, \"longitude\": -0.1, \"altitude\": 420, \"timestamp\": 1739999999}";
            }

            // Add assistant message and GrokTool result
            messages.Add(choice.Message);
            messages.Add(new GrokToolMessage
            {
                Content = result,
                Tool_call_id = grokToolCall.Id
            });

            // Step 3: Send back to Grok
            var followUpRequest = new GrokChatCompletionRequest
            {
                Messages = messages,
                Model = model,
                Stream = false,
                Temperature = 0f,
                Tools = grokTools,
                Tool_choice = Tool_choice.Auto
            };

            await WaitForRateLimitAsync();
            GrokChatCompletionResponse? finalResponse = null;
            try
            {
                finalResponse = await client.CreateChatCompletionAsync(followUpRequest);
            }
            catch (GrokSdkException ex)
            {
                Assert.Fail(
                    $"Follow-up API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
            }

            Assert.IsNotNull(finalResponse, "Final response should not be null.");
            Assert.IsTrue(finalResponse.Choices.Count > 0, "Final response should have at least one choice.");
            var finalChoice = finalResponse.Choices.First();
            Assert.IsFalse(string.IsNullOrEmpty(finalChoice.Message.Content), "Final response should have content.");
            Assert.IsTrue(
                finalChoice.Message.Content.Contains("25544") || finalChoice.Message.Content.Contains("latitude") ||
                finalChoice.Message.Content.Contains("longitude"),
                "Final response should mention ISS (25544) position.");
            Assert.AreEqual(GrokChoiceFinish_reason.Stop, finalChoice.Finish_reason,
                "Final finish reason should be 'stop'.");
        }
        else
        {
            Assert.Inconclusive(
                "Grok did not request a GrokTool call with 'auto'; cannot test external API integration.");
        }

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }

    [DataTestMethod]
    [DataRow("grok-3-latest")]
    [DataRow("grok-2-latest")]
    [DataRow("grok-4-latest")]
    [TestCategory("Live")]
    public async Task CreateChatCompletionAsync_LiveToolChoice_AskStarlinkSatelliteCount(string model)
    {
        var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var messages = new Collection<GrokMessage>
        {
            new GrokSystemMessage { Content = "You are an assistant with access to satellite data GrokTools." },
            new GrokUserMessage
            {
                Content =
                [
                    new GrokTextPart
                    {
                        Text =
                            "How many Starlink satellites are out there? Category Code 52; Only give me the number value back (not json, just the number)"
                    }
                ]
            }
        };

        // Define a GrokTool to count Starlink satellites
        var grokTools = new Collection<GrokTool>
        {
            new()
            {
                Type = GrokToolType.Function,
                Function = new GrokFunctionDefinition
                {
                    Name = "get_satellite_count",
                    Description = "Get the satellite count from n2yo website using the category code",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            categoryCode = new
                            {
                                type = "number",
                                description =
                                    "The Category Code for n2yo website for a specific company (such as Starlink 52)"
                            }
                        },
                        required = new[] { "categoryCode" }
                    }
                }
            }
        };

        // Step 1: Initial request to Grok
        var request = new GrokChatCompletionRequest
        {
            Messages = messages,
            Model = model,
            Stream = false,
            Temperature = 0f,
            Tools = grokTools,
            Tool_choice = Tool_choice.Auto // Let Grok decide to use the GrokTool
        };

        var response = await client.CreateChatCompletionAsync(request);
        var choice = response.Choices.First();

        if (choice.Message.Tool_calls?.Count > 0)
        {
            var grokToolCall = choice.Message.Tool_calls.First();
            if (grokToolCall.Function.Name == "get_satellite_count")
            {
                // Step 2: Call N2YO API to get Starlink satellite count
                var args = JsonConvert.DeserializeObject<Dictionary<string, int>>(grokToolCall.Function.Arguments) ??
                           throw new Exception("Could not process arguments from function");
                var categoryCode = args["categoryCode"];

                var data = await SatelliteHelper.GetSatellitesAsync(categoryCode);

                var totalCount = new
                {
                    SatelliteCount = data.Count
                };

                var result = JsonConvert.SerializeObject(totalCount);

                // Add assistant message and GrokTool result
                messages.Add(choice.Message);
                messages.Add(new GrokToolMessage
                {
                    Content = result,
                    Tool_call_id = grokToolCall.Id
                });

                // Step 3: Send back to Grok
                var followUpRequest = new GrokChatCompletionRequest
                {
                    Messages = messages,
                    Model = model,
                    Stream = false,
                    Temperature = 0f,
                    Tools = grokTools,
                    Tool_choice = Tool_choice.Auto
                };

                var finalResponse = await client.CreateChatCompletionAsync(followUpRequest);
                var responseMessage = finalResponse.Choices.First().Message.Content;
                Console.WriteLine(responseMessage);
                if (int.TryParse(responseMessage, out var count))
                    Assert.IsTrue(count > 1000, $"Something isn't right - Starlink reporting {count} active?");
                else
                    Assert.Fail("Expected only a number and the response was {}");
            }
        }
        else
        {
            Assert.Fail("The GrokTool was not called like it should have been!");
        }

        // Safety Check for Live Unit Tests to prevent API exhaustion
        await WaitForRateLimitAsync();
    }
}