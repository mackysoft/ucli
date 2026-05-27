using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Converts enum-backed contract literals without input normalization. </summary>
public static class ContractLiteralCodec
{
    /// <summary> Converts one enum value to its canonical contract literal. </summary>
    /// <typeparam name="TEnum"> The enum type. </typeparam>
    /// <param name="value"> The enum value to convert. </param>
    /// <returns> The canonical contract literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="value" /> is not mapped by the enum type. </exception>
    public static string ToValue<TEnum> (TEnum value)
        where TEnum : struct, Enum
    {
        if (TryToValue(value, out var literal))
        {
            return literal;
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, $"Unsupported {typeof(TEnum).Name} value.");
    }

    /// <summary> Tries to convert one enum value to its canonical contract literal. </summary>
    /// <typeparam name="TEnum"> The enum type. </typeparam>
    /// <param name="value"> The enum value to convert. </param>
    /// <param name="literal"> The canonical contract literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the enum value is mapped; otherwise <see langword="false" />. </returns>
    public static bool TryToValue<TEnum> (
        TEnum value,
        [NotNullWhen(true)]
        out string? literal)
        where TEnum : struct, Enum
    {
        return Cache<TEnum>.Table.TryToValue(value, out literal);
    }

    /// <summary> Tries to parse one canonical contract literal to its enum value. </summary>
    /// <typeparam name="TEnum"> The enum type. </typeparam>
    /// <param name="literal"> The canonical contract literal. </param>
    /// <param name="value"> The parsed enum value when parsing succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse<TEnum> (
        string? literal,
        out TEnum value)
        where TEnum : struct, Enum
    {
        return Cache<TEnum>.Table.TryParse(literal, out value);
    }

    /// <summary> Determines whether one canonical contract literal is defined by the enum type. </summary>
    /// <typeparam name="TEnum"> The enum type. </typeparam>
    /// <param name="literal"> The canonical contract literal. </param>
    /// <returns> <see langword="true" /> when the literal is defined; otherwise <see langword="false" />. </returns>
    public static bool IsDefined<TEnum> (string? literal)
        where TEnum : struct, Enum
    {
        return Cache<TEnum>.Table.IsDefined(literal);
    }

    /// <summary> Determines whether one enum value has a canonical contract literal. </summary>
    /// <typeparam name="TEnum"> The enum type. </typeparam>
    /// <param name="value"> The enum value. </param>
    /// <returns> <see langword="true" /> when the enum value is mapped; otherwise <see langword="false" />. </returns>
    public static bool IsDefined<TEnum> (TEnum value)
        where TEnum : struct, Enum
    {
        return Cache<TEnum>.Table.IsDefined(value);
    }

    /// <summary> Gets the canonical contract literals for one enum type in declaration order. </summary>
    /// <typeparam name="TEnum"> The enum type. </typeparam>
    /// <returns> The canonical contract literal list. </returns>
    public static IReadOnlyList<string> GetLiterals<TEnum> ()
        where TEnum : struct, Enum
    {
        return Cache<TEnum>.Table.Literals;
    }

    private static class Cache<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly Lazy<Table<TEnum>> TableSource = new(Build);

        public static Table<TEnum> Table => TableSource.Value;

        private static Table<TEnum> Build ()
        {
            var enumType = typeof(TEnum);
            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            Array.Sort(fields, static (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));

            var valueToLiteral = new Dictionary<TEnum, string>();
            var literalToValue = new Dictionary<string, TEnum>(StringComparer.Ordinal);
            var literals = new List<string>(fields.Length);

            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var attribute = field.GetCustomAttribute<UcliContractLiteralAttribute>(inherit: false);
                var ignoreAttribute = field.GetCustomAttribute<UcliContractLiteralIgnoreAttribute>(inherit: false);
                if (attribute != null && ignoreAttribute != null)
                {
                    throw new InvalidOperationException($"Enum member '{enumType.FullName}.{field.Name}' cannot define both a contract literal and a contract literal ignore marker.");
                }

                if (attribute is null)
                {
                    if (ignoreAttribute != null)
                    {
                        continue;
                    }

                    throw new InvalidOperationException($"Enum member '{enumType.FullName}.{field.Name}' is missing UcliContractLiteralAttribute.");
                }

                var literal = attribute.Literal;
                if (string.IsNullOrWhiteSpace(literal))
                {
                    throw new InvalidOperationException($"Enum member '{enumType.FullName}.{field.Name}' has an empty contract literal.");
                }

                if (StringValueValidator.HasOuterWhitespace(literal))
                {
                    throw new InvalidOperationException($"Enum member '{enumType.FullName}.{field.Name}' has a contract literal with leading or trailing whitespace.");
                }

                var value = (TEnum)field.GetValue(null)!;
                if (valueToLiteral.ContainsKey(value))
                {
                    throw new InvalidOperationException($"Enum type '{enumType.FullName}' defines duplicate enum value '{value}'.");
                }

                if (literalToValue.ContainsKey(literal))
                {
                    throw new InvalidOperationException($"Enum type '{enumType.FullName}' defines duplicate contract literal '{literal}'.");
                }

                valueToLiteral.Add(value, literal);
                literalToValue.Add(literal, value);
                literals.Add(literal);
            }

            return new Table<TEnum>(valueToLiteral, literalToValue, literals.AsReadOnly());
        }
    }

    private sealed class Table<TEnum>
        where TEnum : struct, Enum
    {
        private readonly Dictionary<string, TEnum> literalToValue;
        private readonly IReadOnlyList<string> literals;
        private readonly Dictionary<TEnum, string> valueToLiteral;

        public Table (
            Dictionary<TEnum, string> valueToLiteral,
            Dictionary<string, TEnum> literalToValue,
            IReadOnlyList<string> literals)
        {
            this.valueToLiteral = valueToLiteral;
            this.literalToValue = literalToValue;
            this.literals = literals;
        }

        public IReadOnlyList<string> Literals => literals;

        public bool TryToValue (
            TEnum value,
            [NotNullWhen(true)]
            out string? literal)
        {
            return valueToLiteral.TryGetValue(value, out literal);
        }

        public bool TryParse (
            string? literal,
            out TEnum value)
        {
            if (literal is null)
            {
                value = default;
                return false;
            }

            return literalToValue.TryGetValue(literal, out value);
        }

        public bool IsDefined (string? literal)
        {
            return literal is not null && literalToValue.ContainsKey(literal);
        }

        public bool IsDefined (TEnum value)
        {
            return valueToLiteral.ContainsKey(value);
        }
    }
}
