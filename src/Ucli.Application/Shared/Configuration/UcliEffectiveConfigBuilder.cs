using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Maps a shared strict configuration snapshot to Application-specific effective values. </summary>
internal sealed class UcliEffectiveConfigBuilder
{
    /// <summary> Maps one validated snapshot without repeating contract validation. </summary>
    public UcliConfigBuildResult Build (UcliConfigContractSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var programPresets = snapshot.ProgramPresets.ToDictionary(
            static entry => entry.Key,
            static entry => new ProgramPresetRegistration(entry.Value.Description, RootRelativePath.Parse(entry.Value.ProgramPath)),
            StringComparer.Ordinal);
        return UcliConfigBuildResult.Success(new UcliConfig(
            snapshot.SchemaVersion,
            snapshot.OperationPolicy,
            snapshot.PlanTokenMode,
            snapshot.ReadIndexDefaultMode,
            snapshot.OperationAllowlist.ToArray())
        {
            EvalEnabled = snapshot.EvalEnabled,
            IpcDefaultTimeoutMilliseconds = snapshot.IpcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand = BuildTimeoutOverrides(snapshot.IpcTimeoutMillisecondsByCommand),
            ProgramPresets = programPresets,
        });
    }

    private static IReadOnlyDictionary<string, int?> BuildTimeoutOverrides (IReadOnlyDictionary<string, int?>? overrides)
    {
        return overrides is null
            ? IpcTimeoutDefaults.CreateDefaultTimeoutOverrides()
            : new Dictionary<string, int?>(overrides, StringComparer.Ordinal);
    }
}
