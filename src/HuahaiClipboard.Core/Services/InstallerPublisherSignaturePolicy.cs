using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HuahaiClipboard.Core.Services
{
public static class InstallerPublisherSignaturePolicy
{
    public const string PinnedPublisherThumbprint = "CD06B727BD8811C3B59CE0A4F9384D68EC7431C2";

    private const uint ErrorSuccess = 0x00000000;
    private const uint CertEUntrustedRoot = 0x800B0109;
    private static readonly Guid VerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public static void Verify(string installerPath, string expectedThumbprint)
    {
        if (String.IsNullOrWhiteSpace(installerPath)) throw new ArgumentException("Installer path is required.", "installerPath");
        if (String.IsNullOrWhiteSpace(expectedThumbprint)) throw new ArgumentException("Publisher thumbprint is required.", "expectedThumbprint");

        var resolvedPath = Path.GetFullPath(installerPath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                "The installer publisher signature could not be verified because the file does not exist.",
                resolvedPath);
        }

        var trustStatus = VerifyAuthenticode(resolvedPath);
        if (trustStatus != ErrorSuccess && trustStatus != CertEUntrustedRoot)
        {
            throw new InvalidDataException(
                String.Format("The installer publisher signature is missing or invalid (WinVerifyTrust 0x{0:X8}).", trustStatus));
        }

        string actualThumbprint;
        try
        {
            using (var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(resolvedPath)))
                actualThumbprint = NormalizeThumbprint(certificate.Thumbprint);
        }
        catch (Exception error)
        {
            throw new InvalidDataException("The installer publisher signature certificate could not be read.", error);
        }

        if (!string.Equals(
                actualThumbprint,
                NormalizeThumbprint(expectedThumbprint),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The installer publisher signature does not match the pinned HuahaiClipboard publisher.");
        }
    }

    private static string NormalizeThumbprint(string value)
    {
        return new string((value ?? String.Empty).Where(Uri.IsHexDigit).ToArray());
    }

    private static uint VerifyAuthenticode(string path)
    {
        var fileInfo = new WinTrustFileInfo(path);
        var trustData = new WinTrustData(fileInfo);
        try
        {
            return WinVerifyTrust(IntPtr.Zero, VerifyV2, trustData.Pointer);
        }
        finally
        {
            trustData.Dispose();
            fileInfo.Dispose();
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint WinVerifyTrust(
        IntPtr windowHandle,
        [MarshalAs(UnmanagedType.LPStruct)] Guid actionId,
        IntPtr trustData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeWinTrustFileInfo
    {
        public uint StructSize;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeWinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
    }

    private sealed class WinTrustFileInfo : IDisposable
    {
        public WinTrustFileInfo(string path)
        {
            PathPointer = Marshal.StringToCoTaskMemUni(path);
            var native = new NativeWinTrustFileInfo
            {
                StructSize = (uint)Marshal.SizeOf(typeof(NativeWinTrustFileInfo)),
                FilePath = PathPointer,
                FileHandle = IntPtr.Zero,
                KnownSubject = IntPtr.Zero
            };
            Pointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeWinTrustFileInfo)));
            Marshal.StructureToPtr(native, Pointer, false);
        }

        public IntPtr Pointer { get; private set; }
        private IntPtr PathPointer { get; set; }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Pointer);
            Marshal.FreeCoTaskMem(PathPointer);
        }
    }

    private sealed class WinTrustData : IDisposable
    {
        public WinTrustData(WinTrustFileInfo fileInfo)
        {
            var native = new NativeWinTrustData
            {
                StructSize = (uint)Marshal.SizeOf(typeof(NativeWinTrustData)),
                PolicyCallbackData = IntPtr.Zero,
                SipClientData = IntPtr.Zero,
                UiChoice = 2,
                RevocationChecks = 0,
                UnionChoice = 1,
                FileInfo = fileInfo.Pointer,
                StateAction = 0,
                StateData = IntPtr.Zero,
                UrlReference = IntPtr.Zero,
                ProviderFlags = 0,
                UiContext = 0
            };
            Pointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeWinTrustData)));
            Marshal.StructureToPtr(native, Pointer, false);
        }

        public IntPtr Pointer { get; private set; }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Pointer);
        }
    }
}
}
