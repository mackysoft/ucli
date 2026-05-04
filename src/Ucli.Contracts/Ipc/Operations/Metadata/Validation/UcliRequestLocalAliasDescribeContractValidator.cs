namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliRequestLocalAliasDescribeContractValidator
{
    public static bool TryValidatePublicRawOpInputs (
        IReadOnlyList<UcliOperationInputContract>? inputs,
        out string errorMessage)
    {
        if (inputs == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
        {
            var input = inputs[inputIndex];
            if (input == null)
            {
                errorMessage = $"Describe contract input at index {inputIndex} must not be null.";
                return false;
            }

            if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasArgsPath(input.ArgsPath))
            {
                errorMessage = $"Describe contract input '{input.Name}' must not expose request-local alias args path '{input.ArgsPath}'.";
                return false;
            }

            if (!TryValidatePublicRawOpInputVariants(input, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidatePublicRawOpInputVariants (
        UcliOperationInputContract input,
        out string errorMessage)
    {
        var variants = input.Variants;
        if (variants == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        for (var variantIndex = 0; variantIndex < variants.Count; variantIndex++)
        {
            var variant = variants[variantIndex];
            if (variant == null)
            {
                errorMessage = $"Describe contract input '{input.Name}' variant at index {variantIndex} must not be null.";
                return false;
            }

            if (!TryValidatePublicRawOpVariantArgsPaths(input, variant, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidatePublicRawOpVariantArgsPaths (
        UcliOperationInputContract input,
        UcliOperationInputVariantContract variant,
        out string errorMessage)
    {
        var argsPaths = variant.ArgsPaths;
        if (argsPaths == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        for (var pathIndex = 0; pathIndex < argsPaths.Count; pathIndex++)
        {
            var argsPath = argsPaths[pathIndex];
            if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasArgsPath(argsPath))
            {
                errorMessage = $"Describe contract input '{input.Name}' variant '{variant.Name}' must not expose request-local alias args path '{argsPath}'.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }
}
