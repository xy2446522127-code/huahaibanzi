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
        Assert.IsFalse(TryParse(tryParse, "A", out _), "Plain typing keys must not be captured globally.");
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

        object?[] dragArguments = ["{\"action\":\"beginDrag\",\"x\":125.5,\"y\":240.25}", null];
        Assert.AreEqual(true, tryParse.Invoke(null, dragArguments));
        Assert.AreEqual(125.5, ((double?)requestType.GetProperty("X")!.GetValue(dragArguments[1]))!.Value, 0.001);
        Assert.AreEqual(240.25, ((double?)requestType.GetProperty("Y")!.GetValue(dragArguments[1]))!.Value, 0.001);

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
            "ready", "hide", "resize", "copy", "togglePin", "toggleFavorite", "delete",
            "setRetentionDays", "clearOrdinary", "clearAll", "setTheme", "setOpacity",
            "setPetals", "setReduceMotion", "setClickDuration", "setRightDoubleClick",
            "setShortcut", "resetShortcut", "setExclusions", "openDataFolder", "setStartup",
            "setBackground", "beginDrag", "dragMove", "endDrag"
            , "setPanelScale", "setCheckUpdatesOnStartup", "checkUpdate", "installUpdate", "openRelease"
        ];

        foreach (var action in actions)
        {
            Assert.AreEqual(true, isSupported.Invoke(null, [action]), action);
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
