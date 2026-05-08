using System.Reflection;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Builds source-facing code contracts from documented public API types. </summary>
public static class UcliOperationCodeContractBuilder
{
    /// <summary> Creates one C# code contract. </summary>
    public static UcliOperationCodeContract CreateCSharp (
        string entryPointSignature,
        bool requiredStatic,
        IReadOnlyList<Type> parameterTypes,
        string returnValue,
        IReadOnlyList<Type> apiTypes)
    {
        if (string.IsNullOrWhiteSpace(entryPointSignature))
        {
            throw new ArgumentException("Entry point signature must not be empty.", nameof(entryPointSignature));
        }

        if (parameterTypes == null)
        {
            throw new ArgumentNullException(nameof(parameterTypes));
        }

        if (string.IsNullOrWhiteSpace(returnValue))
        {
            throw new ArgumentException("Return value contract must not be empty.", nameof(returnValue));
        }

        if (apiTypes == null)
        {
            throw new ArgumentNullException(nameof(apiTypes));
        }

        var parameterTypeNames = new string[parameterTypes.Count];
        for (var i = 0; i < parameterTypes.Count; i++)
        {
            parameterTypeNames[i] = GetTypeName(parameterTypes[i]);
        }

        var apiTypeContracts = new UcliCodeApiTypeContract[apiTypes.Count];
        for (var i = 0; i < apiTypes.Count; i++)
        {
            apiTypeContracts[i] = CreateApiType(apiTypes[i]);
        }

        return new UcliOperationCodeContract(
            "csharp",
            new UcliCodeEntryPointContract(
                entryPointSignature,
                requiredStatic,
                parameterTypeNames,
                returnValue),
            apiTypeContracts);
    }

    private static UcliCodeApiTypeContract CreateApiType (Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var members = CreateMembers(type);
        return new UcliCodeApiTypeContract(
            type.Name,
            GetTypeName(type),
            GetDescription(type),
            members);
    }

    private static IReadOnlyList<UcliCodeApiMemberContract> CreateMembers (Type type)
    {
        var members = new List<UcliCodeApiMemberContract>();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (property.GetMethod == null)
            {
                continue;
            }

            members.Add(new UcliCodeApiMemberContract(
                UcliCodeApiMemberKindValues.Property,
                property.Name,
                GetDescription(property),
                GetTypeName(property.PropertyType),
                returnType: null,
                parameters: Array.Empty<UcliCodeApiParameterContract>()));
        }

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName)
            {
                continue;
            }

            members.Add(new UcliCodeApiMemberContract(
                UcliCodeApiMemberKindValues.Method,
                method.Name,
                GetDescription(method),
                type: null,
                returnType: GetTypeName(method.ReturnType),
                parameters: CreateParameters(method)));
        }

        return members
            .OrderBy(static member => member.Name, StringComparer.Ordinal)
            .ThenBy(static member => member.Kind, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<UcliCodeApiParameterContract> CreateParameters (MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return Array.Empty<UcliCodeApiParameterContract>();
        }

        var contracts = new UcliCodeApiParameterContract[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            contracts[i] = new UcliCodeApiParameterContract(
                parameters[i].Name,
                GetTypeName(parameters[i].ParameterType),
                GetDescription(parameters[i]));
        }

        return contracts;
    }

    private static string GetDescription (MemberInfo member)
    {
        return member.GetCustomAttribute<UcliDescriptionAttribute>()?.Description
            ?? throw new InvalidOperationException($"Code contract member '{member.Name}' must declare {nameof(UcliDescriptionAttribute)}.");
    }

    private static string GetDescription (ParameterInfo parameter)
    {
        return parameter.GetCustomAttribute<UcliDescriptionAttribute>()?.Description
            ?? throw new InvalidOperationException($"Code contract parameter '{parameter.Name}' must declare {nameof(UcliDescriptionAttribute)}.");
    }

    private static string GetTypeName (Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return type == typeof(void) ? "void" : (type.FullName ?? type.Name);
    }
}
