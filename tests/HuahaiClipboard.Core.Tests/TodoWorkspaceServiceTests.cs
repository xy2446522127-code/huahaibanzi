using HuahaiClipboard.Core.Todo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class TodoWorkspaceServiceTests
{
    [TestMethod]
    public async Task MoveTodoAsync_InsertsBeforeOrAfterTheDropTarget()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-todo-service-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonTodoWorkspaceStore(Path.Combine(directory, "todo.json"));
            await store.SaveAsync(new TodoWorkspace(true,
            [
                new TodoItem("a", "A", false, 1),
                new TodoItem("b", "B", false, 2),
                new TodoItem("c", "C", false, 3)
            ], []));
            var service = new TodoWorkspaceService(store);

            await service.MoveTodoAsync("c", "a", insertAfterTarget: false);
            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, (await service.LoadAsync()).Todos.Select(todo => todo.Id).ToArray());

            await service.MoveTodoAsync("c", "a", insertAfterTarget: true);
            CollectionAssert.AreEqual(new[] { "a", "c", "b" }, (await service.LoadAsync()).Todos.Select(todo => todo.Id).ToArray());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task AddAndUpdateNoteAsync_PreservesTextAndCapsuleSetting()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-todo-service-{Guid.NewGuid():N}");
        try
        {
            var service = new TodoWorkspaceService(new JsonTodoWorkspaceStore(Path.Combine(directory, "todo.json")));

            var todo = await service.AddTodoAsync("准备发布");
            var note = await service.AddNoteAsync("发布笔记");
            await service.UpdateNoteAsync(note.Id, "发布笔记", "<p>检查安装包</p>");
            await service.SetCapsuleEnabledAsync(false);
            await service.ToggleTodoAsync(todo.Id);

            var workspace = await service.LoadAsync();
            Assert.IsFalse(workspace.CapsuleEnabled);
            Assert.IsTrue(workspace.Todos.Single().IsCompleted);
            Assert.AreEqual("<p>检查安装包</p>", workspace.Notes.Single().Html);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
