using System;
using System.Reflection;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Clears the Unity Editor Console display through UnityEditor internal Console APIs. </summary>
    internal sealed class UnityEditorConsoleClearer : IUnityConsoleClearer
    {
        private static readonly ClearMethodResolution DefaultClearMethodResolution = ResolveClearMethod();

        private readonly ClearMethodResolution clearMethodResolution;

        /// <summary> Initializes a new instance of the <see cref="UnityEditorConsoleClearer" /> class. </summary>
        public UnityEditorConsoleClearer () : this(DefaultClearMethodResolution)
        {
        }

        internal UnityEditorConsoleClearer (
            MethodInfo clearMethod,
            string resolutionErrorMessage) : this(clearMethod == null
                ? ClearMethodResolution.Failure(resolutionErrorMessage)
                : ClearMethodResolution.Success(clearMethod))
        {
        }

        private UnityEditorConsoleClearer (ClearMethodResolution clearMethodResolution)
        {
            this.clearMethodResolution = clearMethodResolution ?? throw new ArgumentNullException(nameof(clearMethodResolution));
        }

        /// <inheritdoc />
        public UnityConsoleClearResult Clear ()
        {
            if (clearMethodResolution.ClearMethod == null)
            {
                return UnityConsoleClearResult.Failure(clearMethodResolution.ErrorMessage);
            }

            try
            {
                clearMethodResolution.ClearMethod.Invoke(null, null);
                return UnityConsoleClearResult.Success();
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                return UnityConsoleClearResult.Failure($"UnityEditor.LogEntries.Clear failed. {exception.InnerException.Message}");
            }
            catch (Exception exception)
            {
                return UnityConsoleClearResult.Failure($"UnityEditor.LogEntries.Clear failed. {exception.Message}");
            }
        }

        private static ClearMethodResolution ResolveClearMethod ()
        {
            try
            {
                var logEntriesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries")
                    ?? Type.GetType("UnityEditor.LogEntries,UnityEditor");
                if (logEntriesType == null)
                {
                    return ClearMethodResolution.Failure("UnityEditor.LogEntries.Clear could not be resolved.");
                }

                var clearMethod = logEntriesType.GetMethod(
                    "Clear",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                return clearMethod == null
                    ? ClearMethodResolution.Failure("UnityEditor.LogEntries.Clear could not be resolved.")
                    : ClearMethodResolution.Success(clearMethod);
            }
            catch (Exception exception)
            {
                return ClearMethodResolution.Failure($"UnityEditor.LogEntries.Clear could not be resolved. {exception.Message}");
            }
        }

        private sealed class ClearMethodResolution
        {
            private ClearMethodResolution (
                MethodInfo clearMethod,
                string errorMessage)
            {
                ClearMethod = clearMethod;
                ErrorMessage = errorMessage;
            }

            public MethodInfo ClearMethod { get; }

            public string ErrorMessage { get; }

            public static ClearMethodResolution Success (MethodInfo clearMethod)
            {
                return new ClearMethodResolution(clearMethod ?? throw new ArgumentNullException(nameof(clearMethod)), string.Empty);
            }

            public static ClearMethodResolution Failure (string errorMessage)
            {
                return new ClearMethodResolution(null, string.IsNullOrWhiteSpace(errorMessage) ? "UnityEditor.LogEntries.Clear could not be resolved." : errorMessage);
            }
        }
    }
}
