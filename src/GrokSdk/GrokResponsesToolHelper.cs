using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GrokSdk
{
    /// <summary>
    /// Shared helper methods for Responses API tool wrappers.
    /// Handles applying common options and extracting tool usage data from responses.
    /// </summary>
    internal static class GrokResponsesToolHelper
    {
        /// <summary>
        /// Applies <see cref="GrokResponsesToolOptions"/> to a <see cref="GrokResponseRequest"/>.
        /// </summary>
        /// <param name="request">The request to configure.</param>
        /// <param name="options">Options to apply (can be null).</param>
        internal static void ApplyOptions(GrokResponseRequest request, GrokResponsesToolOptions? options)
        {
            if (options == null) return;

            if (options.PreviousResponseId != null)
                request.Previous_response_id = options.PreviousResponseId;

            if (options.MaxOutputTokens.HasValue)
                request.Max_output_tokens = options.MaxOutputTokens.Value;

            if (options.MaxTurns.HasValue)
                request.Max_turns = options.MaxTurns.Value;

            if (options.Include?.Count > 0)
                request.Include = options.Include;
        }

        /// <summary>
        /// Extracts <see cref="GrokToolUsage"/> records from Responses API output items.
        /// Captures server-side tool call events (web_search_call, code_interpreter_call, etc.).
        /// </summary>
        /// <param name="response">The API response.</param>
        /// <returns>List of tool usage events.</returns>
        internal static List<GrokToolUsage> ExtractToolUsages(GrokResponseResponse response)
        {
            var usages = new List<GrokToolUsage>();

            if (response.Output == null) return usages;

            foreach (var outputItem in response.Output)
            {
                var itemType = outputItem.Type.ToString().ToLowerInvariant();

                // Only capture tool call items (not message/reasoning output)
                if (itemType.Contains("call"))
                {
                    var usage = new GrokToolUsage
                    {
                        Type = itemType,
                        Id = outputItem.Id,
                        Name = outputItem.Name,
                        Status = outputItem.Status.ToString().ToLowerInvariant()
                    };

                    // Extract action details if available (Action is typed as object from NSwag)
                    if (outputItem.Action is System.Collections.Generic.IDictionary<string, object> actionDict)
                    {
                        usage.Action = new Dictionary<string, object>(actionDict);
                    }
                    else if (outputItem.Action is Newtonsoft.Json.Linq.JObject jObj)
                    {
                        usage.Action = jObj.ToObject<Dictionary<string, object>>();
                    }

                    usages.Add(usage);
                }
            }

            return usages;
        }

        /// <summary>
        /// Extracts <see cref="GrokCitation"/> records from Responses API output items.
        /// </summary>
        /// <param name="response">The API response.</param>
        /// <returns>List of citations.</returns>
        internal static List<GrokCitation> ExtractCitations(GrokResponseResponse response)
        {
            var citations = new List<GrokCitation>();

            if (response.Output == null) return citations;

            foreach (var outputItem in response.Output)
            {
                if (outputItem.Content == null) continue;

                foreach (var content in outputItem.Content)
                {
                    if (content.Annotations == null) continue;

                    foreach (var annotation in content.Annotations)
                    {
                        citations.Add(new GrokCitation
                        {
                            Type = annotation.Type ?? "url_citation",
                            Url = annotation.Url ?? string.Empty,
                            Title = annotation.Title,
                            StartIndex = annotation.Start_index,
                            EndIndex = annotation.End_index
                        });
                    }
                }
            }

            return citations;
        }
    }
}
