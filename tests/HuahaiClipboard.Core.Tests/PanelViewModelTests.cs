using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.App.Infrastructure.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class PanelViewModelTests
{
    [TestMethod]
    public void ClipboardRecord_PreservesEveryApprovedRecordState()
    {
        var record = new ClipboardRecord(
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            ClipboardItemKind.File,
            @"C:\资料\花海.txt",
            "文件不可用",
            DateTimeOffset.Parse("2026-08-04T09:00:00+08:00"),
            IsFavorite: true,
            IsPinned: true,
            IsAvailable: false,
            PreviewAssetPath: null);

        Assert.AreEqual(Guid.Parse("00000000-0000-0000-0000-000000000004"), record.Id);
        Assert.AreEqual(ClipboardItemKind.File, record.Kind);
        Assert.AreEqual(@"C:\资料\花海.txt", record.PrimaryText);
        Assert.AreEqual("文件不可用", record.SecondaryText);
        Assert.AreEqual(DateTimeOffset.Parse("2026-08-04T09:00:00+08:00"), record.LastCopiedAt);
        Assert.IsTrue(record.IsFavorite);
        Assert.IsTrue(record.IsPinned);
        Assert.IsFalse(record.IsAvailable);
        Assert.IsNull(record.PreviewAssetPath);
    }

    [TestMethod]
    public void ClipboardFilter_ExposesEveryApprovedFilter()
    {
        string[] expected = ["All", "Text", "Link", "Image", "File", "Favorites"];

        CollectionAssert.AreEqual(expected, Enum.GetNames<ClipboardFilter>());
    }

    [TestMethod]
    public void PanelActionResult_FactoriesExposeSuccessAndRecoveryState()
    {
        var success = PanelActionResult.Success();
        var failure = PanelActionResult.Failure("请手动粘贴");

        Assert.IsTrue(success.Succeeded);
        Assert.IsNull(success.RecoveryMessage);
        Assert.IsFalse(failure.Succeeded);
        Assert.AreEqual("请手动粘贴", failure.RecoveryMessage);
    }

    [TestMethod]
    public void CoreContracts_ExposeTheApprovedMembers()
    {
        AssertMethod<IClipboardHistorySource>(
            "GetAllAsync",
            typeof(Task<IReadOnlyList<ClipboardRecord>>),
            typeof(CancellationToken));
        AssertMethod<IClipboardHistorySource>(
            "SetFavoriteAsync",
            typeof(Task),
            typeof(Guid),
            typeof(bool),
            typeof(CancellationToken));
        AssertMethod<IClipboardHistorySource>(
            "SetPinnedAsync",
            typeof(Task),
            typeof(Guid),
            typeof(bool),
            typeof(CancellationToken));
        AssertMethod<IClipboardHistorySource>(
            "DeleteAsync",
            typeof(Task),
            typeof(Guid),
            typeof(CancellationToken));
        AssertMethod<IClipboardHistorySource>(
            "ClearAsync",
            typeof(Task),
            typeof(CancellationToken));
        Assert.AreEqual(5, typeof(IClipboardHistorySource).GetMethods().Length);

        AssertMethod<IPanelActionSink>(
            "CopyAsync",
            typeof(Task<PanelActionResult>),
            typeof(Guid),
            typeof(CancellationToken));
        AssertMethod<IPanelActionSink>(
            "PasteAsync",
            typeof(Task<PanelActionResult>),
            typeof(Guid),
            typeof(CancellationToken));
        Assert.AreEqual(2, typeof(IPanelActionSink).GetMethods().Length);

        AssertMethod<IWindowNavigator>("ShowCursorPanel", typeof(void));
        AssertMethod<IWindowNavigator>("ShowEdgePanel", typeof(void));
        AssertMethod<IWindowNavigator>("ShowSettings", typeof(void));
        AssertMethod<IWindowNavigator>("HideTransientPanel", typeof(void));
        Assert.AreEqual(4, typeof(IWindowNavigator).GetMethods().Length);
    }

    [TestMethod]
    public async Task MockHistorySource_ReturnsTheTwelveStableShellRecords()
    {
        var source = new MockClipboardHistorySource();

        var records = await source.GetAllAsync(CancellationToken.None);

        string[] expectedIds =
        [
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            "00000000-0000-0000-0000-000000000003",
            "00000000-0000-0000-0000-000000000004",
            "00000000-0000-0000-0000-000000000005",
            "00000000-0000-0000-0000-000000000006",
            "00000000-0000-0000-0000-000000000007",
            "00000000-0000-0000-0000-000000000008",
            "00000000-0000-0000-0000-000000000009",
            "00000000-0000-0000-0000-000000000010",
            "00000000-0000-0000-0000-000000000011",
            "00000000-0000-0000-0000-000000000012"
        ];
        DateTimeOffset[] expectedTimes =
        [
            DateTimeOffset.Parse("2026-08-04T09:00:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T08:58:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T08:55:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T08:51:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T08:46:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T08:39:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T08:26:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T08:13:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T08:00:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T07:00:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T06:00:00+08:00"),
            DateTimeOffset.Parse("2026-08-04T05:00:00+08:00")
        ];

        Assert.AreEqual(12, records.Count);
        CollectionAssert.AreEqual(expectedIds, records.Select(record => record.Id.ToString()).ToArray());
        CollectionAssert.AreEqual(expectedTimes, records.Select(record => record.LastCopiedAt).ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ClipboardItemKind>(),
            records.Select(record => record.Kind).Distinct().ToArray());
        Assert.IsTrue(records.Any(record => record.IsFavorite));
        Assert.IsTrue(records.Any(record => record.IsPinned));
        Assert.IsTrue(records.Any(record => !record.IsAvailable));
    }

    [TestMethod]
    public async Task MockHistorySource_MutationsPersistWithoutChangingEarlierSnapshots()
    {
        var source = new MockClipboardHistorySource();
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var originalSnapshot = await source.GetAllAsync(CancellationToken.None);

        await source.SetFavoriteAsync(firstId, false, CancellationToken.None);
        await source.SetPinnedAsync(firstId, false, CancellationToken.None);
        await source.DeleteAsync(secondId, CancellationToken.None);

        var changedSnapshot = await source.GetAllAsync(CancellationToken.None);
        var originalFirst = originalSnapshot.Single(record => record.Id == firstId);
        var changedFirst = changedSnapshot.Single(record => record.Id == firstId);
        Assert.IsTrue(originalFirst.IsFavorite);
        Assert.IsTrue(originalFirst.IsPinned);
        Assert.IsFalse(changedFirst.IsFavorite);
        Assert.IsFalse(changedFirst.IsPinned);
        Assert.AreEqual(11, changedSnapshot.Count);
        Assert.IsFalse(changedSnapshot.Any(record => record.Id == secondId));

        await source.ClearAsync(CancellationToken.None);

        Assert.AreEqual(0, (await source.GetAllAsync(CancellationToken.None)).Count);
        Assert.AreEqual(11, changedSnapshot.Count);
    }

    [TestMethod]
    public async Task MockHistorySource_SerializesConcurrentMutations()
    {
        var source = new MockClipboardHistorySource();
        var records = await source.GetAllAsync(CancellationToken.None);

        var mutations = records.SelectMany(record => new Task[]
        {
            Task.Run(() => source.SetFavoriteAsync(record.Id, true, CancellationToken.None)),
            Task.Run(() => source.SetPinnedAsync(record.Id, true, CancellationToken.None))
        });
        await Task.WhenAll(mutations);

        var changedRecords = await source.GetAllAsync(CancellationToken.None);
        Assert.IsTrue(changedRecords.All(record => record.IsFavorite));
        Assert.IsTrue(changedRecords.All(record => record.IsPinned));
    }

    [TestMethod]
    public async Task MockHistorySource_RejectsMissingRecords()
    {
        var source = new MockClipboardHistorySource();
        var missingId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        await Assert.ThrowsExactlyAsync<KeyNotFoundException>(
            () => source.SetFavoriteAsync(missingId, true, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<KeyNotFoundException>(
            () => source.SetPinnedAsync(missingId, true, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<KeyNotFoundException>(
            () => source.DeleteAsync(missingId, CancellationToken.None));
    }

    [TestMethod]
    public async Task MockAdapters_HonorCancellationBeforeAnyOperation()
    {
        var source = new MockClipboardHistorySource();
        var sink = new MockPanelActionSink();
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var cancellationToken = new CancellationToken(canceled: true);
        Func<Task>[] operations =
        [
            async () => await source.GetAllAsync(cancellationToken),
            () => source.SetFavoriteAsync(id, true, cancellationToken),
            () => source.SetPinnedAsync(id, true, cancellationToken),
            () => source.DeleteAsync(id, cancellationToken),
            () => source.ClearAsync(cancellationToken),
            async () => await sink.CopyAsync(id, cancellationToken),
            async () => await sink.PasteAsync(id, cancellationToken)
        ];

        foreach (var operation in operations)
        {
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(operation);
        }

        Assert.AreEqual(12, (await source.GetAllAsync(CancellationToken.None)).Count);
    }

    [TestMethod]
    public async Task MockPanelActionSink_ExposesSuccessRecoveryAndMissingStates()
    {
        var sink = new MockPanelActionSink();
        var successId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var manualPasteId = Guid.Parse("00000000-0000-0000-0000-000000000012");
        var missingId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        var copySuccess = await sink.CopyAsync(successId, CancellationToken.None);
        var pasteSuccess = await sink.PasteAsync(successId, CancellationToken.None);
        var copyRecovery = await sink.CopyAsync(manualPasteId, CancellationToken.None);
        var pasteRecovery = await sink.PasteAsync(manualPasteId, CancellationToken.None);
        var copyMissing = await sink.CopyAsync(missingId, CancellationToken.None);
        var pasteMissing = await sink.PasteAsync(missingId, CancellationToken.None);

        Assert.AreEqual(PanelActionResult.Success(), copySuccess);
        Assert.AreEqual(PanelActionResult.Success(), pasteSuccess);
        Assert.AreEqual(
            PanelActionResult.Failure("已复制，请按 Ctrl+V 手动粘贴"),
            copyRecovery);
        Assert.AreEqual(
            PanelActionResult.Failure("已复制，请按 Ctrl+V 手动粘贴"),
            pasteRecovery);
        Assert.AreEqual(PanelActionResult.Failure("记录不存在或已删除"), copyMissing);
        Assert.AreEqual(PanelActionResult.Failure("记录不存在或已删除"), pasteMissing);
    }

    [TestMethod]
    public async Task SearchAndFilter_ComposeWithoutChangingSourceOrder()
    {
        var viewModel = CreatePanelViewModel();
        await viewModel.LoadAsync();
        var originalOrder = viewModel.AllRecords.Select(record => record.Id).ToArray();

        viewModel.SelectedFilter = ClipboardFilter.Link;
        viewModel.SearchText = "  OPENAI  ";

        Assert.AreEqual(1, viewModel.VisibleRecords.Count);
        Assert.AreEqual(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            viewModel.VisibleRecords[0].Id);
        CollectionAssert.AreEqual(
            originalOrder,
            viewModel.AllRecords.Select(record => record.Id).ToArray());
    }

    [TestMethod]
    public async Task Load_SortsPinnedRecordsFirstAndSelectsTheFirstVisibleRecord()
    {
        var viewModel = CreatePanelViewModel();

        await viewModel.LoadAsync();

        string[] expectedLeadingIds =
        [
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000003",
            "00000000-0000-0000-0000-000000000004",
            "00000000-0000-0000-0000-000000000010"
        ];
        CollectionAssert.AreEqual(
            expectedLeadingIds,
            viewModel.VisibleRecords.Take(4).Select(record => record.Id.ToString()).ToArray());
        Assert.AreEqual(viewModel.VisibleRecords[0], viewModel.SelectedRecord);
        Assert.IsFalse(viewModel.IsLoading);
        Assert.IsFalse(viewModel.IsEmpty);
    }

    [TestMethod]
    public async Task CopyAndPaste_OnlyHideThePanelAfterSuccessfulActions()
    {
        var navigator = new RecordingWindowNavigator();
        var viewModel = CreatePanelViewModel(navigator);
        await viewModel.LoadAsync();
        var successRecord = viewModel.AllRecords.Single(
            record => record.Id == Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var recoveryRecord = viewModel.AllRecords.Single(
            record => record.Id == Guid.Parse("00000000-0000-0000-0000-000000000012"));

        await viewModel.CopyAsync(successRecord);
        Assert.AreEqual(1, navigator.HideTransientPanelCalls);
        Assert.IsNull(viewModel.RecoveryMessage);

        await viewModel.PasteAsync(recoveryRecord);
        Assert.AreEqual(1, navigator.HideTransientPanelCalls);
        Assert.AreEqual("已复制，请按 Ctrl+V 手动粘贴", viewModel.RecoveryMessage);
        Assert.IsFalse(viewModel.IsBusy);
    }

    [TestMethod]
    public async Task RecordCommands_RefreshStateAndKeepSelectionUsable()
    {
        var navigator = new RecordingWindowNavigator();
        var viewModel = CreatePanelViewModel(navigator);
        await viewModel.LoadAsync();
        var first = viewModel.SelectedRecord!;

        await viewModel.ToggleFavoriteAsync(first);
        await viewModel.TogglePinnedAsync(first);
        Assert.IsFalse(viewModel.AllRecords.Single(record => record.Id == first.Id).IsFavorite);
        Assert.IsFalse(viewModel.AllRecords.Single(record => record.Id == first.Id).IsPinned);

        viewModel.MoveSelection(1);
        var selectedAfterMove = viewModel.SelectedRecord;
        Assert.IsNotNull(selectedAfterMove);
        Assert.AreNotEqual(first.Id, selectedAfterMove.Id);

        await viewModel.DeleteAsync(selectedAfterMove);
        Assert.IsFalse(viewModel.AllRecords.Any(record => record.Id == selectedAfterMove.Id));
        Assert.IsNotNull(viewModel.SelectedRecord);

        viewModel.Close();
        Assert.AreEqual(1, navigator.HideTransientPanelCalls);
    }

    [TestMethod]
    public async Task ClearedHistory_LoadsAsAnEmptyPanel()
    {
        var source = new MockClipboardHistorySource();
        await source.ClearAsync(CancellationToken.None);
        var viewModel = new PanelViewModel(
            source,
            new MockPanelActionSink(),
            new RecordingWindowNavigator());

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.IsEmpty);
        Assert.AreEqual(0, viewModel.VisibleRecords.Count);
        Assert.IsNull(viewModel.SelectedRecord);
    }

    private static PanelViewModel CreatePanelViewModel(IWindowNavigator? navigator = null) =>
        new(
            new MockClipboardHistorySource(),
            new MockPanelActionSink(),
            navigator ?? new RecordingWindowNavigator());

    private static void AssertMethod<TContract>(
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        var method = typeof(TContract).GetMethod(name, parameterTypes);

        Assert.IsNotNull(method, $"{typeof(TContract).Name}.{name} has the approved parameters.");
        Assert.AreEqual(returnType, method.ReturnType);
    }

    private sealed class RecordingWindowNavigator : IWindowNavigator
    {
        public int HideTransientPanelCalls { get; private set; }

        public void ShowCursorPanel()
        {
        }

        public void ShowEdgePanel()
        {
        }

        public void ShowSettings()
        {
        }

        public void HideTransientPanel() => HideTransientPanelCalls++;
    }
}
