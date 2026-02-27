using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GrokSdk
{
    /// <summary>
    /// Helper class for working with structured JSON outputs from the Grok API.
    /// Enables schema-constrained responses via the <c>response_format</c> parameter.
    /// </summary>
    public static class GrokStructuredOutput
    {
        /// <summary>
        /// Creates a <see cref="GrokResponseFormat"/> for structured JSON output with the given schema.
        /// </summary>
        /// <param name="schemaName">A name for the schema (used for identification).</param>
        /// <param name="schema">The JSON Schema object defining the expected output structure.</param>
        /// <param name="strict">Whether the model must strictly follow the schema. Defaults to true.</param>
        /// <returns>A configured GrokResponseFormat for JSON schema output.</returns>
        public static GrokResponseFormat CreateJsonFormat(string schemaName, object schema, bool strict = true)
        {
            return new GrokResponseFormat
            {
                Type = GrokResponseFormatType.Json_schema,
                Json_schema = new GrokJsonSchemaDefinition
                {
                    Name = schemaName,
                    Schema = schema,
                    Strict = strict
                }
            };
        }

        /// <summary>
        /// Creates a <see cref="GrokResponseFormat"/> for structured JSON output from a type.
        /// Generates a JSON schema automatically from the type's properties.
        /// </summary>
        /// <typeparam name="T">The type to generate a schema for.</typeparam>
        /// <param name="strict">Whether the model must strictly follow the schema. Defaults to true.</param>
        /// <returns>A configured GrokResponseFormat for JSON schema output.</returns>
        public static GrokResponseFormat CreateJsonFormat<T>(bool strict = true)
        {
            var schema = GenerateSchemaFromType(typeof(T));
            return new GrokResponseFormat
            {
                Type = GrokResponseFormatType.Json_schema,
                Json_schema = new GrokJsonSchemaDefinition
                {
                    Name = typeof(T).Name,
                    Schema = schema,
                    Strict = strict
                }
            };
        }

        /// <summary>
        /// Creates a plain text response format.
        /// </summary>
        /// <returns>A GrokResponseFormat for plain text output.</returns>
        public static GrokResponseFormat CreateTextFormat()
        {
            return new GrokResponseFormat
            {
                Type = GrokResponseFormatType.Text
            };
        }

        /// <summary>
        /// Sends a chat completion request with structured output and deserializes the response to the given type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to.</typeparam>
        /// <param name="client">The GrokClient to use.</param>
        /// <param name="prompt">The user prompt.</param>
        /// <param name="schemaName">Name for the schema.</param>
        /// <param name="schema">The JSON Schema object.</param>
        /// <param name="model">Model to use. Defaults to "grok-4-fast".</param>
        /// <param name="systemInstruction">Optional system instruction.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The deserialized response.</returns>
        public static async Task<T?> AskAsJsonAsync<T>(
            GrokClient client,
            string prompt,
            string schemaName,
            object schema,
            string model = "grok-4-fast",
            string? systemInstruction = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var messages = new List<GrokMessage>();

            if (!string.IsNullOrEmpty(systemInstruction))
            {
                messages.Add(new GrokSystemMessage { Content = systemInstruction });
            }

            messages.Add(new GrokUserMessage
            {
                Content = new System.Collections.ObjectModel.Collection<GrokContent>
                {
                    new GrokTextPart { Text = prompt }
                }
            });

            var request = new GrokChatCompletionRequest
            {
                Messages = messages,
                Model = model,
                Temperature = 0,
                Stream = false,
                Response_format = CreateJsonFormat(schemaName, schema)
            };

            var response = await client.CreateChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
            var content = response.Choices.FirstOrDefault()?.Message?.Content;

            return string.IsNullOrWhiteSpace(content) ? null : JsonConvert.DeserializeObject<T>(content!);
        }

        /// <summary>
        /// Sends a chat completion request with structured output using an auto-generated schema from the type.
        /// </summary>
        /// <typeparam name="T">The type to generate schema for and deserialize the response to.</typeparam>
        /// <param name="client">The GrokClient to use.</param>
        /// <param name="prompt">The user prompt.</param>
        /// <param name="model">Model to use. Defaults to "grok-4-fast".</param>
        /// <param name="systemInstruction">Optional system instruction.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The deserialized response.</returns>
        public static async Task<T?> AskAsJsonAsync<T>(
            GrokClient client,
            string prompt,
            string model = "grok-4-fast",
            string? systemInstruction = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var schema = GenerateSchemaFromType(typeof(T));
            return await AskAsJsonAsync<T>(client, prompt, typeof(T).Name, schema, model, systemInstruction, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates a simple JSON schema from a C# type's public properties.
        /// Supports string, int, bool, double, float, arrays, and nested objects.
        /// </summary>
        internal static object GenerateSchemaFromType(Type type)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var jsonProp = prop.GetCustomAttributes(typeof(JsonPropertyAttribute), false)
                    .FirstOrDefault() as JsonPropertyAttribute;

                var name = jsonProp?.PropertyName ?? prop.Name;
                var propType = prop.PropertyType;

                // Check if nullable
                var isNullable = !propType.IsValueType ||
                    (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>));

                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    propType = Nullable.GetUnderlyingType(propType) ?? propType;

                properties[name] = GetSchemaForType(propType);

                if (!isNullable)
                    required.Add(name);
            }

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false
            };

            if (required.Count > 0)
                schema["required"] = required;

            return schema;
        }

        private static object GetSchemaForType(Type type)
        {
            if (type == typeof(string))
                return new Dictionary<string, object> { ["type"] = "string" };
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
                return new Dictionary<string, object> { ["type"] = "integer" };
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return new Dictionary<string, object> { ["type"] = "number" };
            if (type == typeof(bool))
                return new Dictionary<string, object> { ["type"] = "boolean" };

            // Arrays and collections
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                return new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = GetSchemaForType(elementType)
                };
            }

            if (type.IsGenericType && (
                type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>) ||
                type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                var elementType = type.GetGenericArguments()[0];
                return new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = GetSchemaForType(elementType)
                };
            }

            // Nested object — recurse
            if (type.IsClass && type != typeof(object))
            {
                return GenerateSchemaFromType(type);
            }

            return new Dictionary<string, object> { ["type"] = "string" };
        }
    }
}
