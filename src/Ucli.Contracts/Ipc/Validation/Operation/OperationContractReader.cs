using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Provides reusable readers for operation-contract properties. </summary>
internal static class OperationContractReader
{
    private static readonly HashSet<string> AllowedOperationProperties = new(StringComparer.Ordinal)
    {
        "id",
        "op",
        "args",
    };

    /// <summary> Finds the first unknown property in one operation object. </summary>
    /// <param name="operationElement"> The operation JSON object. </param>
    /// <returns> The unknown property name, or <see langword="null" /> when all properties are allowed. </returns>
    public static string? FindUnknownOperationProperty (JsonElement operationElement)
    {
        return JsonPropertyGuard.FindUnknownProperty(operationElement, AllowedOperationProperties);
    }

    /// <summary> Reads operation identifier based on the provided schema policy. </summary>
    /// <param name="operationElement"> The operation JSON object. </param>
    /// <param name="policy"> The schema strictness policy. </param>
    /// <param name="operationId"> The parsed operation identifier, or <see langword="null" /> when unspecified. </param>
    /// <param name="error"> The machine-readable read error on failure. </param>
    /// <returns> <see langword="true" /> when contract is satisfied; otherwise <see langword="false" />. </returns>
    public static bool TryReadOperationId (
        JsonElement operationElement,
        in RequestSchemaPolicy policy,
        out string? operationId,
        out JsonStringReadError error)
    {
        return JsonStringContractReader.TryRead(
            jsonObject: operationElement,
            propertyName: "id",
            presenceRequirement: policy.RequireOperationId
                ? JsonStringPresenceRequirement.Required
                : JsonStringPresenceRequirement.OptionalLoose,
            rejectEmptyOrWhitespace: policy.RequireNonEmptyOperationId,
            rejectOuterWhitespace: true,
            value: out operationId,
            error: out error);
    }

    /// <summary> Reads operation name based on the provided schema policy. </summary>
    /// <param name="operationElement"> The operation JSON object. </param>
    /// <param name="policy"> The schema strictness policy. </param>
    /// <param name="operationName"> The parsed operation name, or <see langword="null" /> when unspecified. </param>
    /// <param name="error"> The machine-readable read error on failure. </param>
    /// <returns> <see langword="true" /> when contract is satisfied; otherwise <see langword="false" />. </returns>
    public static bool TryReadOperationName (
        JsonElement operationElement,
        in RequestSchemaPolicy policy,
        out string? operationName,
        out JsonStringReadError error)
    {
        return JsonStringContractReader.TryRead(
            jsonObject: operationElement,
            propertyName: "op",
            presenceRequirement: policy.RequireOperationName
                ? JsonStringPresenceRequirement.Required
                : JsonStringPresenceRequirement.OptionalLoose,
            rejectEmptyOrWhitespace: policy.RequireNonEmptyOperationName,
            rejectOuterWhitespace: true,
            value: out operationName,
            error: out error);
    }

    /// <summary> Reads one required operation arguments object from operation JSON. </summary>
    /// <param name="operationElement"> The operation JSON object. </param>
    /// <param name="args"> The parsed args object. </param>
    /// <param name="errorKind"> The machine-readable read error kind on failure. </param>
    /// <returns> <see langword="true" /> when args contract is satisfied; otherwise <see langword="false" />. </returns>
    public static bool TryReadOperationArgs (
        JsonElement operationElement,
        out JsonElement args,
        out OperationObjectReadErrorKind errorKind)
    {
        args = default;
        if (!operationElement.TryGetProperty("args", out var argsElement))
        {
            errorKind = OperationObjectReadErrorKind.Missing;
            return false;
        }

        if (argsElement.ValueKind != JsonValueKind.Object)
        {
            errorKind = OperationObjectReadErrorKind.TypeMismatch;
            return false;
        }

        args = argsElement;
        errorKind = default;
        return true;
    }

}
