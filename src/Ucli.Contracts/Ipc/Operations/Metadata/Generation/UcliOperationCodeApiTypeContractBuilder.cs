using System.Reflection;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationCodeApiTypeContractBuilder
{
    public static IReadOnlyList<UcliCodeApiTypeContract> Create (IReadOnlyList<Type> apiTypes)
    {
        var contracts = new UcliCodeApiTypeContract[apiTypes.Count];
        for (var i = 0; i < apiTypes.Count; i++)
        {
            contracts[i] = CreateOne(apiTypes[i]);
        }

        return contracts;
    }

    private static UcliCodeApiTypeContract CreateOne (Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return new UcliCodeApiTypeContract(
            type.Name,
            UcliOperationCodeTypeName.Get(type),
            UcliOperationCodeDescriptionReader.Get(type),
            CreateMembers(type));
    }

    private static IReadOnlyList<UcliCodeApiMemberContract> CreateMembers (Type type)
    {
        var members = new List<UcliCodeApiMemberContract>();
        AddProperties(type, members);
        AddMethods(type, members);
        return Sort(members);
    }

    private static void AddProperties (
        Type type,
        ICollection<UcliCodeApiMemberContract> members)
    {
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (property.GetMethod != null)
            {
                members.Add(CreateProperty(property));
            }
        }
    }

    private static void AddMethods (
        Type type,
        ICollection<UcliCodeApiMemberContract> members)
    {
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (!method.IsSpecialName)
            {
                members.Add(CreateMethod(method));
            }
        }
    }

    private static UcliCodeApiMemberContract CreateProperty (PropertyInfo property)
    {
        return new UcliCodeApiMemberContract(
            UcliCodeApiMemberKindValues.Property,
            property.Name,
            UcliOperationCodeDescriptionReader.Get(property),
            UcliOperationCodeTypeName.Get(property.PropertyType),
            returnType: null,
            parameters: Array.Empty<UcliCodeApiParameterContract>());
    }

    private static UcliCodeApiMemberContract CreateMethod (MethodInfo method)
    {
        return new UcliCodeApiMemberContract(
            UcliCodeApiMemberKindValues.Method,
            method.Name,
            UcliOperationCodeDescriptionReader.Get(method),
            type: null,
            returnType: UcliOperationCodeTypeName.Get(method.ReturnType),
            parameters: UcliOperationCodeApiParameterContractBuilder.Create(method));
    }

    private static IReadOnlyList<UcliCodeApiMemberContract> Sort (IEnumerable<UcliCodeApiMemberContract> members)
    {
        return members
            .OrderBy(static member => member.Name, StringComparer.Ordinal)
            .ThenBy(static member => member.Kind, StringComparer.Ordinal)
            .ToArray();
    }
}
