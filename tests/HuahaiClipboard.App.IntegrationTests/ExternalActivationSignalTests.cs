using HuahaiClipboard.App.Infrastructure.Activation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.App.IntegrationTests;

[TestClass]
public sealed class ExternalActivationSignalTests
{
    [TestMethod]
    public async Task SetFromAnotherHandle_InvokesTheRegisteredActivationCallback()
    {
        var eventName = $@"Local\HuahaiClipboard.Tests.{Guid.NewGuid():N}";
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var signal = new ExternalActivationSignal(eventName, () => activated.TrySetResult());
        using var sender = EventWaitHandle.OpenExisting(eventName);

        sender.Set();

        await activated.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
