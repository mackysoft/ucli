using System;
using System.Collections.Generic;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Defines schema metadata for <c>expect</c> constraints. </summary>
internal static class ExpectationConstraintSchema
{
    public const string ExpectationPropertyName = "expect";

    public const string NonNullPropertyName = "nonNull";

    public const string CountPropertyName = "count";

    public const string MinPropertyName = "min";

    public const string MaxPropertyName = "max";

    private static readonly HashSet<string> allowedProperties = new(StringComparer.Ordinal)
    {
        NonNullPropertyName,
        CountPropertyName,
        MinPropertyName,
        MaxPropertyName,
    };

    /// <summary> Gets allowed property names for one <c>expect</c> object. </summary>
    public static ISet<string> AllowedProperties => allowedProperties;
}