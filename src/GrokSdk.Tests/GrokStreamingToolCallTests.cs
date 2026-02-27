using GrokSdk;

namespace GrokSdk.Tests;

[TestClass]
public class GrokStreamingToolCallTests
{
    // ==========================================
    // StreamingToolCallAccumulator Unit Tests
    // ==========================================

    [TestMethod]
    public void Accumulator_InitialState_HasNoToolCalls()
    {
        var accumulator = new StreamingToolCallAccumulator();
        Assert.IsFalse(accumulator.HasToolCalls);
        Assert.AreEqual(0, accumulator.GetToolCalls().Count);
    }

    [TestMethod]
    public void Accumulator_SingleToolCall_AccumulatesCorrectly()
    {
        var accumulator = new StreamingToolCallAccumulator();

        // First chunk: ID, type, function name, and start of arguments
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 0,
            Id = "call_abc123",
            Type = "function",
            Function = new ToolCallFunctionDelta
            {
                Name = "get_weather",
                Arguments = "{\"city\":"
            }
        });

        // Second chunk: continuation of arguments
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 0,
            Function = new ToolCallFunctionDelta
            {
                Arguments = "\"Paris\"}"
            }
        });

        Assert.IsTrue(accumulator.HasToolCalls);

        var toolCalls = accumulator.GetToolCalls();
        Assert.AreEqual(1, toolCalls.Count);

        var call = toolCalls[0];
        Assert.AreEqual("call_abc123", call.Id);
        Assert.AreEqual(GrokToolCallType.Function, call.Type);
        Assert.AreEqual("get_weather", call.Function.Name);
        Assert.AreEqual("{\"city\":\"Paris\"}", call.Function.Arguments);
    }

    [TestMethod]
    public void Accumulator_MultipleToolCalls_AccumulatesAllCorrectly()
    {
        var accumulator = new StreamingToolCallAccumulator();

        // Tool call 0 - first chunk
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 0,
            Id = "call_001",
            Type = "function",
            Function = new ToolCallFunctionDelta
            {
                Name = "search",
                Arguments = "{\"query\":\"hello\"}"
            }
        });

        // Tool call 1 - first chunk
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 1,
            Id = "call_002",
            Type = "function",
            Function = new ToolCallFunctionDelta
            {
                Name = "calculate",
                Arguments = "{\"expr\":"
            }
        });

        // Tool call 1 - second chunk
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 1,
            Function = new ToolCallFunctionDelta
            {
                Arguments = "\"2+2\"}"
            }
        });

        var toolCalls = accumulator.GetToolCalls();
        Assert.AreEqual(2, toolCalls.Count);

        Assert.AreEqual("call_001", toolCalls[0].Id);
        Assert.AreEqual("search", toolCalls[0].Function.Name);
        Assert.AreEqual("{\"query\":\"hello\"}", toolCalls[0].Function.Arguments);

        Assert.AreEqual("call_002", toolCalls[1].Id);
        Assert.AreEqual("calculate", toolCalls[1].Function.Name);
        Assert.AreEqual("{\"expr\":\"2+2\"}", toolCalls[1].Function.Arguments);
    }

    [TestMethod]
    public void Accumulator_Reset_ClearsState()
    {
        var accumulator = new StreamingToolCallAccumulator();

        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 0,
            Id = "call_xyz",
            Type = "function",
            Function = new ToolCallFunctionDelta
            {
                Name = "test",
                Arguments = "{}"
            }
        });

        Assert.IsTrue(accumulator.HasToolCalls);

        accumulator.Reset();

        Assert.IsFalse(accumulator.HasToolCalls);
        Assert.AreEqual(0, accumulator.GetToolCalls().Count);
    }

    [TestMethod]
    public void Accumulator_ManyArgumentChunks_ConcatenatesCorrectly()
    {
        var accumulator = new StreamingToolCallAccumulator();

        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 0,
            Id = "call_long",
            Type = "function",
            Function = new ToolCallFunctionDelta { Name = "process" }
        });

        // Simulate many small argument chunks
        var fragments = new[] { "{", "\"text\":", "\"The ", "quick ", "brown ", "fox", "\"}" };
        foreach (var fragment in fragments)
        {
            accumulator.AddDelta(new ToolCallDelta
            {
                Index = 0,
                Function = new ToolCallFunctionDelta { Arguments = fragment }
            });
        }

        var toolCalls = accumulator.GetToolCalls();
        Assert.AreEqual(1, toolCalls.Count);
        Assert.AreEqual("{\"text\":\"The quick brown fox\"}", toolCalls[0].Function.Arguments);
    }

    [TestMethod]
    public void Accumulator_NullFunctionFields_DoesNotOverwrite()
    {
        var accumulator = new StreamingToolCallAccumulator();

        // First chunk sets name
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 0,
            Id = "call_001",
            Function = new ToolCallFunctionDelta { Name = "myFunc" }
        });

        // Second chunk with null name (just arguments)
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 0,
            Function = new ToolCallFunctionDelta { Name = null, Arguments = "{}" }
        });

        var toolCalls = accumulator.GetToolCalls();
        Assert.AreEqual("myFunc", toolCalls[0].Function.Name, "Name should not be overwritten by null");
    }

    [TestMethod]
    public void Accumulator_OutOfOrderIndices_SortsCorrectly()
    {
        var accumulator = new StreamingToolCallAccumulator();

        // Add index 2 first
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 2,
            Id = "call_c",
            Type = "function",
            Function = new ToolCallFunctionDelta { Name = "funcC", Arguments = "{}" }
        });

        // Then index 0
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 0,
            Id = "call_a",
            Type = "function",
            Function = new ToolCallFunctionDelta { Name = "funcA", Arguments = "{}" }
        });

        // Then index 1
        accumulator.AddDelta(new ToolCallDelta
        {
            Index = 1,
            Id = "call_b",
            Type = "function",
            Function = new ToolCallFunctionDelta { Name = "funcB", Arguments = "{}" }
        });

        var toolCalls = accumulator.GetToolCalls();
        Assert.AreEqual(3, toolCalls.Count);
        Assert.AreEqual("funcA", toolCalls[0].Function.Name);
        Assert.AreEqual("funcB", toolCalls[1].Function.Name);
        Assert.AreEqual("funcC", toolCalls[2].Function.Name);
    }

    // ==========================================
    // ToolCallDelta / ToolCallFunctionDelta Unit Tests
    // ==========================================

    [TestMethod]
    public void ToolCallDelta_DefaultValues()
    {
        var delta = new ToolCallDelta();
        Assert.AreEqual(0, delta.Index);
        Assert.IsNull(delta.Id);
        Assert.IsNull(delta.Type);
        Assert.IsNull(delta.Function);
    }

    [TestMethod]
    public void ToolCallFunctionDelta_DefaultValues()
    {
        var delta = new ToolCallFunctionDelta();
        Assert.IsNull(delta.Name);
        Assert.IsNull(delta.Arguments);
    }

    [TestMethod]
    public void ToolCallDelta_DeserializesFromJson()
    {
        var json = @"{
            ""index"": 0,
            ""id"": ""call_test"",
            ""type"": ""function"",
            ""function"": {
                ""name"": ""get_weather"",
                ""arguments"": ""{\""city\"":\""Tokyo\""}""
            }
        }";

        var delta = Newtonsoft.Json.JsonConvert.DeserializeObject<ToolCallDelta>(json);
        Assert.IsNotNull(delta);
        Assert.AreEqual(0, delta!.Index);
        Assert.AreEqual("call_test", delta.Id);
        Assert.AreEqual("function", delta.Type);
        Assert.IsNotNull(delta.Function);
        Assert.AreEqual("get_weather", delta.Function!.Name);
        Assert.IsTrue(delta.Function.Arguments!.Contains("Tokyo"));
    }

    [TestMethod]
    public void ToolCallDelta_DeserializesPartialChunk()
    {
        // Subsequent chunks only have index and partial arguments
        var json = @"{
            ""index"": 0,
            ""function"": {
                ""arguments"": ""world}""
            }
        }";

        var delta = Newtonsoft.Json.JsonConvert.DeserializeObject<ToolCallDelta>(json);
        Assert.IsNotNull(delta);
        Assert.AreEqual(0, delta!.Index);
        Assert.IsNull(delta.Id);
        Assert.IsNull(delta.Type);
        Assert.IsNotNull(delta.Function);
        Assert.IsNull(delta.Function!.Name);
        Assert.AreEqual("world}", delta.Function.Arguments);
    }

    // ==========================================
    // MessageDelta.ToolCalls Unit Tests
    // ==========================================

    [TestMethod]
    public void MessageDelta_ToolCalls_DeserializesFromJson()
    {
        var json = @"{
            ""role"": ""assistant"",
            ""tool_calls"": [
                {
                    ""index"": 0,
                    ""id"": ""call_abc"",
                    ""type"": ""function"",
                    ""function"": {
                        ""name"": ""search"",
                        ""arguments"": ""{}""
                    }
                }
            ]
        }";

        var delta = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageDelta>(json);
        Assert.IsNotNull(delta);
        Assert.AreEqual("assistant", delta!.Role);
        Assert.IsNotNull(delta.ToolCalls);
        Assert.AreEqual(1, delta.ToolCalls!.Count);
        Assert.AreEqual("call_abc", delta.ToolCalls[0].Id);
    }

    [TestMethod]
    public void MessageDelta_WithoutToolCalls_IsNull()
    {
        var json = @"{ ""role"": ""assistant"", ""content"": ""Hello"" }";

        var delta = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageDelta>(json);
        Assert.IsNotNull(delta);
        Assert.IsNull(delta!.ToolCalls);
    }

    // ==========================================
    // ResponsesStreamEvent & ResponsesTextDelta Unit Tests
    // ==========================================

    [TestMethod]
    public void ResponsesStreamEvent_DefaultValues()
    {
        var evt = new ResponsesStreamEvent();
        Assert.AreEqual(string.Empty, evt.EventType);
        Assert.AreEqual(string.Empty, evt.Data);
        Assert.IsNull(evt.ParsedData);
    }

    [TestMethod]
    public void ResponsesStreamEvent_ParsesJsonData()
    {
        var evt = new ResponsesStreamEvent
        {
            EventType = "response.created",
            Data = "{\"id\":\"resp_test\",\"status\":\"in_progress\"}"
        };

        Assert.IsNotNull(evt.ParsedData);
        Assert.AreEqual("resp_test", evt.ParsedData!["id"]?.ToString());
    }

    [TestMethod]
    public void ResponsesTextDelta_Properties()
    {
        var delta = new ResponsesTextDelta
        {
            OutputIndex = 1,
            ContentIndex = 2,
            Delta = "Hello world"
        };

        Assert.AreEqual(1, delta.OutputIndex);
        Assert.AreEqual(2, delta.ContentIndex);
        Assert.AreEqual("Hello world", delta.Delta);
    }
}
