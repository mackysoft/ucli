using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Creates <c>execute</c> operation result envelope contracts. </summary>
public static class IpcExecuteOperationResultFactory
{
    /// <summary> Creates one plan-phase operation result envelope. </summary>
    /// <param name="opId"> The public step identifier that corresponds to request <c>steps[].id</c>. </param>
    /// <param name="op"> The public step name reported to clients. </param>
    /// <param name="applied"> Whether the step has been applied. </param>
    /// <param name="changed"> Whether the step produced persistent changes. </param>
    /// <param name="touched"> The touched persistence-unit resources. </param>
    /// <param name="result"> The optional query result payload produced by the step. </param>
    /// <returns> The created operation result envelope. </returns>
    public static IpcExecuteOperationResult CreatePlanResult (
        string opId,
        string op,
        bool applied,
        bool changed,
        IReadOnlyList<IpcExecuteTouchedResource> touched,
        JsonElement? result = null)
    {
        return Create(
            opId,
            op,
            IpcExecuteOperationPhaseNames.Plan,
            applied,
            changed,
            touched,
            result);
    }

    /// <summary> Creates one operation result envelope. </summary>
    /// <param name="opId"> The public step identifier that corresponds to request <c>steps[].id</c>. </param>
    /// <param name="op"> The public step name reported to clients. </param>
    /// <param name="phase"> The final phase reached by the step. </param>
    /// <param name="applied"> Whether the step has been applied. </param>
    /// <param name="changed"> Whether the step produced persistent changes. </param>
    /// <param name="touched"> The touched persistence-unit resources. </param>
    /// <param name="result"> The optional query result payload produced by the step. </param>
    /// <returns> The created operation result envelope. </returns>
    /// <exception cref="ArgumentException"> <paramref name="opId" />, <paramref name="op" />, or <paramref name="phase" /> is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> <paramref name="opId" />, <paramref name="op" />, <paramref name="phase" />, or <paramref name="touched" /> is <see langword="null" />. </exception>
    public static IpcExecuteOperationResult Create (
        string opId,
        string op,
        string phase,
        bool applied,
        bool changed,
        IReadOnlyList<IpcExecuteTouchedResource> touched,
        JsonElement? result = null)
    {
        ThrowIfNullOrWhiteSpace(opId, nameof(opId));
        ThrowIfNullOrWhiteSpace(op, nameof(op));
        ThrowIfNullOrWhiteSpace(phase, nameof(phase));
        if (touched == null)
        {
            throw new ArgumentNullException(nameof(touched));
        }

        return new IpcExecuteOperationResult(
            OpId: opId,
            Op: op,
            Phase: phase,
            Applied: applied,
            Changed: changed,
            Touched: touched)
        {
            Result = result,
        };
    }

    private static void ThrowIfNullOrWhiteSpace (
        string value,
        string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty or whitespace.", parameterName);
        }
    }
}
