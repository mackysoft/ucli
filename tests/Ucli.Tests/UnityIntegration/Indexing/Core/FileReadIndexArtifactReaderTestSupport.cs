using System.Text;
using MackySoft.Ucli.Contracts.Configuration;
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
        if (!ContractLiteralCodec.TryParse<UcliOperationKind>(operation.Kind, out var kind)
            || !ContractLiteralCodec.TryParse<OperationPolicy>(operation.Policy, out var policy))
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
