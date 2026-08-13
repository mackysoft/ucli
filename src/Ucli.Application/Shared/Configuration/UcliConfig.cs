using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents parsed values from <c>.ucli/config.json</c>. </summary>
/// <param name="SchemaVersion"> The config schema version. </param>
/// <param name="OperationPolicy"> The allowed operation safety level. </param>
/// <param name="PlanTokenMode"> The plan token requirement level. </param>
/// <param name="ReadIndexDefaultMode"> The default read-index mode used when command options do not override mode. </param>
/// <param name="OperationAllowlist"> The operation-name allowlist patterns. </param>
internal sealed record UcliConfig (
    int SchemaVersion,
    OperationPolicy OperationPolicy,
    PlanTokenMode PlanTokenMode,
    ReadIndexMode ReadIndexDefaultMode,
    IReadOnlyList<string> OperationAllowlist)
{
    internal const int CurrentSchemaVersion = 1;
    private const string DefaultAllowlistPattern = "^ucli\\.";

    /// <summary> Gets the IPC timeout in milliseconds used when CLI options do not override timeout. </summary>
    public int IpcDefaultTimeoutMilliseconds { get; init; } = IpcTimeoutDefaults.GlobalTimeoutMilliseconds;

    /// <summary> Gets whether the dedicated C# evaluation surface is enabled for this project. </summary>
    public bool EvalEnabled { get; init; }

    /// <summary> Gets per-command IPC timeout overrides in milliseconds. <see langword="null" /> values fallback to <see cref="IpcDefaultTimeoutMilliseconds" />. </summary>
    public IReadOnlyDictionary<string, int?> IpcTimeoutMillisecondsByCommand { get; init; } = new Dictionary<string, int?>(StringComparer.Ordinal);

    /// <summary> Gets project-provided Program Preset registrations keyed by their ordinal ID. </summary>
    public IReadOnlyDictionary<string, ProgramPresetRegistration> ProgramPresets { get; init; } = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal);

    /// <summary> Gets whether the dedicated C# evaluation surface is enabled by configuration. </summary>
    public bool EvalEnabled { get; init; }

    /// <summary> Gets the additional Program Presets required to complete Work Close. </summary>
    public UcliWorkCompletion WorkCompletion { get; init; } = UcliWorkCompletion.Empty;

    /// <summary> Creates default configuration values for missing config files. </summary>
    /// <returns> The default config instance. </returns>
    public static UcliConfig CreateDefault ()
    {
        return new UcliConfig(
            SchemaVersion: CurrentSchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                DefaultAllowlistPattern,
            ])
        {
            IpcDefaultTimeoutMilliseconds = IpcTimeoutDefaults.GlobalTimeoutMilliseconds,
            EvalEnabled = false,
            IpcTimeoutMillisecondsByCommand = IpcTimeoutDefaults.CreateDefaultTimeoutOverrides(),
            EvalEnabled = false,
            ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal),
            WorkCompletion = UcliWorkCompletion.Empty,
        };
    }
}
