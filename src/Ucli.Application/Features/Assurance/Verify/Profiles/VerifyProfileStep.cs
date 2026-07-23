using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Represents one canonical verify profile step. </summary>
internal sealed class VerifyProfileStep
{
    private VerifyProfileStep (
        VerifyStepKind kind,
        bool required,
        ReadyTarget? readyTarget,
        TestRunPlatform? testPlatform,
        string? testFilter,
        IReadOnlyList<string>? testCategory,
        IReadOnlyList<string>? assemblyName)
    {
        Kind = kind;
        Required = required;
        Effects = kind switch
        {
            VerifyStepKind.Compile => AssuranceEffectSets.Compile,
            VerifyStepKind.Test => AssuranceEffectSets.Test,
            _ => AssuranceEffectSets.None,
        };
        ReadyTarget = readyTarget;
        TestPlatform = testPlatform;
        TestFilter = testFilter;
        TestCategory = CopyOptionalValues(testCategory, nameof(testCategory));
        AssemblyName = CopyOptionalValues(assemblyName, nameof(assemblyName));
    }

    public VerifyStepKind Kind { get; }

    public bool Required { get; }

    public IReadOnlyList<AssuranceEffect> Effects { get; }

    /// <summary> Gets the ready target for a ready verifier step. </summary>
    public ReadyTarget? ReadyTarget { get; }

    /// <summary> Gets the optional test platform for a test verifier step. </summary>
    public TestRunPlatform? TestPlatform { get; }

    /// <summary> Gets the optional test filter for a test verifier step. </summary>
    public string? TestFilter { get; }

    /// <summary> Gets the optional test categories for a test verifier step. </summary>
    public IReadOnlyList<string>? TestCategory { get; }

    /// <summary> Gets the optional assembly names for a test verifier step. </summary>
    public IReadOnlyList<string>? AssemblyName { get; }

    public static VerifyProfileStep CreateReady (
        bool required,
        ReadyTarget readyTarget)
    {
        if (!TextVocabulary.IsDefined(readyTarget))
        {
            throw new ArgumentOutOfRangeException(nameof(readyTarget), readyTarget, "Ready target must be defined.");
        }

        return new VerifyProfileStep(
            VerifyStepKind.Ready,
            required,
            readyTarget,
            testPlatform: null,
            testFilter: null,
            testCategory: null,
            assemblyName: null);
    }

    public static VerifyProfileStep CreateCompile (bool required)
    {
        return CreateWithoutArguments(VerifyStepKind.Compile, required);
    }

    public static VerifyProfileStep CreatePostRead (bool required)
    {
        return CreateWithoutArguments(VerifyStepKind.PostRead, required);
    }

    public static VerifyProfileStep CreateTest (
        bool required,
        TestRunPlatform? testPlatform,
        string? testFilter,
        IReadOnlyList<string>? testCategory,
        IReadOnlyList<string>? assemblyName)
    {
        if (testFilter is not null && string.IsNullOrWhiteSpace(testFilter))
        {
            throw new ArgumentException("Test filter must not be empty or whitespace.", nameof(testFilter));
        }

        return new VerifyProfileStep(
            VerifyStepKind.Test,
            required,
            readyTarget: null,
            testPlatform,
            testFilter,
            testCategory,
            assemblyName);
    }

    public static VerifyProfileStep CreateLogs ()
    {
        return CreateWithoutArguments(VerifyStepKind.Logs, required: false);
    }

    private static VerifyProfileStep CreateWithoutArguments (
        VerifyStepKind kind,
        bool required)
    {
        return new VerifyProfileStep(
            kind,
            required,
            readyTarget: null,
            testPlatform: null,
            testFilter: null,
            testCategory: null,
            assemblyName: null);
    }

    private static IReadOnlyList<string>? CopyOptionalValues (
        IReadOnlyList<string>? values,
        string parameterName)
    {
        if (values is null)
        {
            return null;
        }

        if (values.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                throw new ArgumentException($"Value at index {i} must not be empty or whitespace.", parameterName);
            }

            copy[i] = values[i];
        }

        return Array.AsReadOnly(copy);
    }
}
