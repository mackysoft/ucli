using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Index;

internal static class FileReadIndexArtifactReaderTestSupport
{
    public static void WriteText (
        string path,
        string contents)
    {
        var directoryPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path, contents);
    }

    public static IndexOpsCatalogEntryJsonContract WriteOpsDescribe (
        string storageRoot,
        ProjectFingerprint fingerprint,
        IndexOpEntryJsonContract operation,
        string sourceInputsHash)
    {
        var describeKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(operation.Name!));
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: sourceInputsHash,
            Operation: operation);
        var json = Write(contract);
        var describeHash = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(json));
        WriteText(UcliStoragePathResolver.ResolveOpsDescribePath(storageRoot, fingerprint, describeKey), json);
        return new IndexOpsCatalogEntryJsonContract(
            operation.Name,
            operation.Kind,
            operation.Policy,
            operation.Description,
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

    public static string Write (IndexTypesCatalogJsonContract contract)
    {
        return new IndexTypesCatalogJsonContractWriter().Write(contract);
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
