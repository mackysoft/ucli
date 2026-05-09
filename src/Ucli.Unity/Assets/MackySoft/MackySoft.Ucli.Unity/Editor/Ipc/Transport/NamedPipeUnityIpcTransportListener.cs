using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements named-pipe transport accept loop for Unity IPC server. </summary>
    internal sealed class NamedPipeUnityIpcTransportListener : IUnityIpcTransportListener
    {
        private readonly object syncRoot = new object();

        private readonly IDaemonLogger daemonLogger;

        private NamedPipeServerStream activeServerStream;

        /// <summary> Initializes a new instance of the <see cref="NamedPipeUnityIpcTransportListener" /> class. </summary>
        /// <param name="daemonLogger"> The daemon daemon-logger dependency. </param>
        public NamedPipeUnityIpcTransportListener (IDaemonLogger daemonLogger = null)
        {
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <summary> Gets transport kind handled by this listener. </summary>
        public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

        /// <summary> Runs transport-specific accept loop until cancellation is requested. </summary>
        /// <param name="address"> The pipe name value. </param>
        /// <param name="connectionHandler"> The connection handler dependency. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="address" /> is empty. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="connectionHandler" /> is <see langword="null" />. </exception>
        public async Task RunAsync (
            string address,
            IUnityIpcConnectionHandler connectionHandler,
            Action onStarted,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Pipe address must not be empty or whitespace.", nameof(address));
            }

            if (connectionHandler == null)
            {
                throw new ArgumentNullException(nameof(connectionHandler));
            }

            if (onStarted == null)
            {
                throw new ArgumentNullException(nameof(onStarted));
            }

            var started = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var serverStream = PipeServerStreamFactory.Create(address, daemonLogger);

                lock (syncRoot)
                {
                    activeServerStream = serverStream;
                }

                if (!started)
                {
                    onStarted();
                    started = true;
                }

                try
                {
                    await serverStream.WaitForConnectionAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    await connectionHandler.HandleAsync(serverStream, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IOException exception) when (cancellationToken.IsCancellationRequested)
                {
                    daemonLogger.Info(
                        DaemonLogCategories.Transport,
                        $"Named pipe listener stopped: {exception.Message}");
                    return;
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested && (exception is IOException or InvalidDataException))
                {
                    // NOTE: Probe callers may close timed-out streams while Unity is busy on the main thread.
                    // Emitting these expected connection-local failures to the Unity console can make recovery slower.
                }
                finally
                {
                    lock (syncRoot)
                    {
                        if (ReferenceEquals(activeServerStream, serverStream))
                        {
                            activeServerStream = null;
                        }
                    }
                }
            }
        }

        /// <summary> Releases active transport handles to unblock accept loops. </summary>
        public void Release ()
        {
            lock (syncRoot)
            {
                if (activeServerStream != null)
                {
                    activeServerStream.Dispose();
                    activeServerStream = null;
                }
            }
        }

        private static class PipeServerStreamFactory
        {
            public static NamedPipeServerStream Create (
                string address,
                IDaemonLogger daemonLogger)
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return new NamedPipeServerStream(
                        address,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                }

                if (!TryCreateCurrentUserOnlySecurity(out var pipeSecurity, out var failureReason))
                {
                    daemonLogger.Error(
                        DaemonLogCategories.Transport,
                        "Named pipe listener could not resolve the current Windows user SID. Refusing to start without an explicit current-user ACL.",
                        failureReason);
                    throw new InvalidOperationException(
                        $"Named pipe listener could not resolve the current Windows user SID. Refusing to start without an explicit current-user ACL. {failureReason}");
                }

                return new NamedPipeServerStream(
                    address,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    pipeSecurity);
            }

            private static bool TryCreateCurrentUserOnlySecurity (
                out PipeSecurity pipeSecurity,
                out string failureReason)
            {
                pipeSecurity = null;
                if (!TryResolveCurrentUserSid(out var currentUserSid, out failureReason))
                {
                    return false;
                }

                pipeSecurity = new PipeSecurity();
                pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    currentUserSid,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
                failureReason = string.Empty;
                return true;
            }

            private static bool TryResolveCurrentUserSid (
                out SecurityIdentifier securityIdentifier,
                out string failureReason)
            {
                securityIdentifier = null;
                var errors = new List<string>(3);

                try
                {
                    using var identity = WindowsIdentity.GetCurrent();
                    if (TryGetIdentitySecurityIdentifier(identity, static currentIdentity => currentIdentity.User, "WindowsIdentity.User", out securityIdentifier, out var error))
                    {
                        failureReason = string.Empty;
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        errors.Add(error);
                    }

                    if (TryGetIdentitySecurityIdentifier(identity, static currentIdentity => currentIdentity.Owner, "WindowsIdentity.Owner", out securityIdentifier, out error))
                    {
                        failureReason = string.Empty;
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        errors.Add(error);
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or NotImplementedException or PlatformNotSupportedException)
                {
                    errors.Add($"WindowsIdentity.GetCurrent failed: {exception.GetType().Name}: {exception.Message}");
                }

                if (TryTranslateCurrentUserAccount(out securityIdentifier, out var accountError))
                {
                    failureReason = string.Empty;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(accountError))
                {
                    errors.Add(accountError);
                }

                if (TryResolveCurrentUserSidFromAccessToken(out securityIdentifier, out var accessTokenError))
                {
                    failureReason = string.Empty;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(accessTokenError))
                {
                    errors.Add(accessTokenError);
                }

                failureReason = errors.Count == 0
                    ? "Current Windows user SID could not be resolved."
                    : string.Join(" | ", errors);
                return false;
            }

            private static bool TryGetIdentitySecurityIdentifier (
                WindowsIdentity identity,
                Func<WindowsIdentity, IdentityReference> identityReferenceSelector,
                string sourceName,
                out SecurityIdentifier securityIdentifier,
                out string failureReason)
            {
                securityIdentifier = null;
                try
                {
                    var identityReference = identityReferenceSelector(identity);
                    if (identityReference == null)
                    {
                        failureReason = $"{sourceName} returned null.";
                        return false;
                    }

                    if (identityReference is SecurityIdentifier typedSecurityIdentifier)
                    {
                        securityIdentifier = typedSecurityIdentifier;
                        failureReason = string.Empty;
                        return true;
                    }

                    securityIdentifier = (SecurityIdentifier)identityReference.Translate(typeof(SecurityIdentifier));
                    failureReason = string.Empty;
                    return true;
                }
                catch (Exception exception) when (exception is IdentityNotMappedException or InvalidOperationException or InvalidCastException or NotImplementedException or PlatformNotSupportedException)
                {
                    failureReason = $"{sourceName} failed: {exception.GetType().Name}: {exception.Message}";
                    return false;
                }
            }

            private static bool TryTranslateCurrentUserAccount (
                out SecurityIdentifier securityIdentifier,
                out string failureReason)
            {
                securityIdentifier = null;

                var userName = Environment.UserName;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    failureReason = "Environment.UserName returned an empty user name.";
                    return false;
                }

                var domainName = Environment.UserDomainName;
                var accountName = string.IsNullOrWhiteSpace(domainName)
                    ? userName
                    : string.Concat(domainName, "\\", userName);

                try
                {
                    securityIdentifier = (SecurityIdentifier)new NTAccount(accountName).Translate(typeof(SecurityIdentifier));
                    failureReason = string.Empty;
                    return true;
                }
                catch (Exception exception) when (exception is IdentityNotMappedException or InvalidCastException or InvalidOperationException or NotImplementedException or PlatformNotSupportedException)
                {
                    failureReason = $"NTAccount translation failed for '{accountName}': {exception.GetType().Name}: {exception.Message}";
                    return false;
                }
            }

            private static bool TryResolveCurrentUserSidFromAccessToken (
                out SecurityIdentifier securityIdentifier,
                out string failureReason)
            {
                securityIdentifier = null;

                IntPtr accessTokenHandle = IntPtr.Zero;
                IntPtr tokenUserBuffer = IntPtr.Zero;

                try
                {
                    if (!OpenProcessToken(GetCurrentProcess(), TokenQueryAccess, out accessTokenHandle))
                    {
                        failureReason = CreateWin32FailureReason("OpenProcessToken");
                        return false;
                    }

                    _ = GetTokenInformation(
                        accessTokenHandle,
                        TokenInformationClass.TokenUser,
                        IntPtr.Zero,
                        0,
                        out var requiredBufferLength);

                    var probeErrorCode = Marshal.GetLastWin32Error();
                    if ((requiredBufferLength <= 0) && (probeErrorCode != ErrorInsufficientBuffer))
                    {
                        failureReason = CreateWin32FailureReason("GetTokenInformation probe", probeErrorCode);
                        return false;
                    }

                    tokenUserBuffer = Marshal.AllocHGlobal(requiredBufferLength);
                    if (!GetTokenInformation(
                            accessTokenHandle,
                            TokenInformationClass.TokenUser,
                            tokenUserBuffer,
                            requiredBufferLength,
                            out _))
                    {
                        failureReason = CreateWin32FailureReason("GetTokenInformation");
                        return false;
                    }

                    var tokenUser = Marshal.PtrToStructure<TokenUser>(tokenUserBuffer);
                    if (tokenUser.UserSid == IntPtr.Zero)
                    {
                        failureReason = "GetTokenInformation(TokenUser) returned a null SID pointer.";
                        return false;
                    }

                    securityIdentifier = new SecurityIdentifier(tokenUser.UserSid);
                    failureReason = string.Empty;
                    return true;
                }
                catch (Exception exception) when (exception is InvalidOperationException or InvalidCastException or PlatformNotSupportedException)
                {
                    failureReason = $"Access token SID resolution failed: {exception.GetType().Name}: {exception.Message}";
                    return false;
                }
                finally
                {
                    if (tokenUserBuffer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(tokenUserBuffer);
                    }

                    if (accessTokenHandle != IntPtr.Zero)
                    {
                        CloseHandle(accessTokenHandle);
                    }
                }
            }

            private static string CreateWin32FailureReason (
                string operationName,
                int? errorCode = null)
            {
                var effectiveErrorCode = errorCode ?? Marshal.GetLastWin32Error();
                return $"{operationName} failed with Win32 error {effectiveErrorCode}: {new Win32Exception(effectiveErrorCode).Message}";
            }

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern bool GetTokenInformation (
                IntPtr tokenHandle,
                TokenInformationClass tokenInformationClass,
                IntPtr tokenInformation,
                int tokenInformationLength,
                out int returnLength);

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern bool OpenProcessToken (
                IntPtr processHandle,
                uint desiredAccess,
                out IntPtr tokenHandle);

            [DllImport("kernel32.dll")]
            private static extern IntPtr GetCurrentProcess ();

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle (IntPtr handle);

            private const uint TokenQueryAccess = 0x0008;
            private const int ErrorInsufficientBuffer = 122;

            private enum TokenInformationClass
            {
                TokenUser = 1,
            }

            [StructLayout(LayoutKind.Sequential)]
            private readonly struct TokenUser
            {
                public readonly IntPtr UserSid;
                public readonly uint Attributes;
            }
        }
    }
}
