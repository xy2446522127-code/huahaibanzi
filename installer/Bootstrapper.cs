using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("花海剪贴板安装程序")]
[assembly: AssemblyDescription("花海剪贴板 Windows x64 安装程序")]
[assembly: AssemblyCompany("HuahaiClipboard")]
[assembly: AssemblyProduct("花海剪贴板")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyVersion("1.1.13.0")]
[assembly: AssemblyFileVersion("1.1.13.0")]

internal static class Bootstrapper
{
    private const string ProductName = "花海剪贴板";
    private const string ProductFolderName = "HuahaiClipboard";
    private const string AppFileName = "HuahaiClipboard.App.exe";
    private const string LauncherFileName = "HuahaiClipboard.Launcher.exe";
    private const string ResourceName = "HuahaiClipboard.Payload";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\HuahaiClipboard";
    private static string installerLogPath;

    // 解析静默参数并串行执行安装，避免两个安装器同时替换文件。
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        installerLogPath = InstallerLogPolicy.ResolvePath(Path.GetTempPath(), DateTime.Now);
        Log("Installer started.");

        bool silent = HasArgument(args, "--silent");
        bool noLaunch = HasArgument(args, "--no-launch");
        bool createdNew;

        using (var mutex = new Mutex(true, @"Local\HuahaiClipboardInstaller", out createdNew))
        {
            if (!createdNew)
            {
                ShowMessage("安装程序已在运行。", MessageBoxIcon.Information, silent);
                return 2;
            }

            string defaultInstallRoot;
            string installRoot;
            bool restartRequired;
            try
            {
                defaultInstallRoot = InstallLocationPolicy.DefaultForRoots(GetAvailableFixedDriveRoots(), ProductFolderName);
                string requestedInstallRoot = GetArgumentValue(args, "--install-dir");
                if (!silent && String.IsNullOrWhiteSpace(requestedInstallRoot))
                {
                    requestedInstallRoot = ChooseInstallRoot(defaultInstallRoot);
                    if (requestedInstallRoot == null)
                        return 4;
                }
                installRoot = InstallLocationPolicy.Resolve(requestedInstallRoot, defaultInstallRoot);
                restartRequired = Install(installRoot);
            }
            catch (Exception ex)
            {
                Log("Installation failed: " + ex);
                ShowMessage("安装失败：\n" + ex.Message, MessageBoxIcon.Error, silent);
                return 1;
            }

            if (PostInstallLaunchPolicy.ShouldLaunch(noLaunch, restartRequired))
            {
                try
                {
                    var startInfo = new ProcessStartInfo(Path.Combine(installRoot, AppFileName))
                    {
                        UseShellExecute = true,
                        WorkingDirectory = installRoot
                    };
                    startInfo.Arguments = PostInstallLaunchPolicy.ArgumentsFor(silent);
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    Log("Post-install launch failed: " + ex);
                    ShowMessage(
                        "安装已完成，但自动启动失败。\n请从桌面快捷方式打开花海剪贴板。\n\n原因：" + ex.Message,
                        MessageBoxIcon.Warning,
                        silent);
                    return 3;
                }
            }

            ShowMessage(
                restartRequired
                    ? "花海剪贴板已安装完成，但 Windows 需要重启后才能启动程序。\n\n安装位置：" + installRoot
                    : "花海剪贴板已安装完成。\n\n安装位置：" + installRoot,
                MessageBoxIcon.Information,
                silent);
            Log("Installation completed. RestartRequired=" + restartRequired + "; InstallRoot=" + installRoot);
            return 0;
        }
    }

    private static string[] GetAvailableFixedDriveRoots()
    {
        var roots = new List<string>();
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    roots.Add(drive.RootDirectory.FullName);
            }
            catch (IOException)
            {
                // 忽略暂时不可读取的磁盘，继续寻找其他本地磁盘。
            }
            catch (UnauthorizedAccessException)
            {
                // 忽略当前用户无权访问的磁盘。
            }
        }
        return roots.ToArray();
    }

    private static string ChooseInstallRoot(string defaultInstallRoot)
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = "选择花海剪贴板的安装位置（不能安装到 C 盘）。\n选择磁盘或文件夹后，程序会安装到其中的 HuahaiClipboard 文件夹。";
            dialog.ShowNewFolderButton = true;
            dialog.SelectedPath = Path.GetPathRoot(defaultInstallRoot);
            if (dialog.ShowDialog() != DialogResult.OK)
                return null;

            string selectedParent = dialog.SelectedPath;
            string selectedName = Path.GetFileName(selectedParent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string selectedRoot = String.Equals(selectedName, ProductFolderName, StringComparison.OrdinalIgnoreCase)
                ? selectedParent
                : Path.Combine(selectedParent, ProductFolderName);
            return InstallLocationPolicy.Resolve(selectedRoot, defaultInstallRoot);
        }
    }

    // 使用同卷暂存与备份目录完成可回滚的当前用户安装。
    private static bool Install(string installRoot)
    {
        string parent = Path.GetDirectoryName(installRoot);
        if (String.IsNullOrWhiteSpace(parent))
            throw new InvalidOperationException("无法确定安装目录。");

        InstallTargetPolicy.Validate(installRoot, GetRegisteredInstallRoot());

        Directory.CreateDirectory(parent);
        string stagingRoot = Path.Combine(parent, ".HuahaiClipboard-install-" + Guid.NewGuid().ToString("N"));
        string backupRoot = Path.Combine(parent, ".HuahaiClipboard-backup-" + Guid.NewGuid().ToString("N"));
        string activeStep = "准备安装目录";

        try
        {
            activeStep = "创建候选目录";
            Directory.CreateDirectory(stagingRoot);
            activeStep = "解压应用文件";
            ExtractPayloadSafely(stagingRoot);
            activeStep = "校验应用文件";
            ValidatePayload(stagingRoot);
            activeStep = "检查 Microsoft 运行时";
            bool restartRequired = InstallMissingPrerequisites(stagingRoot);
            Log("Prerequisites verified. RestartRequired=" + restartRequired);
            activeStep = "关闭旧版本";
            StopInstalledProcesses(installRoot);
            activeStep = "保留安装目录中的用户数据";
            InstallDataPreserver.CopyIntoCandidate(installRoot, stagingRoot);
            Log("Install-root Data was preserved into the candidate payload.");

            InstallSwapResult swapResult = InstallSwapTransaction.Execute(
                stagingRoot,
                installRoot,
                backupRoot,
                Directory.Exists,
                MoveDirectoryWithRetry,
                TryDeleteDirectory,
                delegate
                {
                    activeStep = "写入安装所有权标记";
                    InstallTargetPolicy.WriteOwnerMarker(installRoot);
                    activeStep = "创建快捷方式";
                    CreateShortcuts(installRoot);
                    activeStep = "注册卸载入口";
                    RegisterUninstaller(installRoot);
                });

            if (swapResult.BackupCleanupPending)
                Trace.WriteLine("Installation committed; old-version backup cleanup is pending: " + backupRoot);
            return restartRequired;
        }
        catch (Exception ex)
        {
            if (Directory.Exists(stagingRoot))
                TryDeleteDirectory(stagingRoot);

            throw new InvalidOperationException("安装步骤“" + activeStep + "”失败：" + ex.Message, ex);
        }
    }

    // 对杀毒扫描或子进程退出造成的短暂目录占用执行有限重试。
    private static void MoveDirectoryWithRetry(string source, string destination)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Move(source, destination);
                return;
            }
            catch (IOException)
            {
                // WebView2 may keep its profile handle briefly after the parent app exits.
                if (attempt >= 74)
                    throw;
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt >= 74)
                    throw;
                Thread.Sleep(200);
            }
        }
    }

    // 逐项校验 ZIP 路径，阻止条目逃逸到暂存目录之外。
    private static void ExtractPayloadSafely(string stagingRoot)
    {
        string normalizedRoot = Path.GetFullPath(stagingRoot + Path.DirectorySeparatorChar);
        Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (resource == null)
            throw new InvalidDataException("安装包缺少应用文件。");

        using (resource)
        using (var archive = new ZipArchive(resource, ZipArchiveMode.Read, false))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string targetPath = Path.GetFullPath(Path.Combine(stagingRoot, entry.FullName));
                if (!targetPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("安装包包含无效路径。");

                if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!String.IsNullOrEmpty(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                using (Stream input = entry.Open())
                using (FileStream output = File.Create(targetPath))
                    input.CopyTo(output);
            }
        }
    }

    // 在触碰现有安装前确认原生应用入口、运行依赖和卸载脚本完整。
    private static void ValidatePayload(string root)
    {
        string[] requiredPaths =
        {
            AppFileName,
            "HuahaiClipboard.App.dll",
            "HuahaiClipboard.App.deps.json",
            "HuahaiClipboard.App.runtimeconfig.json",
            "HuahaiClipboard.App.pri",
            "HuahaiClipboard.Core.dll",
            "Microsoft.WinUI.dll",
            "Microsoft.Web.WebView2.Core.dll",
            "Microsoft.WindowsAppRuntime.Bootstrap.Net.dll",
            "Microsoft.Windows.SDK.NET.dll",
            "WinRT.Runtime.dll",
            "App.xbf",
            @"Presentation\Windows\CursorPanelWindow.xbf",
            @"Assets\Web\product-shell.html",
            @"Assets\Web\panel-scale.js",
            "WebView2Loader.dll",
            "Microsoft.WindowsAppRuntime.Bootstrap.dll",
            @"prerequisites\MicrosoftEdgeWebView2RuntimeInstallerX64.exe",
            "Uninstall.ps1",
            "UninstallPolicy.ps1"
        };
        foreach (string relativePath in requiredPaths)
        {
            if (!File.Exists(Path.Combine(root, relativePath)))
                throw new InvalidDataException("安装包内容不完整：" + relativePath);
        }
    }

    // 仅在本机缺失时安装微软官方运行时，并清除安装包中的临时副本。
    private static bool InstallMissingPrerequisites(string stagingRoot)
    {
        string prerequisiteRoot = Path.Combine(stagingRoot, "prerequisites");
        string[] dotNetInstallers = Directory.Exists(prerequisiteRoot)
            ? Directory.GetFiles(prerequisiteRoot, "windowsdesktop-runtime-8.*-win-x64.exe")
            : new string[0];
        string[] webView2RuntimeInstallers = Directory.Exists(prerequisiteRoot)
            ? Directory.GetFiles(prerequisiteRoot, "MicrosoftEdgeWebView2RuntimeInstallerX64.exe")
            : new string[0];

        if (dotNetInstallers.Length != 1)
            throw new InvalidDataException("安装包缺少 .NET 8 桌面运行时组件。");
        if (webView2RuntimeInstallers.Length != 1)
            throw new InvalidDataException("安装包缺少 Evergreen WebView2 Runtime 组件。");

        bool needsDotNet = PrerequisitePolicy.NeedsDotNetDesktopRuntime(GetInstalledDesktopRuntimeVersions());
        bool needsWebView2Runtime = PrerequisitePolicy.NeedsWebView2Runtime(GetInstalledWebView2RuntimeVersions());
        bool restartRequired = false;

        // 前置安装程序从系统临时目录运行，避免其子进程锁住应用暂存目录。
        if (needsDotNet || needsWebView2Runtime)
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(), "HuahaiClipboardPrerequisites-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                if (needsDotNet)
                {
                    string temporaryDotNet = Path.Combine(temporaryRoot, Path.GetFileName(dotNetInstallers[0]));
                    File.Copy(dotNetInstallers[0], temporaryDotNet, true);
                    restartRequired |= RunPrerequisiteInstaller(temporaryDotNet, "/install /quiet /norestart", ".NET Desktop Runtime 8");
                }

                if (needsWebView2Runtime)
                {
                    string temporaryWebView2Runtime = Path.Combine(temporaryRoot, Path.GetFileName(webView2RuntimeInstallers[0]));
                    File.Copy(webView2RuntimeInstallers[0], temporaryWebView2Runtime, true);
                    restartRequired |= RunPrerequisiteInstaller(temporaryWebView2Runtime, "/silent /install", "Evergreen WebView2 Runtime");
                }

            }
            finally
            {
                TryDeleteDirectory(temporaryRoot);
            }
        }

        if (PrerequisitePolicy.HasMissingRuntime(
                PrerequisitePolicy.NeedsDotNetDesktopRuntime(GetInstalledDesktopRuntimeVersions()),
                PrerequisitePolicy.NeedsWebView2Runtime(GetInstalledWebView2RuntimeVersions())))
            throw new InvalidOperationException("运行环境安装后复验失败，请查看安装日志或重启 Windows 后重试。");

        TryDeleteDirectory(prerequisiteRoot);
        if (Directory.Exists(prerequisiteRoot))
            throw new IOException("无法清理安装包中的 Microsoft 运行时临时文件。");
        return restartRequired;
    }

    // 从实际 dotnet 安装根目录枚举桌面运行时版本，兼容未写入 sharedfx 注册表的安装方式。
    private static string[] GetInstalledDesktopRuntimeVersions()
    {
        const string keyPath = @"SOFTWARE\dotnet\Setup\InstalledVersions\x64";
        string dotNetRoot = null;
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            dotNetRoot = key == null ? null : key.GetValue("InstallLocation") as string;

        if (String.IsNullOrWhiteSpace(dotNetRoot))
            dotNetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");

        string desktopRoot = Path.Combine(dotNetRoot, "shared", "Microsoft.WindowsDesktop.App");
        if (!Directory.Exists(desktopRoot))
            return new string[0];

        string[] directories = Directory.GetDirectories(desktopRoot);
        string[] versions = new string[directories.Length];
        for (int index = 0; index < directories.Length; index++)
            versions[index] = Path.GetFileName(directories[index]);
        return versions;
    }

    private static string[] GetInstalledWebView2RuntimeVersions()
    {
        const string clientKey = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        var versions = new List<string>();
        RegistryHive[] hives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
        RegistryView[] views = { RegistryView.Registry64, RegistryView.Registry32 };

        foreach (RegistryHive hive in hives)
        {
            foreach (RegistryView view in views)
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                    using (RegistryKey key = baseKey.OpenSubKey(clientKey))
                    {
                        string version = key == null ? null : key.GetValue("pv") as string;
                        if (!String.IsNullOrWhiteSpace(version))
                            versions.Add(version);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // 无权读取某个注册表视图时继续检查其他当前用户或本机视图。
                }
                catch (System.Security.SecurityException)
                {
                    // 检测不可用时安全地运行微软签名的离线安装器。
                }
            }
        }

        return versions.ToArray();
    }

    // 仅接受微软安装器的成功、已安装和需重启返回码。
    private static bool RunPrerequisiteInstaller(string path, string arguments, string displayName)
    {
        var startInfo = new ProcessStartInfo(path, arguments)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(path)
        };

        using (Process process = Process.Start(startInfo))
        {
            if (process == null)
                throw new InvalidOperationException("无法启动 " + displayName + " 安装程序。");
            process.WaitForExit();
            PrerequisiteInstallOutcome outcome = PrerequisitePolicy.ClassifyInstallerExitCode(process.ExitCode);
            Log(displayName + " installer exit code=" + process.ExitCode + "; outcome=" + outcome);
            if (outcome == PrerequisiteInstallOutcome.Failed)
                throw new InvalidOperationException(displayName + " 安装失败，退出码：" + process.ExitCode);
            return outcome == PrerequisiteInstallOutcome.RestartRequired;
        }
    }

    // 只停止路径位于目标安装目录内的旧版本进程。
    private static void StopInstalledProcesses(string installRoot)
    {
        string normalizedRoot = Path.GetFullPath(installRoot + Path.DirectorySeparatorChar);
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                string path = process.MainModule == null ? null : process.MainModule.FileName;
                if (path != null && Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(1200))
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }

    // 创建当前用户的开始菜单和桌面入口。
    private static void CreateShortcuts(string installRoot)
    {
        string target = Path.Combine(installRoot, LauncherFileName);
        string icon = Path.Combine(installRoot, AppFileName);
        string startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", ProductName + ".lnk");
        string desktop = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            ProductName + ".lnk");

        CreateShortcut(startMenu, target, icon, installRoot);
        CreateShortcut(desktop, target, icon, installRoot);
    }

    // 通过 Windows Shell 写入带狐狸图标的快捷方式。
    private static void CreateShortcut(
        string shortcutPath,
        string target,
        string icon,
        string workingDirectory)
    {
        string directory = Path.GetDirectoryName(shortcutPath);
        if (!String.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            throw new InvalidOperationException("无法创建 Windows 快捷方式。");

        object shell = Activator.CreateInstance(shellType);
        try
        {
            dynamic shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            shortcut.TargetPath = target;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = icon + ",0";
            shortcut.Description = ProductName;
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcut);
        }
        finally
        {
            if (shell != null && Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }

    // 在 HKCU 注册标准的“应用和功能”卸载入口。
    private static void RegisterUninstaller(string installRoot)
    {
        string appPath = Path.Combine(installRoot, AppFileName);
        string uninstallScript = Path.Combine(installRoot, "Uninstall.ps1");
        string powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        long bytes = DirectorySize(installRoot);

        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            if (key == null)
                throw new InvalidOperationException("无法创建卸载入口。");

            key.SetValue("DisplayName", ProductName, RegistryValueKind.String);
            key.SetValue("DisplayVersion", "1.1.13", RegistryValueKind.String);
            key.SetValue("Publisher", "HuahaiClipboard", RegistryValueKind.String);
            key.SetValue("DisplayIcon", appPath + ",0", RegistryValueKind.String);
            key.SetValue("InstallLocation", installRoot, RegistryValueKind.String);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
            key.SetValue("UninstallString", "\"" + powershell + "\" -NoProfile -ExecutionPolicy Bypass -File \"" + uninstallScript + "\"", RegistryValueKind.String);
            key.SetValue("QuietUninstallString", "\"" + powershell + "\" -NoProfile -ExecutionPolicy Bypass -File \"" + uninstallScript + "\" -Silent", RegistryValueKind.String);
            key.SetValue("EstimatedSize", (int)Math.Min(Int32.MaxValue, Math.Max(1L, bytes / 1024L)), RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }

    private static string GetRegisteredInstallRoot()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKey))
            return key == null ? null : key.GetValue("InstallLocation") as string;
    }

    // 计算安装目录大小，供 Windows 卸载列表显示。
    private static long DirectorySize(string root)
    {
        long total = 0;
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            total += new FileInfo(file).Length;
        return total;
    }

    // 对短暂文件占用执行有限重试，不扩大删除范围。
    private static bool TryDeleteDirectory(string path)
    {
        for (int attempt = 0; attempt < 15 && Directory.Exists(path); attempt++)
        {
            try { Directory.Delete(path, true); }
            catch (IOException) { Thread.Sleep(200); }
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
        return !Directory.Exists(path);
    }

    // 使用不区分大小写的安装器命令行开关。
    private static bool HasArgument(string[] args, string expected)
    {
        foreach (string value in args)
            if (String.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // 读取有值的安装目录参数，避免 --install-dir 后缺值时悄然写入默认位置。
    private static string GetArgumentValue(string[] args, string expected)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (!String.Equals(args[index], expected, StringComparison.OrdinalIgnoreCase))
                continue;
            if (index + 1 >= args.Length || String.IsNullOrWhiteSpace(args[index + 1]))
                throw new ArgumentException(expected + " 必须提供安装目录。");
            return args[index + 1];
        }
        return null;
    }

    // 静默模式只返回退出码，交互模式显示中文结果。
    private static void ShowMessage(string message, MessageBoxIcon icon, bool silent)
    {
        if (!silent)
            MessageBox.Show(message, ProductName, MessageBoxButtons.OK, icon);
    }

    private static void Log(string message)
    {
        try
        {
            string directory = Path.GetDirectoryName(installerLogPath);
            if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(installerLogPath, DateTime.Now.ToString("O") + " " + message + Environment.NewLine);
        }
        catch
        {
            // Diagnostic logging must never make installation fail.
        }
    }
}
