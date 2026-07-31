using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.diagnostic</c> stream payload. </summary>
public sealed record CompileDiagnosticEntry
{
    /// <summary> Initializes one compile diagnostic entry for a non-empty execution identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="ExecutionId" /> is empty. </exception>
    [JsonConstructor]
    public CompileDiagnosticEntry (
        Guid ExecutionId,
        CompileLifecycleRefreshOrigin RefreshOrigin,
        UnityEditorPrimaryDiagnostic? PrimaryDiagnostic)
    {
        if (ExecutionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution id must not be empty.",
                nameof(ExecutionId));
        }
        if (!TextVocabulary.IsDefined(RefreshOrigin))
        {
            throw new ArgumentOutOfRangeException(nameof(RefreshOrigin), RefreshOrigin, "Compile refresh origin must be defined.");
        }

        this.ExecutionId = ExecutionId;
        this.RefreshOrigin = RefreshOrigin;
        this.PrimaryDiagnostic = PrimaryDiagnostic;
    }

    public Guid ExecutionId { get; }

    public CompileLifecycleRefreshOrigin RefreshOrigin { get; }

    public UnityEditorPrimaryDiagnostic? PrimaryDiagnostic { get; }
}
