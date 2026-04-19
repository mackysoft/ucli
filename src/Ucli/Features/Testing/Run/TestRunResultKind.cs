namespace MackySoft.Ucli.Features.Testing.Run;

/// <summary> Represents normalized test-run result kinds. </summary>
internal enum TestRunResultKind
{
    /// <summary> Indicates that all tests passed. </summary>
    Pass = 0,

    /// <summary> Indicates that one or more tests failed. </summary>
    Fail = 1,
}