using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Adapts the shared strict configuration compiler for existing Application config consumers. </summary>
internal sealed class UcliConfigSchemaValidator
{
    private readonly UcliConfigContractCompiler compiler = new();

    /// <summary> Validates configuration JSON and returns the legacy raw projection for Application callers. </summary>
    public UcliConfigSchemaValidationResult Validate (JsonElement root, string sourcePath)
    {
        var result = Compile(root, sourcePath);
        if (!result.IsSuccess)
        {
            return UcliConfigSchemaValidationResult.Failure(UcliConfigContractDiagnosticMapper.Map(result.Diagnostics));
        }

        var snapshot = result.Snapshot!;
        return UcliConfigSchemaValidationResult.Success(new UcliConfigJsonRawDocument(
            snapshot.SchemaVersion,
            TextVocabulary.GetText(snapshot.OperationPolicy),
            TextVocabulary.GetText(snapshot.PlanTokenMode),
            TextVocabulary.GetText(snapshot.ReadIndexDefaultMode),
            snapshot.OperationAllowlist.ToArray(),
            snapshot.EvalEnabled,
            snapshot.IpcDefaultTimeoutMilliseconds,
            snapshot.IpcTimeoutMillisecondsByCommand is null
                ? null
                : new Dictionary<string, int?>(snapshot.IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal),
            snapshot.ProgramPresets.ToDictionary(
                static entry => entry.Key,
                static entry => new UcliProgramPresetDocument(entry.Value.Description, entry.Value.ProgramPath),
                StringComparer.Ordinal)));
    }

    /// <summary> Compiles the strict shared configuration contract. </summary>
    public UcliConfigContractCompilationResult Compile (JsonElement root, string sourcePath)
    {
        return compiler.Compile(root, sourcePath);
    }
}
