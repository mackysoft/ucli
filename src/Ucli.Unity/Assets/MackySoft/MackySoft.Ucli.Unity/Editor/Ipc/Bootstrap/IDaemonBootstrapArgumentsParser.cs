namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Parses daemon bootstrap command-line arguments. </summary>
    internal interface IDaemonBootstrapArgumentsParser
    {
        /// <summary> Parses daemon bootstrap command-line arguments. </summary>
        /// <param name="args"> The process command-line arguments. </param>
        /// <param name="bootstrapArguments"> The parsed bootstrap argument model. </param>
        /// <param name="errorMessage"> The error message when parse fails. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        bool TryParse (
            string[] args,
            out DaemonBootstrapArguments bootstrapArguments,
            out string errorMessage);
    }
}
