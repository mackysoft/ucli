namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Holds source text after eval source form normalization. </summary>
    internal sealed class CsEvalPreparedSource
    {
        public CsEvalPreparedSource (
            string sourceKind,
            string compilationSource,
            string wrapperVersion)
        {
            SourceKind = sourceKind;
            CompilationSource = compilationSource;
            WrapperVersion = wrapperVersion;
        }

        public string SourceKind { get; }

        public string CompilationSource { get; }

        public string WrapperVersion { get; }
    }
}
