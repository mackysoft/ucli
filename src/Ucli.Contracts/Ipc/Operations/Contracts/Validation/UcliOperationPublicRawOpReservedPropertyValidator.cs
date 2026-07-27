using MackySoft.JsonSchema.Generation.ContractModel;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationPublicRawOpReservedPropertyValidator
{
    public static bool TryValidate (
        JsonContractModel contractModel,
        out string errorMessage)
    {
        if (!TryValidateNode(contractModel.Root, "args", out errorMessage))
        {
            return false;
        }

        for (var i = 0; i < contractModel.Definitions.Count; i++)
        {
            var definition = contractModel.Definitions[i];
            var definitionPath = string.Equals(
                contractModel.Root.ReferenceId,
                definition.Id,
                StringComparison.Ordinal)
                ? "args"
                : $"args.$defs[{definition.Id}]";
            if (!TryValidateNode(
                    definition.Value,
                    definitionPath,
                    out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateNode (
        JsonContractNode node,
        string path,
        out string errorMessage)
    {
        if (!TryValidateDiscriminator(node, path, out errorMessage)
            || !TryValidateProperties(node, path, out errorMessage)
            || !TryValidateOptionalNode(node.Items, path + "[]", out errorMessage)
            || !TryValidateOptionalNode(node.AdditionalProperties, path + ".*", out errorMessage)
            || !TryValidateVariants(node, path, out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateDiscriminator (
        JsonContractNode node,
        string path,
        out string errorMessage)
    {
        if (node.Discriminator != null)
        {
            return TryValidatePropertyName(node.Discriminator.PropertyName, path, out errorMessage);
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateProperties (
        JsonContractNode node,
        string path,
        out string errorMessage)
    {
        for (var i = 0; i < node.Properties.Count; i++)
        {
            var property = node.Properties[i];
            if (!TryValidatePropertyName(property.Name, path, out errorMessage)
                || !TryValidateNode(property.Value, $"{path}.{property.Name}", out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateOptionalNode (
        JsonContractNode? node,
        string path,
        out string errorMessage)
    {
        if (node != null)
        {
            return TryValidateNode(node, path, out errorMessage);
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateVariants (
        JsonContractNode node,
        string path,
        out string errorMessage)
    {
        for (var i = 0; i < node.Variants.Count; i++)
        {
            if (!TryValidateNode(node.Variants[i].Value, path, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidatePropertyName (
        string propertyName,
        string path,
        out string errorMessage)
    {
        if (string.Equals(
                propertyName,
                UcliOperationContractPropertyNames.Alias,
                StringComparison.Ordinal))
        {
            errorMessage =
                $"Operation contract property '{path}.{propertyName}' uses reserved public raw-op property name '{UcliOperationContractPropertyNames.Alias}'.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
