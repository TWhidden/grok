using System.Text.RegularExpressions;

namespace GrokSdk.Tests.Helpers;

// Define the record type to hold satellite data
public record SatelliteData(
    string N,
    int Id
);

public static class SatelliteHelper
{
    private static HashSet<SatelliteData> ParseSatellites(string htmlString)
    {
        var satellites = new HashSet<SatelliteData>();

        // Step 1: Extract the table content
        string tableStart = "<table class=\"footable table\" id=\"categoriestab\">";
        string tableEnd = "</table>";
        int startIndex = htmlString.IndexOf(tableStart, StringComparison.Ordinal);
        if (startIndex == -1) return satellites; // Table not found
        startIndex += tableStart.Length;
        int endIndex = htmlString.IndexOf(tableEnd, startIndex, StringComparison.Ordinal);
        if (endIndex == -1) return satellites; // Closing tag not found

        string tableHtml = htmlString.Substring(startIndex, endIndex - startIndex);

        // Step 2: Find all <tr>...</tr> segments
        string trPattern = @"<tr\b[^>]*>.*?</tr>";
        var matches = Regex.Matches(tableHtml, trPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            string rowHtml = match.Value;

            // Step 3: Extract <td> elements
            string tdPattern = @"<td\b[^>]*>(.*?)</td>";
            var tdMatches = Regex.Matches(rowHtml, tdPattern, RegexOptions.Singleline);
            if (tdMatches.Count < 5) continue; // Skip rows without enough data (e.g., empty <tr>)

            try
            {
                // Extract data from each <td>
                string[] cells = new string[tdMatches.Count];
                for (int i = 0; i < tdMatches.Count; i++)
                    cells[i] = tdMatches[i].Groups[1].Value.Trim();

                // Parse Name (may contain <a> tag)
                string nameMatch = Regex.Match(cells[0], "<a[^>]*>(.*?)</a>", RegexOptions.IgnoreCase).Groups[1].Value;
                string name = string.IsNullOrEmpty(nameMatch) ? cells[0] : nameMatch;

                // Parse NORAD ID
                int noradId = int.Parse(cells[1]);
               
                // Create satellite object
                var satellite = new SatelliteData(name, noradId); 
                satellites.Add(satellite);
            }
            catch
            {
                // Ignore
            }
        }

        return satellites;
    }

    // Example usage with HttpClient
    public static async Task<HashSet<SatelliteData>> GetSatellitesAsync(int categoryId)
    {
        using var client = new HttpClient();
        var url = $"https://www.n2yo.com/satellites/?c={categoryId}&p=A";

        try
        {
            var htmlContent = await client.GetStringAsync(url);
            return ParseSatellites(htmlContent);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to retrieve or parse satellite data: {ex.Message}");
        }
    }
}