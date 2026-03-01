using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Configuration;

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
    private const int CurrentSchemaVersion = 1;
    private const string DefaultAllowlistPattern = "^ucli\\.";

    /// <summary> Gets the default IPC timeout in milliseconds. </summary>
    public const int DefaultIpcTimeoutMilliseconds = 3000;

    /// <summary> Gets the IPC timeout in milliseconds used when CLI options do not override timeout. </summary>
    public int IpcDefaultTimeoutMilliseconds { get; init; } = DefaultIpcTimeoutMilliseconds;

    /// <summary> Gets per-command IPC timeout overrides in milliseconds. <see langword="null" /> values fallback to <see cref="IpcDefaultTimeoutMilliseconds" />. </summary>
    public IReadOnlyDictionary<string, int?> IpcTimeoutMillisecondsByCommand { get; init; } = new Dictionary<string, int?>(StringComparer.Ordinal);

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
            IpcDefaultTimeoutMilliseconds = DefaultIpcTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand = IpcTimeoutCommandNames.CreateDefaultTimeoutOverrides(),
        };
    }
}