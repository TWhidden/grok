using GrokSdk.Tools;

namespace GrokSdk.Tests;

[TestClass]
public class GrokToolRegistrationTests
{
    private GrokClient CreateClient()
    {
        return new GrokClient(new HttpClient(), "test-key");
    }

    private IGrokTool CreateDummyTool(string name = "dummy_tool")
    {
        return new GrokToolDefinition(
            name,
            $"A dummy tool named {name}",
            new { type = "object", properties = new { input = new { type = "string" } } },
            args => Task.FromResult("{\"result\": \"ok\"}"));
    }

    [TestMethod]
    public void RegisterTool_AddsToolSuccessfully()
    {
        var thread = new GrokThread(CreateClient());
        var tool = CreateDummyTool();

        thread.RegisterTool(tool);

        Assert.IsTrue(thread.IsToolRegistered("dummy_tool"));
        Assert.AreEqual(1, thread.RegisteredToolNames.Count);
        CollectionAssert.Contains(thread.RegisteredToolNames.ToList(), "dummy_tool");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void RegisterTool_DuplicateName_Throws()
    {
        var thread = new GrokThread(CreateClient());
        thread.RegisterTool(CreateDummyTool("my_tool"));
        thread.RegisterTool(CreateDummyTool("my_tool")); // Should throw
    }

    [TestMethod]
    public void RegisterTool_WithNameOverride_RegistersUnderOverrideName()
    {
        var thread = new GrokThread(CreateClient());
        var tool = CreateDummyTool("original_name");

        thread.RegisterTool(tool, nameOverride: "custom_name");

        Assert.IsTrue(thread.IsToolRegistered("custom_name"));
        Assert.IsFalse(thread.IsToolRegistered("original_name"));
        Assert.AreEqual(1, thread.RegisteredToolNames.Count);
    }

    [TestMethod]
    public void RegisterTool_SameToolDifferentNames_BothRegistered()
    {
        var thread = new GrokThread(CreateClient());
        var tool = CreateDummyTool("base_tool");

        thread.RegisterTool(tool, nameOverride: "alias_one");
        thread.RegisterTool(tool, nameOverride: "alias_two");

        Assert.AreEqual(2, thread.RegisteredToolNames.Count);
        Assert.IsTrue(thread.IsToolRegistered("alias_one"));
        Assert.IsTrue(thread.IsToolRegistered("alias_two"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void RegisterTool_NameOverrideConflictsWithExisting_Throws()
    {
        var thread = new GrokThread(CreateClient());
        thread.RegisterTool(CreateDummyTool("tool_a"));
        thread.RegisterTool(CreateDummyTool("tool_b"), nameOverride: "tool_a"); // Should throw
    }

    [TestMethod]
    public void UnregisterTool_ByName_RemovesTool()
    {
        var thread = new GrokThread(CreateClient());
        thread.RegisterTool(CreateDummyTool("removable"));

        Assert.IsTrue(thread.IsToolRegistered("removable"));

        var removed = thread.UnregisterTool("removable");

        Assert.IsTrue(removed);
        Assert.IsFalse(thread.IsToolRegistered("removable"));
        Assert.AreEqual(0, thread.RegisteredToolNames.Count);
    }

    [TestMethod]
    public void UnregisterTool_ByName_NonExistent_ReturnsFalse()
    {
        var thread = new GrokThread(CreateClient());

        var removed = thread.UnregisterTool("nonexistent");

        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void UnregisterTool_ByInstance_RemovesTool()
    {
        var thread = new GrokThread(CreateClient());
        var tool = CreateDummyTool("my_tool");
        thread.RegisterTool(tool);

        var removed = thread.UnregisterTool(tool);

        Assert.IsTrue(removed);
        Assert.IsFalse(thread.IsToolRegistered("my_tool"));
    }

    [TestMethod]
    public void UnregisterTool_ByNameOverride_RequiresOverrideName()
    {
        var thread = new GrokThread(CreateClient());
        var tool = CreateDummyTool("original");
        thread.RegisterTool(tool, nameOverride: "override_name");

        // Unregister by original name should fail — it was registered under the override
        var removedByOriginal = thread.UnregisterTool("original");
        Assert.IsFalse(removedByOriginal);
        Assert.IsTrue(thread.IsToolRegistered("override_name"));

        // Unregister by override name should succeed
        var removedByOverride = thread.UnregisterTool("override_name");
        Assert.IsTrue(removedByOverride);
        Assert.AreEqual(0, thread.RegisteredToolNames.Count);
    }

    [TestMethod]
    public void UnregisterAllTools_ClearsAllTools()
    {
        var thread = new GrokThread(CreateClient());
        thread.RegisterTool(CreateDummyTool("tool_1"));
        thread.RegisterTool(CreateDummyTool("tool_2"));
        thread.RegisterTool(CreateDummyTool("tool_3"));

        Assert.AreEqual(3, thread.RegisteredToolNames.Count);

        thread.UnregisterAllTools();

        Assert.AreEqual(0, thread.RegisteredToolNames.Count);
        Assert.IsFalse(thread.IsToolRegistered("tool_1"));
        Assert.IsFalse(thread.IsToolRegistered("tool_2"));
        Assert.IsFalse(thread.IsToolRegistered("tool_3"));
    }

    [TestMethod]
    public void RegisterTool_AfterUnregister_CanReRegister()
    {
        var thread = new GrokThread(CreateClient());
        thread.RegisterTool(CreateDummyTool("reusable"));

        thread.UnregisterTool("reusable");
        Assert.IsFalse(thread.IsToolRegistered("reusable"));

        // Should not throw — slot is free
        thread.RegisterTool(CreateDummyTool("reusable"));
        Assert.IsTrue(thread.IsToolRegistered("reusable"));
    }

    [TestMethod]
    public void RegisteredToolNames_ReturnsCorrectNames()
    {
        var thread = new GrokThread(CreateClient());
        thread.RegisterTool(CreateDummyTool("alpha"));
        thread.RegisterTool(CreateDummyTool("beta"), nameOverride: "gamma");

        var names = thread.RegisteredToolNames.ToList();

        Assert.AreEqual(2, names.Count);
        CollectionAssert.Contains(names, "alpha");
        CollectionAssert.Contains(names, "gamma");
        CollectionAssert.DoesNotContain(names, "beta");
    }

    [TestMethod]
    public void IsToolRegistered_EmptyThread_ReturnsFalse()
    {
        var thread = new GrokThread(CreateClient());

        Assert.IsFalse(thread.IsToolRegistered("anything"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void RegisterTool_NullTool_Throws()
    {
        var thread = new GrokThread(CreateClient());
        thread.RegisterTool(null!);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void UnregisterTool_NullName_Throws()
    {
        var thread = new GrokThread(CreateClient());
        thread.UnregisterTool((string)null!);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void UnregisterTool_NullInstance_Throws()
    {
        var thread = new GrokThread(CreateClient());
        thread.UnregisterTool((IGrokTool)null!);
    }

    [TestMethod]
    public void DynamicToolSwap_RegisterUnregisterRegister()
    {
        // Simulates a real scenario: dynamically swapping tools mid-conversation
        var thread = new GrokThread(CreateClient());

        // Start with web search
        var webSearch = CreateDummyTool("web_search");
        thread.RegisterTool(webSearch);
        Assert.AreEqual(1, thread.RegisteredToolNames.Count);

        // Swap to code execution
        thread.UnregisterTool("web_search");
        var codeExec = CreateDummyTool("code_execution");
        thread.RegisterTool(codeExec);

        Assert.AreEqual(1, thread.RegisteredToolNames.Count);
        Assert.IsFalse(thread.IsToolRegistered("web_search"));
        Assert.IsTrue(thread.IsToolRegistered("code_execution"));

        // Add another alongside
        thread.RegisterTool(CreateDummyTool("reasoning"));
        Assert.AreEqual(2, thread.RegisteredToolNames.Count);

        // Clear all and start fresh
        thread.UnregisterAllTools();
        Assert.AreEqual(0, thread.RegisteredToolNames.Count);
    }
}
