using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class UnmanagedCallbackGuardTests
{
    [TestMethod]
    public void Invoke_WhenCallbackThrows_ReturnsFallbackResult()
    {
        var fallbackCalls = 0;

        var result = UnmanagedCallbackGuard.Invoke<int>(
            () => throw new InvalidOperationException("protected process"),
            () =>
            {
                fallbackCalls++;
                return 42;
            });

        Assert.AreEqual(42, result);
        Assert.AreEqual(1, fallbackCalls);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenCallbackThrows_ReportsTheFailureWithoutRethrowing()
    {
        Exception? reported = null;

        await UnmanagedCallbackGuard.InvokeAsync(
            () => Task.FromException(new IOException("clipboard cache unavailable")),
            exception => reported = exception);

        Assert.IsInstanceOfType<IOException>(reported);
        Assert.AreEqual("clipboard cache unavailable", reported.Message);
    }
}
