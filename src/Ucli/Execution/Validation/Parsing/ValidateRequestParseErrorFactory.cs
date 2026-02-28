using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Execution;

/// <summary> Creates parse errors for static request preflight validation. </summary>
internal static class ValidateRequestParseErrorFactory
{
    public static ExecutionError OperationMustBeObject (int operationIndex)
    {
        return ExecutionError.InvalidArgument($"Operation at index {operationIndex} must be an object.");
    }

    public static ExecutionError UnknownOperationProperty (int operationIndex, string unknownPropertyName)
    {
        return ExecutionError.InvalidArgument(
            $"Operation at index {operationIndex} contains an unknown property: {unknownPropertyName}.");
    }

    public static ExecutionError OperationArgs (int operationIndex, OperationObjectReadErrorKind readError)
    {
        return readError switch
        {
            OperationObjectReadErrorKind.Missing => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'args' is required."),
            OperationObjectReadErrorKind.TypeMismatch => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'args' must be an object."),
            _ => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'args' is invalid."),
        };
    }

    public static ExecutionError OperationStringProperty (
        int operationIndex,
        string propertyName,
        JsonStringReadError readError)
    {
        return readError.Kind switch
        {
            JsonStringReadErrorKind.OuterWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{propertyName}' must not contain leading or trailing whitespace."),
            JsonStringReadErrorKind.TypeMismatch => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{propertyName}' must be a string when specified."),
            JsonStringReadErrorKind.EmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{propertyName}' must not be empty."),
            JsonStringReadErrorKind.Missing => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{propertyName}' is required."),
            _ => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{propertyName}' is invalid."),
        };
    }

    public static ExecutionError OperationAlias (int operationIndex, JsonStringReadError readError)
    {
        return readError.Kind switch
        {
            JsonStringReadErrorKind.TypeMismatch => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'as' must be a string when specified."),
            JsonStringReadErrorKind.EmptyOrWhitespace or JsonStringReadErrorKind.OuterWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'as' must not be empty or contain outer whitespace."),
            _ => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'as' is invalid."),
        };
    }

    public static ExecutionError OperationExpectation (
        int operationIndex,
        ExpectationConstraintReadError expectationError)
    {
        return expectationError.Kind switch
        {
            ExpectationConstraintReadErrorKind.ExpectationMustBeObject => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' must be an object when specified."),
            ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' contains an unknown property: {expectationError.UnknownPropertyName}."),
            ExpectationConstraintReadErrorKind.ExpectationMustContainAtLeastOneConstraint => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' must contain at least one constraint."),
            ExpectationConstraintReadErrorKind.BooleanConstraintMustBeBoolean => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{expectationError.PropertyPath}' must be a boolean."),
            ExpectationConstraintReadErrorKind.IntegerConstraintMustBeInteger => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{expectationError.PropertyPath}' must be an integer."),
            ExpectationConstraintReadErrorKind.IntegerConstraintMustBeNonNegative => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{expectationError.PropertyPath}' must be greater than or equal to 0."),
            ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' cannot combine 'count' with 'min' or 'max'."),
            ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' requires 'min' to be less than or equal to 'max'."),
            _ => ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' is invalid."),
        };
    }
}