namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Defines machine-readable error kinds for <c>expect</c> contract reads. </summary>
internal enum ExpectationConstraintReadErrorKind
{
    /// <summary> No error. </summary>
    None = 0,

    /// <summary> The <c>expect</c> property must be an object when specified. </summary>
    ExpectationMustBeObject,

    /// <summary> The <c>expect</c> object contains one unknown property. </summary>
    ExpectationContainsUnknownProperty,

    /// <summary> The <c>expect</c> object must contain at least one constraint. </summary>
    ExpectationMustContainAtLeastOneConstraint,

    /// <summary> One boolean constraint has invalid value kind. </summary>
    BooleanConstraintMustBeBoolean,

    /// <summary> One integer constraint has invalid value kind. </summary>
    IntegerConstraintMustBeInteger,

    /// <summary> One integer constraint must be non-negative. </summary>
    IntegerConstraintMustBeNonNegative,

    /// <summary> <c>count</c> cannot be combined with <c>min</c> or <c>max</c>. </summary>
    CountCannotCombineWithMinOrMax,

    /// <summary> <c>min</c> must be less than or equal to <c>max</c>. </summary>
    MinMustBeLessThanOrEqualToMax,
}