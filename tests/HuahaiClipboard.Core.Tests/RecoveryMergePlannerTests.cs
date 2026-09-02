using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Recovery;
using HuahaiClipboard.Core.Todo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class RecoveryMergePlannerTests
{
    [TestMethod]
    public void CreatePlan_KeepsBothHistoryPayloadsWhenTheSameIdHasDifferentContent()
    {
        var id = Guid.NewGuid();
        var destination = new RecoveryDataset(
            [CreateRecord(id, "current value", isFavorite: false, isPinned: true)],
            TodoWorkspace.Empty);
        var source = new RecoveryDataset(
            [CreateRecord(id, "recovered value", isFavorite: true, isPinned: false)],
            TodoWorkspace.Empty);

        var plan = new RecoveryMergePlanner().CreatePlan(source, destination);

        Assert.AreEqual(2, plan.History.Count);
        CollectionAssert.AreEquivalent(
            new[] { "current value", "recovered value" },
            plan.History.Select(record => record.PrimaryText).ToArray());
        Assert.AreEqual(1, plan.Conflicts.Count(conflict => conflict.Kind == RecoveryConflictKind.HistoryPayload));
        Assert.AreEqual(id, plan.History.Single(record => record.PrimaryText == "current value").Id);
        Assert.AreNotEqual(id, plan.History.Single(record => record.PrimaryText == "recovered value").Id);
    }

    [TestMethod]
    public void CreatePlan_ClonesConflictingNoteWithRecoverySuffix()
    {
        var destination = new RecoveryDataset(
            [],
            new TodoWorkspace(true, [], [new TodoNote("note-1", "项目笔记", "<p>当前</p>")]));
        var source = new RecoveryDataset(
            [],
            new TodoWorkspace(true, [], [new TodoNote("note-1", "项目笔记", "<p>恢复</p>")]));

        var plan = new RecoveryMergePlanner().CreatePlan(source, destination);

        Assert.AreEqual(2, plan.Workspace.Notes.Count);
        Assert.IsTrue(plan.Workspace.Notes.Any(note => note.Id == "note-1" && note.Html == "<p>当前</p>"));
        Assert.IsTrue(plan.Workspace.Notes.Any(note => note.Id != "note-1" && note.Title == "项目笔记（恢复副本）" && note.Html == "<p>恢复</p>"));
        Assert.AreEqual(1, plan.Conflicts.Count(conflict => conflict.Kind == RecoveryConflictKind.NoteContent));
    }

    private static ClipboardRecord CreateRecord(Guid id, string text, bool isFavorite, bool isPinned) =>
        new(
            id,
            ClipboardItemKind.Text,
            text,
            "fixture",
            DateTimeOffset.Parse("2026-09-02T12:00:00+08:00"),
            isFavorite,
            isPinned,
            true,
            null);
}
