using System.IO.Compression;
using System.Text;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Exports official SKILL packages to a host-materialized output directory. </summary>
public sealed class SkillExportService
{
    private static readonly DateTimeOffset ZipEntryTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SkillMaterializationService materializationService;

    /// <summary> Initializes a new instance of the <see cref="SkillExportService" /> class. </summary>
    /// <param name="materializationService"> The materialization service. </param>
    public SkillExportService (SkillMaterializationService materializationService)
    {
        this.materializationService = materializationService ?? throw new ArgumentNullException(nameof(materializationService));
    }

    /// <summary> Exports all packages into an output root. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="host"> The target host. </param>
    /// <param name="outputRoot"> The output root directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The output root or failure. </returns>
    public ValueTask<SkillOperationResult<string>> ExportAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string host,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        return ExportAsync(packages, host, outputRoot, SkillExportFormat.Directory, cancellationToken);
    }

    /// <summary> Exports all packages into an output path. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="host"> The target host. </param>
    /// <param name="outputRoot"> The output directory or zip file path. </param>
    /// <param name="format"> The output format. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The output path or failure. </returns>
    public async ValueTask<SkillOperationResult<string>> ExportAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string host,
        string outputRoot,
        SkillExportFormat format = SkillExportFormat.Directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        return format switch
        {
            SkillExportFormat.Directory => await ExportDirectoryAsync(packages, host, outputRoot, cancellationToken).ConfigureAwait(false),
            SkillExportFormat.Zip => await ExportZipAsync(packages, host, outputRoot, cancellationToken).ConfigureAwait(false),
            _ => SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Unsupported SKILL export format: {format}"),
        };
    }

    private async ValueTask<SkillOperationResult<string>> ExportDirectoryAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string host,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
        {
            var materializedResult = materializationService.Materialize(package, host);
            if (!materializedResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
            }

            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(fullOutputRoot, package.Manifest.SkillName);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            foreach (var file in materializedResult.Value!.Files)
            {
                var filePathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(fullOutputRoot, skillDirectory, file.RelativePath);
                if (!filePathResult.IsSuccess)
                {
                    return SkillOperationResult<string>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
                }

                await SkillPackageFileWriter.WriteAllTextAtomically(filePathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        return SkillOperationResult<string>.Success(fullOutputRoot);
    }

    private async ValueTask<SkillOperationResult<string>> ExportZipAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string host,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"SKILL export zip output path is invalid: {fullOutputPath}");
        }

        var zipEntries = new List<SkillZipEntry>();
        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var materializedResult = materializationService.Materialize(package, host);
            if (!materializedResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
            }

            foreach (var file in materializedResult.Value!.Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
            {
                var entryPath = $"{package.Manifest.SkillName}/{file.RelativePath}";
                if (!SkillRelativePath.IsSafeFilePath(entryPath))
                {
                    return SkillOperationResult<string>.FailureResult(
                        SkillFailureCodes.PathUnsafe,
                        $"SKILL export zip entry path is unsafe: {entryPath}");
                }

                zipEntries.Add(new SkillZipEntry(entryPath, file.Content));
            }
        }

        var temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");
        var committed = false;
        try
        {
            Directory.CreateDirectory(outputDirectory);
            await using (var fileStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                foreach (var entry in zipEntries.OrderBy(static entry => entry.EntryPath, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var archiveEntry = archive.CreateEntry(entry.EntryPath, CompressionLevel.Optimal);
                    archiveEntry.LastWriteTime = ZipEntryTimestamp;
                    await using var entryStream = archiveEntry.Open();
                    var bytes = Encoding.UTF8.GetBytes(entry.Content);
                    await entryStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(temporaryPath, fullOutputPath, overwrite: true);
            committed = true;
            return SkillOperationResult<string>.Success(fullOutputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to export SKILL zip: {fullOutputPath}. {ex.Message}");
        }
        finally
        {
            if (!committed)
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }
    }

    private static void TryDeleteTemporaryFile (string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record SkillZipEntry (
        string EntryPath,
        string Content);
}
