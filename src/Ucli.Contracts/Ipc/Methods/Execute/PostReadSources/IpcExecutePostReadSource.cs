using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents source facts needed to verify post-read claims from a portable execute result. </summary>
/// <param name="SchemaVersion"> The post-read source schema version. </param>
/// <param name="Steps"> The source facts aligned to <c>opResults[].opId</c>. </param>
public sealed record IpcExecutePostReadSource
{
    /// <summary> Gets the current post-read source schema version. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary> Initializes a post-read source contract. </summary>
    [JsonConstructor]
    public IpcExecutePostReadSource (
        int SchemaVersion,
        IReadOnlyList<IpcExecutePostReadSourceStep> Steps)
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), SchemaVersion, "Post-read source schema version is unsupported.");
        }

        this.SchemaVersion = SchemaVersion;
        this.Steps = ContractArgumentGuard.RequireItems(Steps, nameof(Steps));
    }

    public int SchemaVersion { get; }

    public IReadOnlyList<IpcExecutePostReadSourceStep> Steps { get; }
}
