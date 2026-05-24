using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationCodeContractArguments
{
    public static void ThrowIfInvalid (
        string entryPointSignature,
        string entryPointMatchRule,
        IReadOnlyList<Type> parameterTypes,
        string returnValue,
        IReadOnlyList<UcliCodeSourceFormContract> sourceForms,
        IReadOnlyList<Type> apiTypes)
    {
        ThrowIfEmpty(entryPointSignature, nameof(entryPointSignature), "Entry point signature must not be empty.");
        ThrowIfEmpty(entryPointMatchRule, nameof(entryPointMatchRule), "Entry point match rule must not be empty.");
        ThrowIfNull(parameterTypes, nameof(parameterTypes));
        ThrowIfEmpty(returnValue, nameof(returnValue), "Return value contract must not be empty.");
        ThrowIfNull(apiTypes, nameof(apiTypes));
        ThrowIfNullOrEmpty(sourceForms, nameof(sourceForms));
    }

    private static void ThrowIfNull<T> (
        IReadOnlyList<T>? values,
        string paramName)
    {
        if (values == null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    private static void ThrowIfNullOrEmpty (
        IReadOnlyList<UcliCodeSourceFormContract>? values,
        string paramName)
    {
        if (values == null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (values.Count == 0)
        {
            throw new ArgumentException("Source forms must not be empty.", paramName);
        }
    }

    private static void ThrowIfEmpty (
        string value,
        string paramName,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, paramName);
        }
    }
}
