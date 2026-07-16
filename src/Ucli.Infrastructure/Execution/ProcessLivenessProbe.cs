using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Infrastructure.Execution;

/// <summary> Probes operating-system process liveness and generation identity. </summary>
internal static class ProcessLivenessProbe
{
    private const int LinuxProcessStateFieldIndex = 0;
    private const int LinuxProcessStartGenerationFieldIndex = 19;

    /// <summary> Gets whether the specified process identifier still points to a live process. </summary>
    /// <param name="processId"> The operating-system process identifier. </param>
    /// <returns> <see langword="true" /> when the process exists and has not exited; otherwise <see langword="false" />. </returns>
    internal static bool IsAlive (int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary> Captures the identity of the current operating-system process. </summary>
    internal static ProcessIdentity CaptureCurrentProcess ()
    {
        using var process = Process.GetCurrentProcess();
        return new ProcessIdentity(process.Id, GetProcessGeneration(process));
    }

    /// <summary> Gets whether an identity still refers to the same live operating-system process generation. </summary>
    internal static bool IsSameProcess (ProcessIdentity expectedIdentity)
    {
        if (expectedIdentity == null)
        {
            throw new ArgumentNullException(nameof(expectedIdentity));
        }

        try
        {
            using var process = Process.GetProcessById(expectedIdentity.ProcessId);
            if (process.HasExited)
            {
                return false;
            }

            return GetProcessGeneration(process) == expectedIdentity.Generation
                && !process.HasExited;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or Win32Exception
            or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary> Parses the raw Linux process start generation from field 22 of <c>/proc/&lt;pid&gt;/stat</c>. </summary>
    internal static bool TryParseLinuxProcessStartGeneration (
        string stat,
        out ulong generation)
    {
        generation = default;
        var commandEndIndex = stat.LastIndexOf(')');
        if (commandEndIndex < 0 || commandEndIndex == stat.Length - 1)
        {
            return false;
        }

        var fields = stat.AsSpan(commandEndIndex + 1);
        var fieldIndex = 0;
        var position = 0;
        while (position < fields.Length)
        {
            while (position < fields.Length && char.IsWhiteSpace(fields[position]))
            {
                position++;
            }

            var fieldStart = position;
            while (position < fields.Length && !char.IsWhiteSpace(fields[position]))
            {
                position++;
            }

            if (fieldStart == position)
            {
                break;
            }

            var field = fields.Slice(fieldStart, position - fieldStart);
            if (fieldIndex == LinuxProcessStateFieldIndex && field.Length != 1)
            {
                return false;
            }

            if (fieldIndex == LinuxProcessStartGenerationFieldIndex)
            {
                return ulong.TryParse(
                        field,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out generation)
                    && generation != 0;
            }

            fieldIndex++;
        }

        return false;
    }

    private static ulong GetProcessGeneration (Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ReadLinuxProcessStartGeneration(process.Id);
        }

        var startTimeTicks = process.StartTime.ToUniversalTime().Ticks;
        if (startTimeTicks <= 0)
        {
            throw new InvalidOperationException("Process start time must identify a positive generation.");
        }

        return (ulong)startTimeTicks;
    }

    private static ulong ReadLinuxProcessStartGeneration (int processId)
    {
        var statPath = "/proc/" + processId.ToString(CultureInfo.InvariantCulture) + "/stat";
        string stat;
        try
        {
            stat = File.ReadAllText(statPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Linux process generation could not be read.",
                exception);
        }

        if (!TryParseLinuxProcessStartGeneration(stat, out var generation))
        {
            throw new InvalidOperationException("Linux process generation is invalid.");
        }

        return generation;
    }
}
