using HuahaiClipboard.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ShellIntegrationPolicyTests
{
    [TestMethod]
    public void ShortcutGestureParser_AcceptsModifierFunctionAndMouseGestures()
    {
        var parser = typeof(InputSettings).Assembly.GetType(
            "HuahaiClipboard.Core.Services.ShortcutGestureParser");
        Assert.IsNotNull(parser);
        var tryParse = parser.GetMethod("TryParse");
        Assert.IsNotNull(tryParse);

        AssertGesture(tryParse, "Ctrl + Alt + H", "Keyboard", 0x0003u, 0x48u);
        AssertGesture(tryParse, "F8", "Keyboard", 0u, 0x77u);
        AssertGesture(tryParse, "鼠标中键", "MiddleMouse", 0u, 0u);
        AssertGesture(tryParse, "鼠标侧键 1", "XButton1", 0u, 0u);
        AssertGesture(tryParse, "鼠标滚轮上", "WheelUp", 0u, 0u);
        AssertGesture(tryParse, "鼠标滚轮下", "WheelDown", 0u, 0u);
        AssertGesture(tryParse, "Ctrl + 鼠标左键", "LeftMouse", 0x0002u, 0u);
        AssertGesture(tryParse, "Alt + 鼠标右键", "RightMouse", 0x0001u, 0u);
        AssertGesture(tryParse, "Ctrl + Numpad1", "Keyboard", 0x0002u, 0x61u);
        AssertGesture(tryParse, "Ctrl + MediaPlayPause", "Keyboard", 0x0002u, 0xB3u);
        Assert.IsFalse(TryParse(tryParse, "A", out _), "Plain typing keys must not be captured globally.");
        Assert.IsFalse(TryParse(tryParse, "鼠标左键", out _), "Bare primary clicks must not replace normal Windows input.");
    }

    [TestMethod]
    public void StartupPolicies_QuoteTheExecutableAndRecognizeBackgroundLaunch()
    {
        var assembly = typeof(InputSettings).Assembly;
        var commandType = assembly.GetType("HuahaiClipboard.Core.Services.StartupCommand");
        var launchPolicyType = assembly.GetType("HuahaiClipboard.Core.Services.StartupLaunchPolicy");
        Assert.IsNotNull(commandType);
        Assert.IsNotNull(launchPolicyType);

        var command = commandType.GetMethod("Create")!.Invoke(
            null,
            [@"C:\Program Files\花海剪贴板\HuahaiClipboard.App.exe"]);
        Assert.AreEqual(
            "\"C:\\Program Files\\花海剪贴板\\HuahaiClipboard.App.exe\" --background",
            command);
        Assert.AreEqual(true, launchPolicyType.GetMethod("ShouldStartHidden")!.Invoke(null, ["--background"]));
        Assert.AreEqual(false, launchPolicyType.GetMethod("ShouldStartHidden")!.Invoke(null, [""]));
    }

    [TestMethod]
    public void WebBridgeRequest_ParsesControlPayloadsAndRejectsMissingActions()
    {
        var requestType = typeof(InputSettings).Assembly.GetType(
            "HuahaiClipboard.Core.Services.WebBridgeRequest");
        Assert.IsNotNull(requestType);
        var tryParse = requestType.GetMethod("TryParse");
        Assert.IsNotNull(tryParse);

        object?[] exclusionsArguments =
        [
            "{\"action\":\"setExclusions\",\"values\":[\"1Password.exe\",\"KeePass.exe\"]}",
            null
        ];
        Assert.AreEqual(true, tryParse.Invoke(null, exclusionsArguments));
        var exclusions = exclusionsArguments[1];
        Assert.IsNotNull(exclusions);
        Assert.AreEqual("setExclusions", requestType.GetProperty("Action")!.GetValue(exclusions));
        CollectionAssert.AreEqual(
            new[] { "1Password.exe", "KeePass.exe" },
            (string[])requestType.GetProperty("Values")!.GetValue(exclusions)!);

        object?[] opacityArguments = ["{\"action\":\"setOpacity\",\"number\":0.82}", null];
        Assert.AreEqual(true, tryParse.Invoke(null, opacityArguments));
        Assert.AreEqual(
            0.82,
            ((double?)requestType.GetProperty("Number")!.GetValue(opacityArguments[1]))!.Value,
            0.001);

        object?[] dragArguments = ["{\"action\":\"beginSystemDrag\"}", null];
        Assert.AreEqual(true, tryParse.Invoke(null, dragArguments));
        Assert.AreEqual("beginSystemDrag", requestType.GetProperty("Action")!.GetValue(dragArguments[1]));

        object?[] invalidArguments = ["{}", null];
        Assert.AreEqual(false, tryParse.Invoke(null, invalidArguments));
        Assert.IsNull(invalidArguments[1]);
    }

    [TestMethod]
    public void WebBridgeProtocol_RecognizesEveryNativeControlAndRejectsUnknownActions()
    {
        var protocolType = typeof(InputSettings).Assembly.GetType(
            "HuahaiClipboard.Core.Services.WebBridgeProtocol");
        Assert.IsNotNull(protocolType);
        var isSupported = protocolType.GetMethod("IsSupported");
        Assert.IsNotNull(isSupported);

        string[] actions =
        [
            "ready", "hide", "resize", "copy", "requestThumbnail", "togglePin", "toggleFavorite", "delete",
            "openPreview", "previewHover", "previewHoverEnd", "previewReady", "savePreview", "discardPreview",
            "previewCopy", "previewDirty", "previewFocus", "previewPointer", "previewTopmost", "previewAutoHide",
            "previewHide", "previewClose",
            "setRetentionDays", "setAutoCleanupCountEnabled", "setAutoCleanupCount", "clearOrdinary", "clearAll", "setTheme", "setOpacity",
            "setPetals", "setReduceMotion", "setClickDuration", "setRightDoubleClick",
            "setShortcut", "setPreviewShortcut", "resetShortcut", "setExclusions", "openDataFolder", "setStartup",
            "setBackground", "setOutsideAutoHide", "beginNativeDrag"
            , "previewPanelScale", "commitPanelScale", "cancelPanelScale", "setPanelScale", "setCheckUpdatesOnStartup", "checkUpdate", "snoozeUpdate", "installUpdate", "openRelease",
            "openTodoWorkspace", "todoReady", "todoAdd", "todoToggle", "todoDelete", "todoMove", "todoAddNote", "todoUpdateNote", "todoDeleteNote", "todoSetCapsule", "todoCollapse", "todoRestore", "todoClose", "todoTopmost"
        ];

        foreach (var action in actions)
        {
            Assert.AreEqual(true, isSupported.Invoke(null, [action]), action);
        }

        foreach (var obsoleteDragAction in new[] { "beginDrag", "dragMove", "endDrag", "beginSystemDrag" })
        {
            Assert.AreEqual(false, isSupported.Invoke(null, [obsoleteDragAction]), obsoleteDragAction);
        }

        Assert.AreEqual(false, isSupported.Invoke(null, ["formatDisk"]));
    }

    private static void AssertGesture(
        System.Reflection.MethodInfo tryParse,
        string value,
        string expectedKind,
        uint expectedModifiers,
        uint expectedVirtualKey)
    {
        Assert.IsTrue(TryParse(tryParse, value, out var gesture));
        Assert.IsNotNull(gesture);
        var type = gesture.GetType();
        Assert.AreEqual(expectedKind, type.GetProperty("Kind")!.GetValue(gesture)!.ToString());
        Assert.AreEqual(expectedModifiers, type.GetProperty("Modifiers")!.GetValue(gesture));
        Assert.AreEqual(expectedVirtualKey, type.GetProperty("VirtualKey")!.GetValue(gesture));
    }

    private static bool TryParse(
        System.Reflection.MethodInfo tryParse,
        string value,
        out object? gesture)
    {
        object?[] arguments = [value, null];
        var result = (bool)tryParse.Invoke(null, arguments)!;
        gesture = arguments[1];
        return result;
    }
}
