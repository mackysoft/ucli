using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Represents one declared-type resolution output for one SerializedProperty path. </summary>
    /// <param name="DeclaredType"> The resolved declared runtime type. </param>
    /// <param name="ElementType"> The resolved element runtime type for array-like values. </param>
    internal sealed record IndexDeclaredTypeResolution (
        Type DeclaredType,
        Type? ElementType);
}
