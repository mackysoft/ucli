namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Normalizes user-authored request JSON into the internal execute-request contract. </summary>
internal interface IUserRequestJsonNormalizer
{
    /// <summary> Normalizes one user request JSON string. </summary>
    /// <param name="requestJson"> The user-authored request JSON. </param>
    /// <returns> The normalized request JSON, or a structured validation error. </returns>
    UserRequestJsonNormalizationResult Normalize (string requestJson);
}
