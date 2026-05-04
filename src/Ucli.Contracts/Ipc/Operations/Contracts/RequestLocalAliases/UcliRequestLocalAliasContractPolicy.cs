using System.Reflection;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliRequestLocalAliasContractPolicy
{
    private static readonly HashSet<Type> RequestLocalAliasReferenceContractTypes = new HashSet<Type>
    {
        typeof(AssetReferenceArgs),
        typeof(ComponentReferenceArgs),
        typeof(GameObjectReferenceArgs),
        typeof(SceneGameObjectReferenceArgs),
    };

    public static bool IsRequestLocalAliasValueType (Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType == typeof(UcliPlanAlias);
    }

    public static bool IsBuiltInReferenceContractType (Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return RequestLocalAliasReferenceContractTypes.Contains(actualType);
    }

    public static bool IsInternalRequestLocalAliasBranchProperty (PropertyInfo property)
    {
        if (!IsRequestLocalAliasValueType(property.PropertyType))
        {
            return false;
        }

        if (!IsRequestLocalAliasPropertyName(UcliOperationContractReflection.GetJsonPropertyName(property)))
        {
            return false;
        }

        return property.DeclaringType != null
            && IsBuiltInReferenceContractType(property.DeclaringType);
    }

    public static bool IsRequestLocalAliasPropertyName (string propertyName)
    {
        return string.Equals(propertyName, UcliOperationContractPropertyNames.Alias, StringComparison.Ordinal);
    }

    public static bool IsRequestLocalAliasArgsPath (string? argsPath)
    {
        if (string.IsNullOrEmpty(argsPath))
        {
            return false;
        }

        return string.Equals(argsPath, "$." + UcliOperationContractPropertyNames.Alias, StringComparison.Ordinal)
            || argsPath.EndsWith("." + UcliOperationContractPropertyNames.Alias, StringComparison.Ordinal)
            || argsPath.IndexOf("." + UcliOperationContractPropertyNames.Alias + ".", StringComparison.Ordinal) >= 0;
    }
}
