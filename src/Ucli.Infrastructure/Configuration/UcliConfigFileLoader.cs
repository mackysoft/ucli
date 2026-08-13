using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Configuration;

/// <summary> Loads the canonical project configuration file for all runtime surfaces. </summary>
internal sealed class UcliConfigFileLoader
{
    private readonly UcliConfigContractCompiler compiler = new();

    /// <summary> Loads and strictly compiles <c>.ucli/config.json</c> below the repository root. </summary>
    public async ValueTask<UcliConfigFileLoadResult> LoadAsync (
        AbsolutePath repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        if (repositoryRoot is null)
        {
            throw new ArgumentNullException(nameof(repositoryRoot));
        }
        cancellationToken.ThrowIfCancellationRequested();

        var configPath = UcliStoragePathResolver.ResolveConfigPath(repositoryRoot);
        string? contents;
        try
        {
            contents = await FileUtilities.ReadAllTextOrNullAsync(configPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UcliConfigFileLoadResult.Unavailable(configPath.Value, exception.Message);
        }

        if (contents is null)
        {
            return UcliConfigFileLoadResult.Default(configPath.Value, CreateDefaultSnapshot());
        }

        try
        {
            using var document = JsonDocument.Parse(contents);
            var result = compiler.Compile(document.RootElement, configPath.Value);
            return result.IsSuccess
                ? UcliConfigFileLoadResult.File(configPath.Value, result.Snapshot!)
                : UcliConfigFileLoadResult.Invalid(configPath.Value, result.Diagnostics);
        }
        catch (JsonException exception)
        {
            return UcliConfigFileLoadResult.Invalid(configPath.Value,
            [
                new UcliConfigContractDiagnostic(
                    "config.json.invalid",
                    propertyPath: null,
                    configPath.Value,
                    $"Config JSON is invalid: {exception.Message}"),
            ]);
        }
    }

    private static UcliConfigContractSnapshot CreateDefaultSnapshot ()
    {
        return new UcliConfigContractSnapshot(
            UcliConfigContractCompiler.CurrentSchemaVersion,
            OperationPolicy.Safe,
            PlanTokenMode.Optional,
            ReadIndexMode.RequireFresh,
            ["^ucli\\."],
            EvalEnabled: false,
            UcliConfigContractCompiler.DefaultIpcTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand: null,
            ProgramPresets: new Dictionary<string, UcliConfigContractProgramPreset>(StringComparer.Ordinal),
            RequiredProgramPresets: Array.Empty<string>());
    }
}

/// <summary> Identifies the source state of one project configuration load. </summary>
internal enum UcliConfigFileLoadState
{
    Default = 0,
    File = 1,
    Invalid = 2,
    Unavailable = 3,
}

/// <summary> Represents a filesystem configuration load before Application-specific mapping. </summary>
internal sealed record UcliConfigFileLoadResult (
    UcliConfigFileLoadState State,
    string ConfigPath,
    UcliConfigContractSnapshot? Snapshot,
    IReadOnlyList<UcliConfigContractDiagnostic> Diagnostics,
    string? UnavailableMessage)
{
    public static UcliConfigFileLoadResult Default (string path, UcliConfigContractSnapshot snapshot) => new(UcliConfigFileLoadState.Default, path, snapshot, Array.Empty<UcliConfigContractDiagnostic>(), null);

    public static UcliConfigFileLoadResult File (string path, UcliConfigContractSnapshot snapshot) => new(UcliConfigFileLoadState.File, path, snapshot, Array.Empty<UcliConfigContractDiagnostic>(), null);

    public static UcliConfigFileLoadResult Invalid (string path, IReadOnlyList<UcliConfigContractDiagnostic> diagnostics) => new(UcliConfigFileLoadState.Invalid, path, null, diagnostics, null);

    public static UcliConfigFileLoadResult Unavailable (string path, string message) => new(UcliConfigFileLoadState.Unavailable, path, null, Array.Empty<UcliConfigContractDiagnostic>(), message);
}
