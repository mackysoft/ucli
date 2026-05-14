namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents the result of a GUI daemon bootstrap start attempt. </summary>
    internal sealed class UnityGuiBootstrapStartResult
    {
        private UnityGuiBootstrapStartResult (
            bool isSuccess,
            bool isAlreadyRunning,
            string errorMessage)
        {
            IsSuccess = isSuccess;
            IsAlreadyRunning = isAlreadyRunning;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess { get; }

        public bool IsAlreadyRunning { get; }

        public string ErrorMessage { get; }

        public static UnityGuiBootstrapStartResult Started ()
        {
            return new UnityGuiBootstrapStartResult(true, false, null);
        }

        public static UnityGuiBootstrapStartResult AlreadyRunning ()
        {
            return new UnityGuiBootstrapStartResult(true, true, null);
        }

        public static UnityGuiBootstrapStartResult Failure (string errorMessage)
        {
            return new UnityGuiBootstrapStartResult(false, false, errorMessage);
        }
    }
}
