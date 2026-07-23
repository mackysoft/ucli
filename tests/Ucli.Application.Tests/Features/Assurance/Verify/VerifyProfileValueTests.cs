using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyProfileValueTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Definition_DoesNotExposeMutableStepStorage ()
    {
        var firstStep = CreateStep(VerifyStepKind.Compile);
        var profile = new VerifyProfileDefinition(
            VerifyProfileSource.BuiltIn,
            "default",
            RepositoryRelativePath: null,
            Steps: [firstStep]);

        var exposedSteps = Assert.IsAssignableFrom<IList<VerifyProfileStep>>(profile.Steps);

        Assert.Throws<NotSupportedException>(() => exposedSteps[0] = CreateStep(VerifyStepKind.Logs));
        Assert.Same(firstStep, profile.Steps[0]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Step_DoesNotExposeMutableEffectStorage ()
    {
        var step = VerifyProfileStep.CreateCompile(required: true);

        var exposedEffects = Assert.IsAssignableFrom<IList<AssuranceEffect>>(step.Effects);

        Assert.Throws<NotSupportedException>(() => exposedEffects[0] = AssuranceEffect.UnityTestRunner);
        Assert.Equal(AssuranceEffectSets.Compile, step.Effects);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Step_DoesNotRetainMutableTestArgumentStorage ()
    {
        var categories = new[] { "smoke" };
        var assemblies = new[] { "Game.Tests" };
        var step = VerifyProfileStep.CreateTest(
            required: true,
            testPlatform: null,
            testFilter: null,
            categories,
            assemblies);

        categories[0] = "mutated-category";
        assemblies[0] = "Mutated.Tests";

        Assert.Equal("smoke", Assert.Single(step.TestCategory!));
        Assert.Equal("Game.Tests", Assert.Single(step.AssemblyName!));
        var exposedCategories = Assert.IsAssignableFrom<IList<string>>(step.TestCategory);
        var exposedAssemblies = Assert.IsAssignableFrom<IList<string>>(step.AssemblyName);
        Assert.Throws<NotSupportedException>(() => exposedCategories[0] = "mutated-category");
        Assert.Throws<NotSupportedException>(() => exposedAssemblies[0] = "Mutated.Tests");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Definition_CanonicalizesStepsAndRejectsDuplicateKinds ()
    {
        var logs = VerifyProfileStep.CreateLogs();
        var compile = VerifyProfileStep.CreateCompile(required: true);
        var profile = new VerifyProfileDefinition(
            VerifyProfileSource.BuiltIn,
            "default",
            RepositoryRelativePath: null,
            Steps: [logs, compile]);

        Assert.Equal([VerifyStepKind.Compile, VerifyStepKind.Logs], profile.Steps.Select(static step => step.Kind));
        var exception = Assert.Throws<ArgumentException>(() => new VerifyProfileDefinition(
            VerifyProfileSource.BuiltIn,
            "duplicate",
            RepositoryRelativePath: null,
            Steps: [compile, VerifyProfileStep.CreateCompile(required: false)]));
        Assert.Equal("Steps", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Definition_WhenBuiltInSourceHasPath_RejectsInvalidState ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VerifyProfileDefinition(
            VerifyProfileSource.BuiltIn,
            "profile",
            "verify.json",
            Steps: []));

        Assert.Equal("RepositoryRelativePath", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Definition_WhenFileSourceHasNoPath_RejectsInvalidState ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VerifyProfileDefinition(
            VerifyProfileSource.File,
            "profile",
            RepositoryRelativePath: null,
            Steps: []));

        Assert.Equal("RepositoryRelativePath", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Definition_WhenFileSourcePathIsNotPortable_RejectsInvalidState ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VerifyProfileDefinition(
            VerifyProfileSource.File,
            "profile",
            @"profiles\verify.json",
            Steps: []));

        Assert.Equal("RepositoryRelativePath", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StepFactories_OnlyPopulateArgumentsOwnedByTheirKind ()
    {
        var ready = VerifyProfileStep.CreateReady(required: true, ReadyTarget.Mutation);
        var test = VerifyProfileStep.CreateTest(
            required: false,
            testPlatform: null,
            testFilter: null,
            testCategory: null,
            assemblyName: null);

        Assert.Equal(ReadyTarget.Mutation, ready.ReadyTarget);
        Assert.Null(ready.TestPlatform);
        Assert.Null(ready.TestFilter);
        Assert.Null(ready.TestCategory);
        Assert.Null(ready.AssemblyName);
        Assert.Null(test.ReadyTarget);
        Assert.Equal(AssuranceEffectSets.Test, test.Effects);
    }

    private static VerifyProfileStep CreateStep (VerifyStepKind kind)
    {
        return kind switch
        {
            VerifyStepKind.Ready => VerifyProfileStep.CreateReady(required: true, ReadyTarget.Execution),
            VerifyStepKind.Compile => VerifyProfileStep.CreateCompile(required: true),
            VerifyStepKind.PostRead => VerifyProfileStep.CreatePostRead(required: true),
            VerifyStepKind.Test => VerifyProfileStep.CreateTest(
                required: true,
                testPlatform: null,
                testFilter: null,
                testCategory: null,
                assemblyName: null),
            VerifyStepKind.Logs => VerifyProfileStep.CreateLogs(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
