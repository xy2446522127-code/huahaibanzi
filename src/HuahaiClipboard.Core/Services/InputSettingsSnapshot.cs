using HuahaiClipboard.Core.Settings;

namespace HuahaiClipboard.Core.Services;

public sealed class InputSettingsSnapshot(InputSettings initialSettings)
{
    private InputSettings current = initialSettings;

    public InputSettings Current => Volatile.Read(ref current);

    public void Update(InputSettings settings) => Volatile.Write(ref current, settings);
}
