using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements byte-range export from Unity editor log file. </summary>
    internal sealed class EditorLogRangeExporter : IEditorLogRangeExporter
    {
        private const int BufferSize = 81920;
        private static readonly UTF8Encoding Utf8NoBomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary> Exports one half-open byte range from source log file into destination file. </summary>
        /// <param name="sourcePath"> The source log file path. </param>
        /// <param name="destinationPath"> The destination artifact file path. </param>
        /// <param name="startOffset"> The inclusive start byte offset. </param>
        /// <param name="endOffset"> The exclusive end byte offset. </param>
        /// <param name="redactionValues"> The sensitive values to redact while writing the artifact. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <returns> The counters collected from the exported log range. </returns>
        public async Task<EditorLogRangeExportResult> ExportRangeAsync (
            string sourcePath,
            string destinationPath,
            long startOffset,
            long endOffset,
            IEnumerable<string>? redactionValues = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path must not be null or whitespace.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("Destination path must not be null or whitespace.", nameof(destinationPath));
            }

            if (startOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startOffset), "Start offset must be greater than or equal to zero.");
            }

            if (endOffset < startOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(endOffset), "End offset must be greater than or equal to start offset.");
            }

            var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
            {
                throw new InvalidOperationException($"Destination directory path could not be resolved: {destinationPath}");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(destinationDirectoryPath);
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, true);
            if (startOffset > sourceStream.Length || endOffset > sourceStream.Length)
            {
                throw new InvalidOperationException(
                    $"Editor log offset is out of range. start={startOffset}, end={endOffset}, length={sourceStream.Length}.");
            }

            var temporaryPath = ProcessOwnedTemporaryFilePath.Create(destinationPath);
            try
            {
                var counter = new BuildLogLineCounter();
                var redactionPatterns = CreateRedactionPatterns(redactionValues);
                var redactionPending = redactionPatterns.Count == 0 ? null : new List<byte>();
                using (var destinationStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, true))
                {
                    sourceStream.Seek(startOffset, SeekOrigin.Begin);
                    var remaining = endOffset - startOffset;
                    var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                    try
                    {
                        while (remaining > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var readLength = remaining > BufferSize
                                ? BufferSize
                                : (int)remaining;
                            var bytesRead = await sourceStream.ReadAsync(buffer, 0, readLength, cancellationToken);
                            if (bytesRead == 0)
                            {
                                throw new EndOfStreamException("Unexpected end of editor log while exporting selected byte range.");
                            }

                            counter.Append(buffer.AsSpan(0, bytesRead));
                            if (redactionPatterns.Count == 0)
                            {
                                await destinationStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            }
                            else
                            {
                                var redacted = RedactChunk(
                                    buffer.AsSpan(0, bytesRead),
                                    redactionPending!,
                                    redactionPatterns,
                                    endOfInput: false);
                                if (redacted.Length > 0)
                                {
                                    await destinationStream.WriteAsync(redacted, 0, redacted.Length, cancellationToken);
                                }
                            }

                            remaining -= bytesRead;
                        }

                        if (redactionPatterns.Count != 0)
                        {
                            var redacted = RedactChunk(
                                ReadOnlySpan<byte>.Empty,
                                redactionPending!,
                                redactionPatterns,
                                endOfInput: true);
                            if (redacted.Length > 0)
                            {
                                await destinationStream.WriteAsync(redacted, 0, redacted.Length, cancellationToken);
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureFile(temporaryPath);
                EnsureWritableArtifactPath(destinationPath);
                ReplaceFile(temporaryPath, destinationPath);
                FileSystemAccessBoundary.EnsureSecureFile(destinationPath);
                return counter.Complete();
            }
            finally
            {
                DeleteTemporaryFileIfExists(temporaryPath);
            }
        }

        private static IReadOnlyList<byte[]> CreateRedactionPatterns (IEnumerable<string>? redactionValues)
        {
            if (redactionValues == null)
            {
                return Array.Empty<byte[]>();
            }

            var orderedValues = SensitiveValueRedactor.CreateOrderedValues(redactionValues);
            if (orderedValues.Length == 0)
            {
                return Array.Empty<byte[]>();
            }

            var patterns = new List<byte[]>(orderedValues.Length);
            for (var i = 0; i < orderedValues.Length; i++)
            {
                patterns.Add(Utf8NoBomEncoding.GetBytes(orderedValues[i]));
            }

            return patterns;
        }

        private static byte[] RedactChunk (
            ReadOnlySpan<byte> bytes,
            List<byte> pending,
            IReadOnlyList<byte[]> redactionPatterns,
            bool endOfInput)
        {
            var output = new ArrayBufferWriter<byte>(bytes.Length + SensitiveValueRedactor.Replacement.Length);
            for (var i = 0; i < bytes.Length; i++)
            {
                pending.Add(bytes[i]);
                FlushDecidedPending(pending, redactionPatterns, output, endOfInput: false);
            }

            if (endOfInput)
            {
                FlushDecidedPending(pending, redactionPatterns, output, endOfInput: true);
            }

            return output.WrittenMemory.ToArray();
        }

        private static void FlushDecidedPending (
            List<byte> pending,
            IReadOnlyList<byte[]> redactionPatterns,
            ArrayBufferWriter<byte> output,
            bool endOfInput)
        {
            while (pending.Count > 0)
            {
                var matchedLength = FindFullMatchLength(pending, redactionPatterns);
                if (matchedLength > 0)
                {
                    if (!endOfInput && HasLongerPendingPrefixMatch(pending, redactionPatterns, matchedLength))
                    {
                        return;
                    }

                    WriteUtf8(output, SensitiveValueRedactor.Replacement);
                    pending.RemoveRange(0, matchedLength);
                    continue;
                }

                if (!endOfInput && HasPendingPrefixMatch(pending, redactionPatterns))
                {
                    return;
                }

                output.GetSpan(1)[0] = pending[0];
                output.Advance(1);
                pending.RemoveAt(0);
            }
        }

        private static int FindFullMatchLength (
            List<byte> pending,
            IReadOnlyList<byte[]> redactionPatterns)
        {
            for (var i = 0; i < redactionPatterns.Count; i++)
            {
                var pattern = redactionPatterns[i];
                if (pending.Count >= pattern.Length && MatchesPrefix(pending, pattern, pattern.Length))
                {
                    return pattern.Length;
                }
            }

            return 0;
        }

        private static bool HasPendingPrefixMatch (
            List<byte> pending,
            IReadOnlyList<byte[]> redactionPatterns)
        {
            for (var i = 0; i < redactionPatterns.Count; i++)
            {
                var pattern = redactionPatterns[i];
                if (pending.Count < pattern.Length && MatchesPrefix(pending, pattern, pending.Count))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLongerPendingPrefixMatch (
            List<byte> pending,
            IReadOnlyList<byte[]> redactionPatterns,
            int matchedLength)
        {
            for (var i = 0; i < redactionPatterns.Count; i++)
            {
                var pattern = redactionPatterns[i];
                if (pattern.Length > matchedLength
                    && pending.Count < pattern.Length
                    && MatchesPrefix(pending, pattern, pending.Count))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesPrefix (
            List<byte> pending,
            byte[] pattern,
            int length)
        {
            for (var i = 0; i < length; i++)
            {
                if (pending[i] != pattern[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteUtf8 (
            ArrayBufferWriter<byte> output,
            string value)
        {
            var byteCount = Utf8NoBomEncoding.GetByteCount(value);
            var span = output.GetSpan(byteCount);
            var written = Utf8NoBomEncoding.GetBytes(value.AsSpan(), span);
            output.Advance(written);
        }

        private static void EnsureWritableArtifactPath (string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Build log artifact target must not be a reparse point: {path}");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException($"Build log artifact target must not be a directory: {path}");
            }
        }

        private static void ReplaceFile (
            string temporaryPath,
            string path)
        {
            try
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            catch (FileNotFoundException)
            {
                MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
            }
            catch (IOException) when (!File.Exists(path))
            {
                MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
            }
        }

        private static void MoveOrReplaceWhenCreatedConcurrently (
            string temporaryPath,
            string path)
        {
            try
            {
                File.Move(temporaryPath, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                EnsureWritableArtifactPath(path);
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
        }

        private static void DeleteTemporaryFileIfExists (string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed class BuildLogLineCounter
        {
            private const string ColonErrorPattern = ": error ";
            private const string ColonWarningPattern = ": warning ";
            private const string BracketErrorPattern = "[error]";
            private const string BracketWarnPattern = "[warn]";
            private const string BracketWarningPattern = "[warning]";
            private const string PrefixWarnSpacePattern = "warn ";
            private const string PrefixWarnColonPattern = "warn:";
            private const string PrefixErrorSpacePattern = "error ";
            private const string PrefixErrorColonPattern = "error:";
            private const string PrefixWarningSpacePattern = "warning ";
            private const string PrefixWarningColonPattern = "warning:";

            private int entryCount;

            private int errorCount;

            private int warningCount;

            private int colonErrorMatchLength;

            private int colonWarningMatchLength;

            private int bracketErrorMatchLength;

            private int bracketWarnMatchLength;

            private int bracketWarningMatchLength;

            private int prefixWarnSpaceMatchLength;

            private int prefixWarnColonMatchLength;

            private int prefixErrorSpaceMatchLength;

            private int prefixErrorColonMatchLength;

            private int prefixWarningSpaceMatchLength;

            private int prefixWarningColonMatchLength;

            private bool hasLineContent;

            private bool lineHasError;

            private bool lineHasWarning;

            private bool canMatchLinePrefix = true;

            private bool isAnsiEscape;

            private bool isAnsiControlSequence;

            public void Append (ReadOnlySpan<byte> bytes)
            {
                for (var i = 0; i < bytes.Length; i++)
                {
                    var value = bytes[i];
                    if (value == '\n')
                    {
                        CompleteLine();
                        continue;
                    }

                    if (value == '\r')
                    {
                        continue;
                    }

                    if (ShouldIgnoreForSeverity(value))
                    {
                        continue;
                    }

                    hasLineContent = true;
                    var lowered = ToLowerAscii(value);
                    ObserveAnywherePattern(lowered, ColonErrorPattern, ref colonErrorMatchLength, ref lineHasError);
                    ObserveAnywherePattern(lowered, BracketErrorPattern, ref bracketErrorMatchLength, ref lineHasError);
                    if (!lineHasError)
                    {
                        ObserveAnywherePattern(lowered, ColonWarningPattern, ref colonWarningMatchLength, ref lineHasWarning);
                        ObserveAnywherePattern(lowered, BracketWarnPattern, ref bracketWarnMatchLength, ref lineHasWarning);
                        ObserveAnywherePattern(lowered, BracketWarningPattern, ref bracketWarningMatchLength, ref lineHasWarning);
                        ObservePrefixPatterns(lowered, value);
                    }
                }
            }

            public EditorLogRangeExportResult Complete ()
            {
                if (hasLineContent)
                {
                    CompleteLine();
                }

                return new EditorLogRangeExportResult(entryCount, errorCount, warningCount);
            }

            private void CompleteLine ()
            {
                if (!hasLineContent)
                {
                    ResetLineState();
                    return;
                }

                entryCount++;
                if (lineHasError)
                {
                    errorCount++;
                }
                else if (lineHasWarning)
                {
                    warningCount++;
                }

                ResetLineState();
            }

            private void ResetLineState ()
            {
                colonErrorMatchLength = 0;
                colonWarningMatchLength = 0;
                bracketErrorMatchLength = 0;
                bracketWarnMatchLength = 0;
                bracketWarningMatchLength = 0;
                prefixWarnSpaceMatchLength = 0;
                prefixWarnColonMatchLength = 0;
                prefixErrorSpaceMatchLength = 0;
                prefixErrorColonMatchLength = 0;
                prefixWarningSpaceMatchLength = 0;
                prefixWarningColonMatchLength = 0;
                hasLineContent = false;
                lineHasError = false;
                lineHasWarning = false;
                canMatchLinePrefix = true;
                isAnsiEscape = false;
                isAnsiControlSequence = false;
            }

            private void ObservePrefixPatterns (
                byte lowered,
                byte original)
            {
                if (!canMatchLinePrefix)
                {
                    return;
                }

                if (IsAsciiHorizontalWhitespace(original)
                    && prefixWarnSpaceMatchLength == 0
                    && prefixWarnColonMatchLength == 0
                    && prefixErrorSpaceMatchLength == 0
                    && prefixErrorColonMatchLength == 0
                    && prefixWarningSpaceMatchLength == 0
                    && prefixWarningColonMatchLength == 0)
                {
                    return;
                }

                if (ObservePrefixPattern(lowered, PrefixErrorSpacePattern, ref prefixErrorSpaceMatchLength)
                    || ObservePrefixPattern(lowered, PrefixErrorColonPattern, ref prefixErrorColonMatchLength))
                {
                    lineHasError = true;
                    canMatchLinePrefix = false;
                    return;
                }

                if (ObservePrefixPattern(lowered, PrefixWarnSpacePattern, ref prefixWarnSpaceMatchLength)
                    || ObservePrefixPattern(lowered, PrefixWarnColonPattern, ref prefixWarnColonMatchLength)
                    || ObservePrefixPattern(lowered, PrefixWarningSpacePattern, ref prefixWarningSpaceMatchLength)
                    || ObservePrefixPattern(lowered, PrefixWarningColonPattern, ref prefixWarningColonMatchLength))
                {
                    lineHasWarning = true;
                    canMatchLinePrefix = false;
                    return;
                }

                if (prefixWarnSpaceMatchLength < 0
                    && prefixWarnColonMatchLength < 0
                    && prefixErrorSpaceMatchLength < 0
                    && prefixErrorColonMatchLength < 0
                    && prefixWarningSpaceMatchLength < 0
                    && prefixWarningColonMatchLength < 0)
                {
                    canMatchLinePrefix = false;
                }
            }

            private static void ObserveAnywherePattern (
                byte lowered,
                string pattern,
                ref int matchLength,
                ref bool lineMatched)
            {
                if (lineMatched)
                {
                    return;
                }

                if (lowered == pattern[matchLength])
                {
                    matchLength++;
                    if (matchLength == pattern.Length)
                    {
                        lineMatched = true;
                        matchLength = 0;
                    }

                    return;
                }

                matchLength = lowered == pattern[0] ? 1 : 0;
            }

            private static bool ObservePrefixPattern (
                byte lowered,
                string pattern,
                ref int matchLength)
            {
                if (matchLength < 0)
                {
                    return false;
                }

                if (lowered == pattern[matchLength])
                {
                    matchLength++;
                    if (matchLength == pattern.Length)
                    {
                        matchLength = -1;
                        return true;
                    }

                    return false;
                }

                matchLength = -1;
                return false;
            }

            private bool ShouldIgnoreForSeverity (byte value)
            {
                if (isAnsiEscape)
                {
                    if (!isAnsiControlSequence)
                    {
                        if (value == '[')
                        {
                            isAnsiControlSequence = true;
                        }
                        else
                        {
                            isAnsiEscape = false;
                        }

                        return true;
                    }

                    if (value >= 0x40 && value <= 0x7E)
                    {
                        isAnsiEscape = false;
                        isAnsiControlSequence = false;
                    }

                    return true;
                }

                if (value == 0x1B)
                {
                    isAnsiEscape = true;
                    isAnsiControlSequence = false;
                    return true;
                }

                return false;
            }

            private static bool IsAsciiHorizontalWhitespace (byte value)
            {
                return value == ' ' || value == '\t';
            }

            private static byte ToLowerAscii (byte value)
            {
                return value >= (byte)'A' && value <= (byte)'Z'
                    ? (byte)(value + ('a' - 'A'))
                    : value;
            }
        }
    }
}
