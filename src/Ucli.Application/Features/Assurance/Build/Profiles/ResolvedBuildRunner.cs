using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents the build runner resolved from a build profile. </summary>
internal abstract class ResolvedBuildRunner
{
    private ResolvedBuildRunner ()
    {
    }

    /// <summary> Gets the runner kind represented by this variant. </summary>
    public abstract BuildRunnerKind Kind { get; }

    /// <summary> Represents execution through Unity <c>BuildPipeline</c>. </summary>
    public sealed class BuildPipeline : ResolvedBuildRunner
    {
        /// <inheritdoc />
        public override BuildRunnerKind Kind => BuildRunnerKind.BuildPipeline;
    }

    /// <summary> Represents execution through an editor-side static method. </summary>
    public sealed class ExecuteMethod : ResolvedBuildRunner
    {
        /// <summary> Initializes an execute-method runner. </summary>
        public ExecuteMethod (
            string method,
            ResolvedBuildRunnerInvocation invocation)
        {
            if (!TryValidateMethod(method, out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(method));
            }

            Method = method;
            Invocation = invocation ?? throw new ArgumentNullException(nameof(invocation));
        }

        /// <inheritdoc />
        public override BuildRunnerKind Kind => BuildRunnerKind.ExecuteMethod;

        /// <summary> Gets the editor-side static method name. </summary>
        public string Method { get; }

        /// <summary> Gets the invocation inputs. </summary>
        public ResolvedBuildRunnerInvocation Invocation { get; }

        /// <summary> Validates an editor-side static method name. </summary>
        internal static bool TryValidateMethod (
            string? method,
            out string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(method)
                || StringValueValidator.HasOuterWhitespace(method)
                || method.IndexOf('\0') >= 0
                || method.IndexOf('\n') >= 0
                || method.IndexOf('\r') >= 0)
            {
                errorMessage = "Build profile runner.method must be a non-empty string without outer whitespace, NUL, or newline characters.";
                return false;
            }

            if (method.IndexOf(',') >= 0)
            {
                errorMessage = "Build profile runner.method must not contain an assembly-qualified type name.";
                return false;
            }

            var separatorIndex = method.LastIndexOf('.');
            if (separatorIndex <= 0 || separatorIndex == method.Length - 1)
            {
                errorMessage = "Build profile runner.method must be Namespace.Type.Method or Type.Method.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
