using MackySoft.Ucli.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Features.Testing.Run.Execution;
using MackySoft.Ucli.Features.Testing.Run.Results;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Pipeline;

/// <summary> Represents one execution pipeline outcome. </summary>
/// <param name="Session"> The run artifacts session when prepared; otherwise <see langword="null" />. </param>
/// <param name="UnityExecutionResult"> The Unity execution result when pipeline reached execution stage; otherwise <see langword="null" />. </param>
/// <param name="ConversionResult"> The conversion result when pipeline reached conversion stage; otherwise <see langword="null" />. </param>
/// <param name="Error"> The pipeline infrastructure error when pipeline failed before final mapping; otherwise <see langword="null" />. </param>
internal sealed record TestRunExecutionPipelineResult (
    ArtifactsSession? Session,
    UnityTestExecutionResult? UnityExecutionResult,
    UnityResultsConversionResult? ConversionResult,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether pipeline completed without infrastructure errors. </summary>
    public bool IsSuccess => Error is null
        && Session is not null
        && UnityExecutionResult is not null
        && ConversionResult is not null;

    /// <summary> Creates one successful pipeline result. </summary>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <param name="unityExecutionResult"> The Unity execution result. </param>
    /// <param name="conversionResult"> The conversion result. </param>
    /// <returns> The successful pipeline result. </returns>
    public static TestRunExecutionPipelineResult Success (
        ArtifactsSession session,
        UnityTestExecutionResult unityExecutionResult,
        UnityResultsConversionResult conversionResult)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(unityExecutionResult);
        ArgumentNullException.ThrowIfNull(conversionResult);

        return new TestRunExecutionPipelineResult(
            Session: session,
            UnityExecutionResult: unityExecutionResult,
            ConversionResult: conversionResult,
            Error: null);
    }

    /// <summary> Creates one failed pipeline result with infrastructure error. </summary>
    /// <param name="error"> The pipeline infrastructure error. </param>
    /// <param name="session"> The optional prepared artifacts session. </param>
    /// <param name="unityExecutionResult"> The optional Unity execution result. </param>
    /// <param name="conversionResult"> The optional conversion result. </param>
    /// <returns> The failed pipeline result. </returns>
    public static TestRunExecutionPipelineResult Failure (
        ExecutionError error,
        ArtifactsSession? session = null,
        UnityTestExecutionResult? unityExecutionResult = null,
        UnityResultsConversionResult? conversionResult = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new TestRunExecutionPipelineResult(
            Session: session,
            UnityExecutionResult: unityExecutionResult,
            ConversionResult: conversionResult,
            Error: error);
    }
}