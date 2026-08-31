using HuahaiClipboard.Core.Todo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class TodoWorkspaceStoreTests
{
    [TestMethod]
    public async Task LoadAsync_WhenWorkspaceDoesNotExist_ReturnsCapsuleEnabledEmptyWorkspace()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-todo-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonTodoWorkspaceStore(Path.Combine(directory, "todo-workspace.json"));

            var workspace = await store.LoadAsync();

            Assert.IsTrue(workspace.CapsuleEnabled);
            Assert.AreEqual(0, workspace.Todos.Count);
            Assert.AreEqual(0, workspace.Notes.Count);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAsync_NormalizesTodoOrderAndRoundTripsNotes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-todo-{Guid.NewGuid():N}");
        try
        {
            var file = Path.Combine(directory, "todo-workspace.json");
            var store = new JsonTodoWorkspaceStore(file);
            var workspace = new TodoWorkspace(
                CapsuleEnabled: false,
                Todos:
                [
                    new TodoItem("later", "第二项", false, 8),
                    new TodoItem("first", "第一项", true, 2)
                ],
                Notes: [new TodoNote("note-1", "笔记", "<p>保留文字</p>")]);

            await store.SaveAsync(workspace);
            var restored = await store.LoadAsync();

            Assert.IsFalse(restored.CapsuleEnabled);
            CollectionAssert.AreEqual(new[] { "first", "later" }, restored.Todos.Select(todo => todo.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, restored.Todos.Select(todo => todo.SortOrder).ToArray());
            Assert.AreEqual("<p>保留文字</p>", restored.Notes.Single().Html);
            Assert.IsFalse(File.Exists(file + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenStoredJsonIsInvalid_ReturnsSafeDefaultWorkspace()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-todo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "todo-workspace.json");
        await File.WriteAllTextAsync(file, "not-json");
        try
        {
            var workspace = await new JsonTodoWorkspaceStore(file).LoadAsync();

            Assert.IsTrue(workspace.CapsuleEnabled);
            Assert.AreEqual(0, workspace.Todos.Count);
            Assert.AreEqual(0, workspace.Notes.Count);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
