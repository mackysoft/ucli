namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves phase-operation implementations by operation name. </summary>
    internal interface IPhaseOperationRegistry
    {
        /// <summary> Attempts to resolve an operation implementation by operation name. </summary>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="operation"> The resolved implementation when found. </param>
        /// <returns> <see langword="true" /> when operation implementation was found; otherwise <see langword="false" />. </returns>
        bool TryResolve (
            string operationName,
            out IPhaseOperation operation);
    }
}
