using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes <c>inputs/manifest.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class IndexInputsManifestJsonContractWriter : IndexJsonContractWriterBase<IndexInputsManifestJsonContract>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        IndexInputsManifestJsonContract contract)
    {
        writer.WriteStartObject();
        WriteRootHeader(writer, contract.SchemaVersion, contract.GeneratedAtUtc);
        WriteNullableString(writer, "scriptAssembliesHash", contract.ScriptAssembliesHash);
        WriteNullableString(writer, "packagesManifestHash", contract.PackagesManifestHash);
        WriteNullableString(writer, "packagesLockHash", contract.PackagesLockHash);
        WriteNullableString(writer, "assemblyDefinitionHash", contract.AssemblyDefinitionHash);
        WriteNullableString(writer, "assetsContentHash", contract.AssetsContentHash);
        WriteNullableString(writer, "assetSearchHash", contract.AssetSearchHash);
        WriteNullableString(writer, "guidPathHash", contract.GuidPathHash);
        WriteNullableString(writer, "combinedHash", contract.CombinedHash);
        writer.WriteEndObject();
    }
}
