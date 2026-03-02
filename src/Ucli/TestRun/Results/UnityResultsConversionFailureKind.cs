namespace MackySoft.Ucli.TestRun.Results;

/// <summary> Represents failure kinds produced by Unity results conversion. </summary>
internal enum UnityResultsConversionFailureKind
{
    /// <summary> Indicates input results XML is malformed or semantically invalid. </summary>
    InvalidResultsXml = 0,

    /// <summary> Indicates reading input results XML failed due to filesystem/runtime issues. </summary>
    ResultsXmlReadFailed = 1,

    /// <summary> Indicates writing normalized result artifacts failed. </summary>
    OutputWriteFailed = 2,

    /// <summary> Indicates conversion was canceled by caller request. </summary>
    Canceled = 3,
}