using System.Text;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

internal static class FileReadIndexArtifactReaderTestSupport
{
    public static FileReadIndexArtifactReader CreateReader ()
    {
        return new FileReadIndexArtifactReader(CreateGenerationStore());
    }

    public static FileReadIndexGenerationStore CreateGenerationStore ()
    {
        return new FileReadIndexGenerationStore(
            new FileReadIndexGenerationPointerStore(),
            TimeProvider.System);
    }

    public static Guid EnsureCurrentGeneration (
        string storageRoot,
        ProjectFingerprint fingerprint)
    {
        var pointerPath = UcliStoragePathResolver.ResolveReadIndexCurrentGenerationPath(storageRoot, fingerprint);
        var persistedValue = FileUtilities.ReadAllTextOrNull(pointerPath);
        if (persistedValue != null)
        {
            return Guid.ParseExact(persistedValue, "N");
        }

        var generationId = Guid.NewGuid();
        Directory.CreateDirectory(UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            storageRoot,
            fingerprint,
            generationId));
        FileUtilities.WriteAllTextAtomically(pointerPath, generationId.ToString("N"));
        return generationId;
    }

    public static void WriteText (
        string path,
        string contents)
    {
        var directoryPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path, contents);
    }

    public static ValidatedOpsCatalogEntry WriteOpsDescribe (
        string storageRoot,
        ProjectFingerprint fingerprint,
        IndexOpEntryJsonContract operation,
        Sha256Digest sourceInputsHash)
    {
        var describeKey = Sha256Digest.Compute(Encoding.UTF8.GetBytes(operation.Name!));
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: sourceInputsHash.ToString(),
            Operation: operation);
        var json = Write(contract);
        var describeHash = Sha256Digest.Compute(Encoding.UTF8.GetBytes(json));
        WriteText(UcliStoragePathResolver.ResolveOpsDescribePath(storageRoot, fingerprint, describeKey), json);
        if (!TextVocabulary.TryGetValue<UcliOperationKind>(operation.Kind, out var kind)
            || !TextVocabulary.TryGetValue<OperationPolicy>(operation.Policy, out var policy))
        {
            throw new InvalidOperationException("Operation fixture must use canonical kind and policy literals.");
        }

        return new ValidatedOpsCatalogEntry(
            operation.Name!,
            kind,
            policy,
            operation.Description!,
            describeKey,
            describeHash);
    }

    public static string Write (IndexOpsCatalogJsonContract contract)
    {
        return new IndexOpsCatalogJsonContractWriter().Write(contract);
    }

    public static string Write (IndexOpsDescribeJsonContract contract)
    {
        return new IndexOpsDescribeJsonContractWriter().Write(contract);
    }

    public static string Write (IndexAssetSearchLookupJsonContract contract)
    {
        return new IndexAssetSearchLookupJsonContractWriter().Write(contract);
    }

    public static string Write (IndexSceneTreeLiteLookupJsonContract contract)
    {
        return new IndexSceneTreeLiteLookupJsonContractWriter().Write(contract);
    }
}
