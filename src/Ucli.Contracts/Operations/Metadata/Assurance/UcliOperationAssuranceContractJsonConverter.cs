using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Converts operation assurance metadata while enforcing its wire-level projections. </summary>
public sealed class UcliOperationAssuranceContractJsonConverter : JsonConverter<UcliOperationAssuranceContract>
{
    /// <inheritdoc />
    public override UcliOperationAssuranceContract Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Operation assurance metadata must be a JSON object.");
        }

        IReadOnlyList<UcliOperationSideEffect>? sideEffects = null;
        bool? mayDirty = null;
        bool? mayPersist = null;
        IReadOnlyList<UcliTouchedResourceKind>? touchedKinds = null;
        UcliOperationPlanMode? planMode = null;
        string? planSemantics = null;
        string? callSemantics = null;
        string? touchedContract = null;
        string? readPostconditionContract = null;
        string? failureSemantics = null;
        IReadOnlyList<string>? dangerousNotes = null;
        var seenFields = ContractField.None;

        foreach (var property in root.EnumerateObject())
        {
            var field = ResolveField(property.Name, options.PropertyNameCaseInsensitive);
            if (field == ContractField.None)
            {
                throw new JsonException($"Operation assurance metadata contains unknown property '{property.Name}'.");
            }
            if ((seenFields & field) != 0)
            {
                throw new JsonException($"Operation assurance metadata contains duplicate property '{property.Name}'.");
            }

            seenFields |= field;
            switch (field)
            {
                case ContractField.SideEffects:
                    sideEffects = ReadContractLiteralArray<UcliOperationSideEffect>(property.Value, property.Name);
                    break;
                case ContractField.MayDirty:
                    mayDirty = ReadBoolean(property.Value, property.Name);
                    break;
                case ContractField.MayPersist:
                    mayPersist = ReadBoolean(property.Value, property.Name);
                    break;
                case ContractField.TouchedKinds:
                    touchedKinds = ReadContractLiteralArray<UcliTouchedResourceKind>(property.Value, property.Name);
                    break;
                case ContractField.PlanMode:
                    planMode = ReadContractLiteral<UcliOperationPlanMode>(property.Value, property.Name);
                    break;
                case ContractField.PlanSemantics:
                    planSemantics = ReadString(property.Value, property.Name);
                    break;
                case ContractField.CallSemantics:
                    callSemantics = ReadString(property.Value, property.Name);
                    break;
                case ContractField.TouchedContract:
                    touchedContract = ReadString(property.Value, property.Name);
                    break;
                case ContractField.ReadPostconditionContract:
                    readPostconditionContract = ReadString(property.Value, property.Name);
                    break;
                case ContractField.FailureSemantics:
                    failureSemantics = ReadString(property.Value, property.Name);
                    break;
                case ContractField.DangerousNotes:
                    dangerousNotes = ReadStringArray(property.Value, property.Name);
                    break;
                default:
                    throw new JsonException($"Operation assurance property '{property.Name}' cannot be read.");
            }
        }

        if (seenFields != ContractField.All)
        {
            throw new JsonException("Operation assurance metadata must define every contract property.");
        }

        UcliOperationAssuranceContract assurance;
        try
        {
            assurance = new UcliOperationAssuranceContract(
                sideEffects!,
                touchedKinds!,
                planMode!.Value,
                planSemantics!,
                callSemantics!,
                touchedContract!,
                readPostconditionContract!,
                failureSemantics!,
                dangerousNotes!);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("Operation assurance metadata violates its value contract.", exception);
        }

        if (mayDirty!.Value != assurance.MayDirty)
        {
            throw new JsonException("Operation assurance property 'mayDirty' does not match the side-effect projection.");
        }
        if (mayPersist!.Value != assurance.MayPersist)
        {
            throw new JsonException("Operation assurance property 'mayPersist' does not match the side-effect projection.");
        }

        return assurance;
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        UcliOperationAssuranceContract value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("sideEffects");
        writer.WriteStartArray();
        for (var index = 0; index < value.SideEffects.Count; index++)
        {
            writer.WriteStringValue(ContractLiteralCodec.ToValue(value.SideEffects[index]));
        }
        writer.WriteEndArray();

        writer.WriteBoolean("mayDirty", value.MayDirty);
        writer.WriteBoolean("mayPersist", value.MayPersist);

        writer.WritePropertyName("touchedKinds");
        writer.WriteStartArray();
        for (var index = 0; index < value.TouchedKinds.Count; index++)
        {
            writer.WriteStringValue(ContractLiteralCodec.ToValue(value.TouchedKinds[index]));
        }
        writer.WriteEndArray();

        writer.WriteString("planMode", ContractLiteralCodec.ToValue(value.PlanMode));
        writer.WriteString("planSemantics", value.PlanSemantics);
        writer.WriteString("callSemantics", value.CallSemantics);
        writer.WriteString("touchedContract", value.TouchedContract);
        writer.WriteString("readPostconditionContract", value.ReadPostconditionContract);
        writer.WriteString("failureSemantics", value.FailureSemantics);

        writer.WritePropertyName("dangerousNotes");
        writer.WriteStartArray();
        for (var index = 0; index < value.DangerousNotes.Count; index++)
        {
            writer.WriteStringValue(value.DangerousNotes[index]);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static ContractField ResolveField (
        string propertyName,
        bool ignoreCase)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(propertyName, "sideEffects", comparison))
        {
            return ContractField.SideEffects;
        }
        if (string.Equals(propertyName, "mayDirty", comparison))
        {
            return ContractField.MayDirty;
        }
        if (string.Equals(propertyName, "mayPersist", comparison))
        {
            return ContractField.MayPersist;
        }
        if (string.Equals(propertyName, "touchedKinds", comparison))
        {
            return ContractField.TouchedKinds;
        }
        if (string.Equals(propertyName, "planMode", comparison))
        {
            return ContractField.PlanMode;
        }
        if (string.Equals(propertyName, "planSemantics", comparison))
        {
            return ContractField.PlanSemantics;
        }
        if (string.Equals(propertyName, "callSemantics", comparison))
        {
            return ContractField.CallSemantics;
        }
        if (string.Equals(propertyName, "touchedContract", comparison))
        {
            return ContractField.TouchedContract;
        }
        if (string.Equals(propertyName, "readPostconditionContract", comparison))
        {
            return ContractField.ReadPostconditionContract;
        }
        if (string.Equals(propertyName, "failureSemantics", comparison))
        {
            return ContractField.FailureSemantics;
        }
        if (string.Equals(propertyName, "dangerousNotes", comparison))
        {
            return ContractField.DangerousNotes;
        }

        return ContractField.None;
    }

    private static bool ReadBoolean (
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        if (element.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        throw new JsonException($"Operation assurance property '{propertyName}' must be a boolean.");
    }

    private static string ReadString (
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Operation assurance property '{propertyName}' must be a string.");
        }

        return element.GetString()!;
    }

    private static TEnum ReadContractLiteral<TEnum> (
        JsonElement element,
        string propertyName)
        where TEnum : struct, Enum
    {
        var literal = ReadString(element, propertyName);
        if (!ContractLiteralCodec.TryParse(literal, out TEnum value))
        {
            throw new JsonException($"Operation assurance property '{propertyName}' contains unsupported value '{literal}'.");
        }

        return value;
    }

    private static IReadOnlyList<TEnum> ReadContractLiteralArray<TEnum> (
        JsonElement element,
        string propertyName)
        where TEnum : struct, Enum
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Operation assurance property '{propertyName}' must be an array.");
        }

        var values = new TEnum[element.GetArrayLength()];
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            values[index] = ReadContractLiteral<TEnum>(item, $"{propertyName}[{index}]");
            index++;
        }

        return values;
    }

    private static IReadOnlyList<string> ReadStringArray (
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Operation assurance property '{propertyName}' must be an array.");
        }

        var values = new string[element.GetArrayLength()];
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            values[index] = ReadString(item, $"{propertyName}[{index}]");
            index++;
        }

        return values;
    }

    [Flags]
    private enum ContractField
    {
        None = 0,
        SideEffects = 1 << 0,
        MayDirty = 1 << 1,
        MayPersist = 1 << 2,
        TouchedKinds = 1 << 3,
        PlanMode = 1 << 4,
        PlanSemantics = 1 << 5,
        CallSemantics = 1 << 6,
        TouchedContract = 1 << 7,
        ReadPostconditionContract = 1 << 8,
        FailureSemantics = 1 << 9,
        DangerousNotes = 1 << 10,
        All = SideEffects
            | MayDirty
            | MayPersist
            | TouchedKinds
            | PlanMode
            | PlanSemantics
            | CallSemantics
            | TouchedContract
            | ReadPostconditionContract
            | FailureSemantics
            | DangerousNotes,
    }
}
