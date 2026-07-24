using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using MackySoft.FileSystem;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.GuiEditor;

/// <summary> Reads OS process metadata for Unity GUI Editor marker candidates. </summary>
internal sealed class UnityGuiEditorProcessInspector : IUnityGuiEditorProcessInspector
{
    private const int ProcessCommandExitTimeoutMilliseconds = 1000;

    private const uint ProcessQueryLimitedInformation = 0x1000;

    private const uint TokenQuery = 0x0008;

    /// <inheritdoc />
    public UnityGuiEditorProcessInspection Inspect (int processId)
    {
        DiagnosticsProcess process;
        try
        {
            process = DiagnosticsProcess.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return UnityGuiEditorProcessInspection.NotRunning();
        }

        using (process)
        {
            return InspectResolvedProcess(process, processId);
        }
    }

    private static UnityGuiEditorProcessInspection InspectResolvedProcess (
        DiagnosticsProcess process,
        int processId)
    {
        if (HasExited(process))
        {
            return UnityGuiEditorProcessInspection.NotRunning();
        }

        DateTimeOffset processStartTimeUtc;
        try
        {
            processStartTimeUtc = process.StartTime.ToUniversalTime();
        }
        catch (InvalidOperationException) when (HasExited(process))
        {
            return UnityGuiEditorProcessInspection.NotRunning();
        }
        catch (Exception)
        {
            return new UnityGuiEditorProcessInspection(
                Exists: true,
                HasExited: false,
                StartTimeUtc: null,
                ProcessName: null,
                CommandLine: null,
                ExecutablePath: null,
                IsOwnedByCurrentUser: null);
        }

        return new UnityGuiEditorProcessInspection(
            Exists: true,
            HasExited: false,
            StartTimeUtc: processStartTimeUtc,
            ProcessName: process.ProcessName,
            CommandLine: TryReadCommandLine(process.Id),
            ExecutablePath: TryReadExecutablePath(process),
            IsOwnedByCurrentUser: IsOwnedByCurrentUser(process.Id));
    }

    private static bool HasExited (DiagnosticsProcess process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static AbsolutePath? TryReadExecutablePath (DiagnosticsProcess process)
    {
        try
        {
            var executablePath = process.MainModule?.FileName;
            return AbsolutePath.TryParse(executablePath, out var guardedPath, out _)
                ? guardedPath
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool? IsOwnedByCurrentUser (int processId)
    {
        if (OperatingSystem.IsWindows())
        {
            return IsWindowsProcessOwnedByCurrentUserCore(processId);
        }

        var currentUser = Environment.UserName;
        if (string.IsNullOrWhiteSpace(currentUser))
        {
            return null;
        }

        var processUser = TryReadUnixProcessUser(processId);
        if (processUser == null)
        {
            return null;
        }

        return string.Equals(processUser, currentUser, StringComparison.Ordinal);
    }

    private static string? TryReadUnixProcessUser (int processId)
    {
        var psPath = TryResolveUnixPsPath();
        if (psPath == null)
        {
            return null;
        }

        return TryRunAndReadStandardOutput(new DiagnosticsProcessStartInfo
        {
            FileName = psPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "-p",
                processId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-o",
                "user=",
            },
        });
    }

    private static string? TryReadCommandLine (int processId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return TryReadWindowsCommandLine(processId);
        }

        var psPath = TryResolveUnixPsPath();
        if (psPath == null)
        {
            return null;
        }

        return TryRunAndReadStandardOutput(new DiagnosticsProcessStartInfo
        {
            FileName = psPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "-p",
                processId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-o",
                "args=",
            },
        });
    }

    private static string? TryReadWindowsCommandLine (int processId)
    {
        var powershellPath = TryResolveWindowsPowershellPath();
        if (powershellPath == null)
        {
            return null;
        }

        return TryRunAndReadStandardOutput(new DiagnosticsProcessStartInfo
        {
            FileName = powershellPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                $"(Get-CimInstance Win32_Process -Filter \"ProcessId = {processId}\").CommandLine",
            },
        });
    }

    private static string? TryResolveUnixPsPath ()
    {
        if (File.Exists("/bin/ps"))
        {
            return "/bin/ps";
        }

        return File.Exists("/usr/bin/ps")
            ? "/usr/bin/ps"
            : null;
    }

    private static string? TryResolveWindowsPowershellPath ()
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrWhiteSpace(systemDirectory))
        {
            return null;
        }

        var powershellPath = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(powershellPath)
            ? powershellPath
            : null;
    }

    private static string? TryRunAndReadStandardOutput (DiagnosticsProcessStartInfo startInfo)
    {
        DiagnosticsProcess? process = null;
        try
        {
            process = DiagnosticsProcess.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            if (!process.WaitForExit(ProcessCommandExitTimeoutMilliseconds))
            {
                TryKill(process);
                return null;
            }

            return process.ExitCode == 0
                ? process.StandardOutput.ReadToEnd().Trim()
                : null;
        }
        catch (Exception)
        {
            if (process != null)
            {
                TryKill(process);
            }

            return null;
        }
        finally
        {
            if (process != null)
            {
                TryDispose(process);
            }
        }
    }

    private static void TryKill (DiagnosticsProcess process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            _ = process.WaitForExit(ProcessCommandExitTimeoutMilliseconds);
        }
        catch (Exception)
        {
        }
    }

    private static void TryDispose (DiagnosticsProcess process)
    {
        try
        {
            process.Dispose();
        }
        catch (Exception)
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool? IsWindowsProcessOwnedByCurrentUserCore (int processId)
    {
        var currentUserSid = WindowsIdentity.GetCurrent().User;
        if (currentUserSid == null)
        {
            return null;
        }

        var processHandle = OpenProcess(ProcessQueryLimitedInformation, inheritHandle: false, processId);
        if (processHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            if (!OpenProcessToken(processHandle, TokenQuery, out var tokenHandle))
            {
                return null;
            }

            try
            {
                _ = GetTokenInformation(
                    tokenHandle,
                    TokenInformationClass.TokenUser,
                    IntPtr.Zero,
                    0,
                    out var tokenInformationLength);
                if (tokenInformationLength == 0)
                {
                    return null;
                }

                var tokenInformation = Marshal.AllocHGlobal(checked((int)tokenInformationLength));
                try
                {
                    if (!GetTokenInformation(
                            tokenHandle,
                            TokenInformationClass.TokenUser,
                            tokenInformation,
                            tokenInformationLength,
                            out _))
                    {
                        return null;
                    }

                    var tokenUser = Marshal.PtrToStructure<TokenUser>(tokenInformation);
                    var processUserSid = new SecurityIdentifier(tokenUser.user.sid);
                    return currentUserSid.Equals(processUserSid);
                }
                finally
                {
                    Marshal.FreeHGlobal(tokenInformation);
                }
            }
            finally
            {
                CloseHandle(tokenHandle);
            }
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess (
        uint processAccess,
        bool inheritHandle,
        int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken (
        IntPtr processHandle,
        uint desiredAccess,
        out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation (
        IntPtr tokenHandle,
        TokenInformationClass tokenInformationClass,
        IntPtr tokenInformation,
        uint tokenInformationLength,
        out uint returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle (IntPtr handle);

    private enum TokenInformationClass
    {
        TokenUser = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SidAndAttributes
    {
        public readonly IntPtr sid;

        public readonly uint attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct TokenUser
    {
        public readonly SidAndAttributes user;
    }
}
