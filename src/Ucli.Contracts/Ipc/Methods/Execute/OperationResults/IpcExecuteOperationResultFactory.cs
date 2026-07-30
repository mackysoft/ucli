using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Creates <c>execute</c> operation result envelope contracts. </summary>
public static class IpcExecuteOperationResultFactory
{
    /// <summary> Creates one direct-operation result that does not establish a verdict. </summary>
    /// <param name="op"> The public direct-operation name reported to clients. </param>
    /// <param name="phase"> The final phase reached by the step. </param>
    /// <param name="applied"> Whether the step has been applied. </param>
    /// <param name="changed"> Whether the step produced persistent changes. </param>
    /// <param name="touched"> The touched persistence-unit resources. </param>
    /// <param name="operationDescriptorDigest"> The exact descriptor digest fixed before execution. </param>
    /// <param name="result"> The optional query result payload produced by the step. </param>
    /// <param name="diagnostics"> The diagnostics emitted for the step. </param>
    /// <returns> The created direct-operation result. </returns>
    /// <exception cref="ArgumentException"> <paramref name="op" /> is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="op" />, <paramref name="touched" />, <paramref name="operationDescriptorDigest" />,
    /// or <paramref name="diagnostics" /> is <see langword="null" />.
    /// </exception>
    public static IpcExecuteOperationResult CreateDirectWithoutVerdict (
        string op,
        IpcExecuteOperationPhase phase,
        bool applied,
        bool changed,
        IReadOnlyList<IpcExecuteTouchedResource> touched,
        Sha256Digest operationDescriptorDigest,
        JsonElement? result,
        IReadOnlyList<IpcExecuteDiagnostic> diagnostics)
    {
        if (operationDescriptorDigest == null)
        {
            throw new ArgumentNullException(nameof(operationDescriptorDigest));
        }

        return new IpcExecuteOperationResult(
            Op: op,
            Phase: phase,
            Applied: applied,
            Changed: changed,
            Touched: touched,
            OperationDescriptorDigest: operationDescriptorDigest,
            Verdict: null,
            Result: result,
            Diagnostics: diagnostics);
    }

    /// <summary> Creates one Edit result without exposing lowered-operation descriptors or results. </summary>
    /// <param name="phase"> The final phase reached by the Edit step. </param>
    /// <param name="applied"> Whether the Edit step has been applied. </param>
    /// <param name="changed"> Whether the Edit step produced persistent changes. </param>
    /// <param name="touched"> The touched persistence-unit resources. </param>
    /// <param name="diagnostics"> The diagnostics emitted for the Edit step. </param>
    /// <returns> The created Edit result. </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="touched" /> or <paramref name="diagnostics" /> is <see langword="null" />.
    /// </exception>
    public static IpcExecuteOperationResult CreateEditResult (
        IpcExecuteOperationPhase phase,
        bool applied,
        bool changed,
        IReadOnlyList<IpcExecuteTouchedResource> touched,
        IReadOnlyList<IpcExecuteDiagnostic> diagnostics)
    {
        return new IpcExecuteOperationResult(
            Op: TextVocabulary.GetText(IpcExecuteStepKind.Edit),
            Phase: phase,
            Applied: applied,
            Changed: changed,
            Touched: touched,
            OperationDescriptorDigest: null,
            Verdict: null,
            Result: null,
            Diagnostics: diagnostics);
    }

    /// <summary> Creates a successful judging Call result with its result evidence. </summary>
    /// <param name="op"> The public direct-operation name. </param>
    /// <param name="applied"> Whether the operation was applied. </param>
    /// <param name="changed"> Whether the operation produced persistent changes. </param>
    /// <param name="touched"> The touched persistence-unit resources. </param>
    /// <param name="operationDescriptorDigest"> The exact descriptor digest used to dispatch the operation. </param>
    /// <param name="verdict"> The verdict established from <paramref name="result" />. </param>
    /// <param name="result"> The result evidence evaluated to establish <paramref name="verdict" />. </param>
    /// <param name="diagnostics"> The diagnostics emitted for the operation. </param>
    /// <returns> The created judging Call result. </returns>
    public static IpcExecuteOperationResult CreateJudgingCallResult (
        string op,
        bool applied,
        bool changed,
        IReadOnlyList<IpcExecuteTouchedResource> touched,
        Sha256Digest operationDescriptorDigest,
        Verdict verdict,
        JsonElement result,
        IReadOnlyList<IpcExecuteDiagnostic> diagnostics)
    {
        if (operationDescriptorDigest == null)
        {
            throw new ArgumentNullException(nameof(operationDescriptorDigest));
        }

        return new IpcExecuteOperationResult(
            Op: op,
            Phase: IpcExecuteOperationPhase.Call,
            Applied: applied,
            Changed: changed,
            Touched: touched,
            OperationDescriptorDigest: operationDescriptorDigest,
            Verdict: verdict,
            Result: result,
            Diagnostics: diagnostics);
    }
}
