namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents one normalized internal ops catalog read result. </summary>
/// <param name="Output"> The successful catalog read output; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable error code on failure; otherwise <see langword="null" />. </param>
internal sealed record OpsCatalogReadResult (
    OpsCatalogReadOutput? Output,
    string Message,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether the catalog read succeeded. </summary>
    public bool IsSuccess => Output is not null && ErrorCode is null;

    /// <summary> Creates a successful catalog-read result. </summary>
    /// <param name="output"> The successful catalog read output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static OpsCatalogReadResult Success (
        OpsCatalogReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new OpsCatalogReadResult(output, message, null);
    }

    /// <summary> Creates a failed catalog-read result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed result. </returns>
    public static OpsCatalogReadResult Failure (
        string message,
        string errorCode)
    {
        return new OpsCatalogReadResult(null, message, errorCode);
    }
}
