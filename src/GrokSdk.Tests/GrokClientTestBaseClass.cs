using System.Diagnostics;
using System.Reflection;

namespace GrokSdk.Tests;

public abstract class GrokClientTestBaseClass
{
    protected static string? ApiToken { get; set; }
    private static readonly object Lock = new();
    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
    private static double _lastCallElapsedSeconds;

    protected static string GetApiKeyFromFileOrEnv()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GROK_API_KEY")))
            return Environment.GetEnvironmentVariable("GROK_API_KEY")!;

        var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                              throw new Exception("Failed to get assembly location");
        var filePath = Path.Combine(outputDirectory, "apikey.txt");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("API key file 'apikey.txt' not found in the test output directory.");

        var apiKey = File.ReadAllText(filePath).Trim();
        if (string.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("API key file 'apikey.txt' is empty.");

        return apiKey;
    }


    protected static async Task WaitForRateLimitAsync()
    {
        var currentElapsed = Stopwatch.Elapsed.TotalSeconds;
        double timeSinceLastCall;
        lock (Lock)
        {
            timeSinceLastCall = currentElapsed - _lastCallElapsedSeconds;
        }

        if (timeSinceLastCall < 1)
        {
            var delaySeconds = 1 - timeSinceLastCall;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            currentElapsed = Stopwatch.Elapsed.TotalSeconds;
        }

        lock (Lock)
        {
            _lastCallElapsedSeconds = currentElapsed;
        }
    }
}