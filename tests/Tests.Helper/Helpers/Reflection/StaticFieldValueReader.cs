namespace MackySoft.Tests;

using System.Reflection;

internal static class StaticFieldValueReader
{
    public static IReadOnlySet<TValue> ReadFromStaticClasses<TValue> (
        Assembly assembly,
        string typeNameSuffix)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (string.IsNullOrWhiteSpace(typeNameSuffix))
        {
            throw new ArgumentException("Type name suffix must not be empty.", nameof(typeNameSuffix));
        }

        var values = new HashSet<TValue>();
        var types = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: true, IsSealed: true }
                && type.Name.EndsWith(typeNameSuffix, StringComparison.Ordinal));

        foreach (var type in types)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(TValue))
                {
                    values.Add((TValue)field.GetValue(null)!);
                }
            }
        }

        return values;
    }
}
