using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Creates <c>execute</c> operation result envelope contracts. </summary>
public static class IpcExecuteOperationResultFactory
{
    /// <summary> Creates one plan-phase operation result envelope. </summary>
    /// <param name="op"> The public step name reported to clients. </param>
    /// <param name="applied"> Whether the step has been applied. </param>
    /// <param name="changed"> Whether the step produced persistent changes. </param>
    /// <param name="touched"> The touched persistence-unit resources. </param>
    /// <param name="result"> The optional query result payload produced by the step. </param>
    /// <param name="diagnostics"> The diagnostics emitted for the step. </param>
    /// <returns> The created operation result envelope. </returns>
    public static IpcExecuteOperationResult CreatePlanResult (
        string op,
        bool applied,
        bool changed,
        IReadOnlyList<IpcExecuteTouchedResource> touched,
        JsonElement? result = null,
        IReadOnlyList<IpcExecuteDiagnostic>? diagnostics = null)
    {
        return Create(
            op,
            IpcExecuteOperationPhase.Plan,
            applied,
            changed,
            touched,
            result,
            diagnostics);
    }

    /// <summary> Creates one operation result envelope. </summary>
    /// <param name="op"> The public step name reported to clients. </param>
    /// <param name="phase"> The final phase reached by the step. </param>
    /// <param name="applied"> Whether the step has been applied. </param>
    /// <param name="changed"> Whether the step produced persistent changes. </param>
    /// <param name="touched"> The touched persistence-unit resources. </param>
    /// <param name="result"> The optional query result payload produced by the step. </param>
    /// <param name="diagnostics"> The diagnostics emitted for the step. </param>
    /// <returns> The created operation result envelope. </returns>
    /// <exception cref="ArgumentException"> <paramref name="op" /> is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> <paramref name="op" /> or <paramref name="touched" /> is <see langword="null" />. </exception>
    public static IpcExecuteOperationResult Create (
        string op,
        IpcExecuteOperationPhase phase,
        bool applied,
        bool changed,
        IReadOnlyList<IpcExecuteTouchedResource> touched,
        JsonElement? result = null,
        IReadOnlyList<IpcExecuteDiagnostic>? diagnostics = null)
    {
        return new IpcExecuteOperationResult(
            Op: op,
            Phase: phase,
            Applied: applied,
            Changed: changed,
            Touched: touched)
        {
            Result = result,
            Diagnostics = diagnostics ?? Array.Empty<IpcExecuteDiagnostic>(),
        };
    }
}
