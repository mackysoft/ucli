namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Creates canonical request identifiers for internally normalized request payloads. </summary>
internal interface IRequestIdFactory
{
    /// <summary> Creates one request identifier in UUID format <c>D</c>. </summary>
    /// <returns> The generated request identifier. </returns>
    string Create ();
}
