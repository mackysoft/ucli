namespace MackySoft.Ucli.Init
{
    /// <summary> Executes initialization flow for generating the <c>.ucli</c> template files. </summary>
    internal interface IInitService
    {
        /// <summary> Executes initialization for a target UnityProject. </summary>
        /// <param name="force"> Whether existing files can be overwritten. </param>
        /// <param name="projectPath"> The optional <c>--projectPath</c> value. When <see langword="null" />, empty, or whitespace, the current working directory is used. </param>
        /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
        /// <returns> A task that resolves to the initialization execution result that contains either generated file paths or a structured error. </returns>
        ValueTask<InitExecutionResult> Execute (
            bool force,
            string? projectPath,
            CancellationToken cancellationToken = default);
    }
}