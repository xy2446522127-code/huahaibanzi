using System.Collections.Specialized;
using System.Runtime.InteropServices;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using FormsClipboard = System.Windows.Forms.Clipboard;

namespace HuahaiClipboard.App.Infrastructure.Clipboard;

public sealed class WindowsClipboardPlatform(IClipboardImageStore imageStore) : IClipboardPlatform
{
    private IntPtr pasteTarget;

    public void SetPasteTarget(IntPtr windowHandle) => pasteTarget = windowHandle;

    public async Task WriteAsync(ClipboardRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                switch (record.Kind)
                {
                    case ClipboardItemKind.File:
                        var files = new StringCollection();
                        files.AddRange(record.PrimaryText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                        FormsClipboard.SetFileDropList(files);
                        break;
                    case ClipboardItemKind.Image when !string.IsNullOrWhiteSpace(record.PreviewAssetPath):
                        var imageBytes = await imageStore.ReadAsync(record.PreviewAssetPath, cancellationToken);
                        using (var stream = new MemoryStream(imageBytes, writable: false))
                        using (var sourceImage = global::System.Drawing.Image.FromStream(stream))
                        using (var image = new global::System.Drawing.Bitmap(sourceImage))
                        {
                            FormsClipboard.SetImage(image);
                        }
                        break;
                    default:
                        FormsClipboard.SetText(record.PrimaryText);
                        break;
                }

                return;
            }
            catch (ExternalException) when (attempt < 4)
            {
                await Task.Delay(30 * (attempt + 1), cancellationToken);
            }
        }
    }

    public async Task<bool> PasteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = pasteTarget;
        if (target == IntPtr.Zero || !IsWindow(target))
        {
            return false;
        }

        ShowWindow(target, 9);
        if (!SetForegroundWindow(target))
        {
            return false;
        }

        await Task.Delay(80, cancellationToken);
        INPUT[] inputs =
        [
            INPUT.Keyboard(0x11, keyUp: false),
            INPUT.Keyboard(0x56, keyUp: false),
            INPUT.Keyboard(0x56, keyUp: true),
            INPUT.Keyboard(0x11, keyUp: true)
        ];
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion data;

        public static INPUT Keyboard(ushort key, bool keyUp) => new()
        {
            type = 1,
            data = new InputUnion
            {
                keyboard = new KEYBDINPUT
                {
                    virtualKey = key,
                    flags = keyUp ? 0x0002u : 0u
                }
            }
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public HARDWAREINPUT hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort virtualKey;
        public ushort scanCode;
        public uint flags;
        public uint time;
        public IntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint message;
        public ushort parameterLow;
        public ushort parameterHigh;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
