using System.Diagnostics;
using System.Globalization;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Creates and parses temporary file paths that identify their owning process. </summary>
internal static class ProcessOwnedTemporaryFilePath
{
    private const string TemporaryMarker = ".tmp.";

    /// <summary> Creates a unique temporary path owned by the current process without creating the file. </summary>
    /// <param name="destinationPath"> The final destination file path. </param>
    /// <returns> A path in the form <c>&lt;destination&gt;.tmp.&lt;processId&gt;.&lt;guidN&gt;</c>. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="destinationPath" /> is empty. </exception>
    public static string Create (string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Destination path must not be null or whitespace.", nameof(destinationPath));
        }

        int processId;
        using (var process = Process.GetCurrentProcess())
        {
            processId = process.Id;
        }

        return string.Concat(
            destinationPath,
            TemporaryMarker,
            processId.ToString(CultureInfo.InvariantCulture),
            ".",
            Guid.NewGuid().ToString("N"));
    }

    /// <summary> Extracts the positive owner process identifier from an exact process-owned temporary path. </summary>
    /// <param name="destinationPath"> The final destination file path. </param>
    /// <param name="candidatePath"> The candidate temporary file path. </param>
    /// <param name="processId"> The extracted positive process identifier when successful. </param>
    /// <returns> <see langword="true" /> only for <c>&lt;destination&gt;.tmp.&lt;processId&gt;.&lt;guidN&gt;</c>; otherwise <see langword="false" />. </returns>
    public static bool TryGetOwnerProcessId (
        string destinationPath,
        string candidatePath,
        out int processId)
    {
        processId = default;
        if (string.IsNullOrWhiteSpace(destinationPath)
            || string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var expectedPrefix = destinationPath + TemporaryMarker;
        if (!candidatePath.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var ownerToken = candidatePath.Substring(expectedPrefix.Length);
        var separatorIndex = ownerToken.IndexOf('.');
        if (separatorIndex <= 0
            || separatorIndex != ownerToken.LastIndexOf('.')
            || separatorIndex == ownerToken.Length - 1)
        {
            return false;
        }

        return int.TryParse(
                ownerToken.Substring(0, separatorIndex),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out processId)
            && processId > 0
            && Guid.TryParseExact(ownerToken.Substring(separatorIndex + 1), "N", out var ownerNonce)
            && ownerNonce != Guid.Empty;
    }
}
