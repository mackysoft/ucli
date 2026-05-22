using System.Runtime.InteropServices;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Implements process scanning for Unity processes that own a project path. </summary>
internal sealed class UnityProjectProcessScanner : IUnityProjectProcessScanner
{
    private static readonly TimeSpan ProcessScanTimeout = TimeSpan.FromSeconds(2);

    private readonly IProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="UnityProjectProcessScanner" /> class. </summary>
    /// <param name="processRunner"> The process runner dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="processRunner" /> is <see langword="null" />. </exception>
    public UnityProjectProcessScanner (IProcessRunner processRunner)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <inheritdoc />
    public async ValueTask<UnityProjectProcessScanResult> FindProcessesForProjectAsync (
        string unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);

        if (!UnityProjectPathIdentity.TryNormalize(unityProjectRoot, out var normalizedTargetPath, out var normalizationError))
        {
            return UnityProjectProcessScanResult.Failure($"Target project path could not be normalized for process scan. {normalizationError}");
        }

        var request = CreateProcessListRequest();
        if (request == null)
        {
            return UnityProjectProcessScanResult.Failure("Unity process scan is not implemented for this operating system.");
        }

        var runResult = await processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (runResult.Status != ProcessRunStatus.Exited || runResult.ExitCode != 0)
        {
            return UnityProjectProcessScanResult.Failure(
                runResult.ErrorMessage ?? $"Unity process scan failed. Status={runResult.Status}.");
        }

        if (runResult.StandardOutput == null)
        {
            return UnityProjectProcessScanResult.Failure("Unity process scan produced no process list output.");
        }

        return ParseProcessList(runResult.StandardOutput, normalizedTargetPath);
    }

    private static ProcessRunRequest? CreateProcessListRequest ()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // NOTE: Unity can leave Temp/UnityLockfile behind after a native crash. Windows does not expose
            // a Unity-specific lock owner API, so stale-lock cleanup uses the process command line as the
            // same project ownership signal used by Unix ps output.
            return new ProcessRunRequest(
                FileName: "powershell.exe",
                Arguments:
                [
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-Command",
                    "$ErrorActionPreference = 'Stop'; Get-CimInstance Win32_Process | Where-Object { $_.CommandLine } | ForEach-Object { '{0}`t{1}' -f $_.ProcessId, $_.CommandLine }",
                ],
                Timeout: ProcessScanTimeout,
                CaptureStandardOutput: true);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var psPath = File.Exists("/bin/ps") ? "/bin/ps" : "/usr/bin/ps";
            return new ProcessRunRequest(
                FileName: psPath,
                Arguments: ["-axo", "pid=,command="],
                Timeout: ProcessScanTimeout,
                CaptureStandardOutput: true);
        }

        return null;
    }

    private static UnityProjectProcessScanResult ParseProcessList (
        string processListText,
        string normalizedTargetPath)
    {
        var matches = new List<UnityProjectProcessMatch>();
        var lines = processListText.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!TryParseProcessListLine(lines[i], out var processId, out var commandLine))
            {
                continue;
            }

            var tokens = TokenizeCommandLine(commandLine);
            if (tokens.Count == 0 || !IsUnityCommand(tokens[0], commandLine))
            {
                continue;
            }

            if (!TryGetMatchingProjectPathArgument(tokens, normalizedTargetPath, out var argumentError))
            {
                if (argumentError != null)
                {
                    return UnityProjectProcessScanResult.Failure(argumentError);
                }

                continue;
            }

            matches.Add(new UnityProjectProcessMatch(processId));
        }

        return UnityProjectProcessScanResult.Success(matches);
    }

    private static bool TryParseProcessListLine (
        string line,
        out int processId,
        out string commandLine)
    {
        processId = default;
        commandLine = string.Empty;

        var trimmed = line.TrimStart();
        var index = 0;
        while (index < trimmed.Length && char.IsDigit(trimmed[index]))
        {
            index++;
        }

        if (index == 0 || index >= trimmed.Length)
        {
            return false;
        }

        if (!int.TryParse(trimmed.AsSpan(0, index), out processId) || processId <= 0)
        {
            return false;
        }

        commandLine = trimmed[index..].TrimStart();
        return commandLine.Length > 0;
    }

    private static bool IsUnityCommand (
        string executableToken,
        string commandLine)
    {
        var fileName = Path.GetFileNameWithoutExtension(executableToken);
        return string.Equals(fileName, "Unity", StringComparison.OrdinalIgnoreCase)
            || commandLine.Contains("/Unity.app/", StringComparison.Ordinal)
            || commandLine.Contains("\\Unity.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetMatchingProjectPathArgument (
        IReadOnlyList<string> tokens,
        string normalizedTargetPath,
        out string? errorMessage)
    {
        errorMessage = null;

        for (var i = 0; i < tokens.Count; i++)
        {
            if (!string.Equals(tokens[i], "-projectPath", StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 >= tokens.Count || string.IsNullOrWhiteSpace(tokens[i + 1]))
            {
                errorMessage = "Unity process command line contains -projectPath without a path value.";
                return false;
            }

            for (var endIndex = i + 2; endIndex <= tokens.Count; endIndex++)
            {
                var candidateProjectPath = JoinTokens(tokens, i + 1, endIndex);
                if (!UnityProjectPathIdentity.TryNormalize(candidateProjectPath, out var normalizedProcessProjectPath, out _))
                {
                    continue;
                }

                if (string.Equals(normalizedProcessProjectPath, normalizedTargetPath, StringComparison.Ordinal)
                    && IsLikelyArgumentBoundary(tokens, endIndex))
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    private static bool IsLikelyArgumentBoundary (
        IReadOnlyList<string> tokens,
        int endIndex)
    {
        return endIndex >= tokens.Count
            || tokens[endIndex].StartsWith("-", StringComparison.Ordinal);
    }

    private static string JoinTokens (
        IReadOnlyList<string> tokens,
        int startIndex,
        int endIndex)
    {
        if (endIndex == startIndex + 1)
        {
            return tokens[startIndex];
        }

        var builder = new System.Text.StringBuilder(tokens[startIndex]);
        for (var i = startIndex + 1; i < endIndex; i++)
        {
            builder.Append(' ');
            builder.Append(tokens[i]);
        }

        return builder.ToString();
    }

    internal static IReadOnlyList<string> TokenizeCommandLine (string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var quoteCharacter = '\0';
        for (var i = 0; i < commandLine.Length; i++)
        {
            var currentCharacter = commandLine[i];
            if ((currentCharacter == '"' || currentCharacter == '\'') && (!inQuotes || currentCharacter == quoteCharacter))
            {
                if (inQuotes)
                {
                    inQuotes = false;
                    quoteCharacter = '\0';
                }
                else
                {
                    inQuotes = true;
                    quoteCharacter = currentCharacter;
                }

                continue;
            }

            if (char.IsWhiteSpace(currentCharacter) && !inQuotes)
            {
                FlushToken(tokens, current);
                continue;
            }

            current.Append(currentCharacter);
        }

        FlushToken(tokens, current);
        return tokens;
    }

    private static void FlushToken (
        List<string> tokens,
        System.Text.StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }
}
