using HuahaiClipboard.Core.Models;
using HuahaiClipboard.NativeUiSpike.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class NativeUiSpikeViewModelTests
{
    [TestMethod]
    public void Fixture_ContainsExactlyOneThousandStableRecordsAcrossFourKinds()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(1000);

        Assert.AreEqual(1000, model.AllItems.Count);
        Assert.AreEqual("fixture-0001", model.AllItems[0].StableId);
        Assert.AreEqual("fixture-1000", model.AllItems[^1].StableId);
        CollectionAssert.AreEquivalent(
            new[] { ClipboardItemKind.Text, ClipboardItemKind.Link, ClipboardItemKind.Image, ClipboardItemKind.File },
            model.AllItems.Take(4).Select(item => item.Kind).ToArray());
    }

    [TestMethod]
    public void Fixture_UsesTheApprovedDistinctGlyphForEachClipboardKind()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(4);

        CollectionAssert.AreEqual(
            new[] { "\uE8D2", "\uE71B", "\uE8B9", "\uE8A5" },
            model.AllItems.Select(item => item.KindGlyph).ToArray());
    }

    [TestMethod]
    public void PinFavoriteDeleteAndFilter_ChangeObservableState()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(12);
        var target = model.AllItems[3];

        model.TogglePinned(target.Id);
        model.ToggleFavorite(target.Id);
        model.SelectFilter(ClipboardFilter.Favorites);

        Assert.IsTrue(target.IsPinned);
        Assert.IsTrue(target.IsFavorite);
        Assert.AreEqual(1, model.VisibleItems.Count);
        Assert.AreSame(target, model.VisibleItems[0]);

        model.Delete(target.Id);

        Assert.AreEqual(0, model.VisibleItems.Count);
        Assert.AreEqual(11, model.AllItems.Count);
    }

    [TestMethod]
    public void UnknownTheme_IsRejectedWithoutChangingCurrentTheme()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(1);

        Assert.IsFalse(model.SetTheme("not-a-theme"));
        Assert.AreEqual("rose-purple", model.ThemeId);
    }

    [TestMethod]
    public void SearchAndKindFilter_UseVisibleUserFacingContent()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(20);

        model.SearchText = "示例链接 0006";
        model.SelectFilter(ClipboardFilter.Link);

        Assert.AreEqual(1, model.VisibleItems.Count);
        Assert.AreEqual("fixture-0006", model.VisibleItems[0].StableId);
    }

    [TestMethod]
    public void ClearOrdinary_PreservesPinnedAndFavoriteItems()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(8);
        model.TogglePinned(model.AllItems[1].Id);
        model.ToggleFavorite(model.AllItems[4].Id);

        var removed = model.ClearOrdinary();

        Assert.AreEqual(6, removed);
        Assert.AreEqual(2, model.AllItems.Count);
        Assert.IsTrue(model.AllItems.Any(item => item.IsPinned));
        Assert.IsTrue(model.AllItems.Any(item => item.IsFavorite));
    }

    [TestMethod]
    public void ClearAll_RemovesPinnedAndFavoriteItems()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(4);
        model.TogglePinned(model.AllItems[0].Id);
        model.ToggleFavorite(model.AllItems[1].Id);

        var removed = model.ClearAll();

        Assert.AreEqual(4, removed);
        Assert.AreEqual(0, model.AllItems.Count);
        Assert.AreEqual(0, model.VisibleItems.Count);
    }

    [TestMethod]
    public void RetentionPeriod_AcceptsOnlyApprovedValues()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(1);

        Assert.IsTrue(model.SetRetentionDays(30));
        Assert.AreEqual(30, model.RetentionDays);
        Assert.IsFalse(model.SetRetentionDays(14));
        Assert.AreEqual(30, model.RetentionDays);
    }

    [TestMethod]
    public void HeaderSummary_TracksVisibleRecordsAndRetentionPeriodInRealTime()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(12);
        var changedProperties = new List<string?>();
        model.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert.AreEqual("最近 7 天 · 12 条", model.HeaderSummary);

        model.SelectFilter(ClipboardFilter.Text);
        Assert.AreEqual("最近 7 天 · 3 条", model.HeaderSummary);

        model.SearchText = "示例文本 0001";
        Assert.AreEqual("最近 7 天 · 1 条", model.HeaderSummary);

        model.Delete(model.VisibleItems.Single().Id);
        Assert.AreEqual("最近 7 天 · 0 条", model.HeaderSummary);

        Assert.IsTrue(model.SetRetentionDays(30));
        Assert.AreEqual("最近 30 天 · 0 条", model.HeaderSummary);
        Assert.IsTrue(changedProperties.Count(name => name == nameof(model.HeaderSummary)) >= 4);
    }
}
