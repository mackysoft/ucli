using System.Xml;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Implements conversion orchestration from Unity results XML into normalized JSON artifacts. </summary>
internal sealed class UnityResultsConverter : IUnityResultsConverter
{
    private readonly IUnityResultsXmlParser xmlParser;

    private readonly IUnityResultsArtifactWriter artifactWriter;

    /// <summary> Initializes a new instance of the <see cref="UnityResultsConverter" /> class. </summary>
    /// <param name="xmlParser"> The results XML parser dependency. </param>
    /// <param name="artifactWriter"> The results artifact writer dependency. </param>
    public UnityResultsConverter (
        IUnityResultsXmlParser xmlParser,
        IUnityResultsArtifactWriter artifactWriter)
    {
        this.xmlParser = xmlParser ?? throw new ArgumentNullException(nameof(xmlParser));
        this.artifactWriter = artifactWriter ?? throw new ArgumentNullException(nameof(artifactWriter));
    }

    /// <summary> Converts one artifacts session results XML into normalized JSON artifacts. </summary>
    /// <param name="session"> The run artifacts session. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the conversion result. </returns>
    public async ValueTask<UnityResultsConversionResult> Convert (
        ArtifactsSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        UnityResultsXmlParseResult parseResult;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            parseResult = await xmlParser.Parse(session.Paths.ResultsXmlPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.Canceled,
                "Unity results conversion was canceled.");
        }
        catch (Exception exception) when (IsResultsXmlReadException(exception))
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.ResultsXmlReadFailed,
                $"Failed to read results.xml: {exception.Message}");
        }
        catch (Exception exception) when (IsInvalidResultsXmlException(exception))
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.InvalidResultsXml,
                $"Failed to parse results.xml: {exception.Message}");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await artifactWriter.Write(session, parseResult, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.Canceled,
                "Unity results conversion was canceled.");
        }
        catch (Exception exception) when (IsOutputWriteException(exception))
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.OutputWriteFailed,
                $"Failed to write results artifacts: {exception.Message}");
        }

        return UnityResultsConversionResult.Success(parseResult.HasFailedTests);
    }

    /// <summary> Determines whether one exception represents results XML read failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception should map to read-failure result; otherwise <see langword="false" />. </returns>
    private static bool IsResultsXmlReadException (Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    /// <summary> Determines whether one exception represents invalid input XML. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception should map to invalid-results failure; otherwise <see langword="false" />. </returns>
    private static bool IsInvalidResultsXmlException (Exception exception)
    {
        return exception is XmlException
            or InvalidDataException
            or OverflowException;
    }

    /// <summary> Determines whether one exception represents output write failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception should map to output-write failure; otherwise <see langword="false" />. </returns>
    private static bool IsOutputWriteException (Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }
}
