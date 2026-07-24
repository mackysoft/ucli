using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Creates and parses process-owned editor-log temporary file paths. </summary>
internal static class EditorLogTemporaryFilePath
{
    private const string FileNamePrefix = FileUtilities.AtomicWriteTemporaryFileNamePrefix;

    private const int MaximumProcessIdCharacterCount = 10;

    private const int TemporaryFileCreationAttemptLimit = 10;

    private const int TemporaryNonceHexCharacterCount = 14;

    private const string LowerHexCharacters = "0123456789abcdef";

    /// <summary> Gets the top-directory search pattern for process-owned editor-log temporary files. </summary>
    public const string FileNameSearchPattern = FileNamePrefix + "*-*";

    /// <summary> Gets the maximum generated editor-log temporary file-name length. </summary>
    internal static int MaximumFileNameLength =>
        FileNamePrefix.Length
        + MaximumProcessIdCharacterCount
        + 1
        + TemporaryNonceHexCharacterCount;

    /// <summary> Exclusively creates an editor-log temporary file owned by the current process. </summary>
    /// <param name="destinationPath"> The final editor-log destination path. </param>
    /// <param name="bufferSize"> The write stream buffer size. </param>
    /// <param name="temporaryPath"> The reserved sibling path in the form <c>.tmp-&lt;processId&gt;-&lt;14 lowercase hex nonce&gt;</c>. </param>
    /// <returns> The exclusive asynchronous write stream that owns <paramref name="temporaryPath" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="destinationPath" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="bufferSize" /> is not positive. </exception>
    /// <exception cref="InvalidOperationException"> Thrown when the destination directory cannot be resolved. </exception>
    /// <exception cref="IOException"> Thrown when no unique temporary file can be reserved. </exception>
    public static FileStream OpenExclusiveWrite (
        AbsolutePath destinationPath,
        int bufferSize,
        out AbsolutePath temporaryPath)
    {
        if (destinationPath is null)
        {
            throw new ArgumentNullException(nameof(destinationPath));
        }

        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive.");
        }

        if (!destinationPath.TryGetParent(out var directoryPath))
        {
            throw new InvalidOperationException(
                $"Destination directory path could not be resolved: {destinationPath.Value}");
        }

        int processId;
        using (var process = Process.GetCurrentProcess())
        {
            processId = process.Id;
        }

        for (var attempt = 0; attempt < TemporaryFileCreationAttemptLimit; attempt++)
        {
            var candidatePath = ContainedPath.Create(
                directoryPath,
                RootRelativePath.Parse(string.Concat(
                    FileNamePrefix,
                    processId.ToString(CultureInfo.InvariantCulture),
                    "-",
                    CreateTemporaryNonce()))).Target;
            try
            {
                var stream = new FileStream(
                    candidatePath.Value,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize,
                    FileOptions.Asynchronous);
                temporaryPath = candidatePath;
                return stream;
            }
            catch (IOException) when (File.Exists(candidatePath.Value) || Directory.Exists(candidatePath.Value))
            {
                // A different writer owns this random name; retry without deleting its file.
            }
        }

        throw new IOException(
            $"Could not reserve an editor-log temporary file after {TemporaryFileCreationAttemptLimit} attempts: {directoryPath.Value}");
    }

    /// <summary> Extracts the positive owner process identifier from an exact editor-log temporary file name. </summary>
    /// <param name="fileName"> The candidate editor-log temporary file name. </param>
    /// <param name="processId"> The extracted positive process identifier when successful. </param>
    /// <returns> <see langword="true" /> only for a canonical editor-log temporary file name; otherwise <see langword="false" />. </returns>
    public static bool TryGetOwnerProcessId (
        string fileName,
        out int processId)
    {
        processId = default;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (!fileName.StartsWith(FileNamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var ownerAndNonce = fileName.Substring(FileNamePrefix.Length);
        var separatorIndex = ownerAndNonce.IndexOf('-');
        if (separatorIndex <= 0
            || separatorIndex != ownerAndNonce.LastIndexOf('-')
            || separatorIndex == ownerAndNonce.Length - 1
            || (separatorIndex > 1 && ownerAndNonce[0] == '0'))
        {
            return false;
        }

        var nonce = ownerAndNonce.Substring(separatorIndex + 1);
        return int.TryParse(
                ownerAndNonce.Substring(0, separatorIndex),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out processId)
            && processId > 0
            && IsTemporaryNonce(nonce);
    }

    private static string CreateTemporaryNonce ()
    {
        Span<byte> bytes = stackalloc byte[TemporaryNonceHexCharacterCount / 2];
        RandomNumberGenerator.Fill(bytes);
        Span<char> characters = stackalloc char[TemporaryNonceHexCharacterCount];
        for (var index = 0; index < bytes.Length; index++)
        {
            var value = bytes[index];
            characters[index * 2] = LowerHexCharacters[value >> 4];
            characters[(index * 2) + 1] = LowerHexCharacters[value & 0x0F];
        }

        return new string(characters);
    }

    private static bool IsTemporaryNonce (string value)
    {
        if (value.Length != TemporaryNonceHexCharacterCount)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!((character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
