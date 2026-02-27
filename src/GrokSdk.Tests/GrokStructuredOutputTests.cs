using Newtonsoft.Json;

namespace GrokSdk.Tests;

[TestClass]
public class GrokStructuredOutputTests : GrokClientTestBaseClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        ApiToken = GetApiKeyFromFileOrEnv();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task AskAsJsonAsync_WithManualSchema_ReturnsStructuredOutput()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        var schema = new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                capital = new { type = "string" },
                population = new { type = "integer" }
            },
            required = new[] { "name", "capital", "population" }
        };

        await WaitForRateLimitAsync();

        CountryInfo? result = null;
        try
        {
            result = await GrokStructuredOutput.AskAsJsonAsync<CountryInfo>(
                client,
                "Tell me about France. Provide the name, capital, and approximate population.",
                "country_info",
                schema,
                model: "grok-3-mini-fast");
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(result, "Result should not be null.");
        Assert.AreEqual("France", result.Name, "Country name should be France.");
        Assert.AreEqual("Paris", result.Capital, "Capital should be Paris.");
        Assert.IsTrue(result.Population > 0, "Population should be positive.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task AskAsJsonAsync_WithAutoSchema_ReturnsStructuredOutput()
    {
        using var httpClient = new HttpClient();
        var client = new GrokClient(httpClient, ApiToken ?? throw new Exception("API Token not set"));

        await WaitForRateLimitAsync();

        CountryInfo? result = null;
        try
        {
            result = await GrokStructuredOutput.AskAsJsonAsync<CountryInfo>(
                client,
                "Tell me about Japan. Provide the name, capital, and approximate population.",
                model: "grok-3-mini-fast");
        }
        catch (GrokSdkException ex)
        {
            Assert.Fail($"API call failed with status {ex.StatusCode}: {ex.Message}\nResponse: {ex.Response}");
        }

        Assert.IsNotNull(result, "Result should not be null.");
        Assert.AreEqual("Japan", result.Name, "Country name should be Japan.");
        Assert.AreEqual("Tokyo", result.Capital, "Capital should be Tokyo.");
        Assert.IsTrue(result.Population > 0, "Population should be positive.");

        await WaitForRateLimitAsync();
    }

    [TestMethod]
    public void CreateJsonFormat_ReturnsValidFormat()
    {
        var schema = new { type = "object", properties = new { name = new { type = "string" } } };
        var format = GrokStructuredOutput.CreateJsonFormat("test_schema", schema);

        Assert.IsNotNull(format, "Format should not be null.");
        Assert.AreEqual(GrokResponseFormatType.Json_schema, format.Type, "Type should be json_schema.");
        Assert.IsNotNull(format.Json_schema, "Json_schema should not be null.");
        Assert.AreEqual("test_schema", format.Json_schema.Name, "Schema name should match.");
    }

    [TestMethod]
    public void CreateJsonFormat_Generic_GeneratesSchemaFromType()
    {
        var format = GrokStructuredOutput.CreateJsonFormat<CountryInfo>();

        Assert.IsNotNull(format, "Format should not be null.");
        Assert.AreEqual(GrokResponseFormatType.Json_schema, format.Type, "Type should be json_schema.");
        Assert.IsNotNull(format.Json_schema, "Json_schema should not be null.");
        Assert.AreEqual("CountryInfo", format.Json_schema.Name, "Schema name should match type name.");
    }

    [TestMethod]
    public void CreateTextFormat_ReturnsTextFormat()
    {
        var format = GrokStructuredOutput.CreateTextFormat();

        Assert.IsNotNull(format, "Format should not be null.");
        Assert.AreEqual(GrokResponseFormatType.Text, format.Type, "Type should be text.");
    }

    private class CountryInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("capital")]
        public string Capital { get; set; } = "";

        [JsonProperty("population")]
        public int Population { get; set; }
    }
}
