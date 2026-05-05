namespace MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;

/// <summary> Parses request JSON into a static-validation request model. </summary>
internal interface IValidateRequestJsonParser
{
    /// <summary> Parses request JSON into a validation model. </summary>
    /// <param name="requestJson"> The internal execute-request JSON string. </param>
    /// <returns> The parse result. </returns>
    ValidateRequestJsonParseResult Parse (string requestJson);
}
