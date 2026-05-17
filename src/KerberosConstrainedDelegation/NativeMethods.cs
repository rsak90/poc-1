using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace KerberosConstrainedDelegation;

/// <summary>
/// P/Invoke declarations for Windows Security APIs
/// </summary>
internal static class NativeMethods
{
    #region Constants

    // Logon Types
    public const int LOGON32_LOGON_INTERACTIVE = 2;
    public const int LOGON32_LOGON_NETWORK = 3;
    public const int LOGON32_LOGON_BATCH = 4;
    public const int LOGON32_LOGON_SERVICE = 5;
    public const int LOGON32_LOGON_UNLOCK = 7;
    public const int LOGON32_LOGON_NETWORK_CLEARTEXT = 8;
    public const int LOGON32_LOGON_NEW_CREDENTIALS = 9;

    // Security Logon Types (for LsaLogonUser)
    public const int Interactive = 2;
    public const int Network = 3;
    public const int Batch = 4;
    public const int Service = 5;
    public const int Proxy = 6;
    public const int Unlock = 7;
    public const int NetworkCleartext = 8;
    public const int NewCredentials = 9;
    public const int RemoteInteractive = 10;
    public const int CachedInteractive = 11;
    public const int CachedRemoteInteractive = 12;
    public const int CachedUnlock = 13;

    // Logon Providers
    public const int LOGON32_PROVIDER_DEFAULT = 0;
    public const int LOGON32_PROVIDER_WINNT50 = 3;
    public const int LOGON32_PROVIDER_WINNT40 = 2;

    // Process Creation Flags
    public const uint CREATE_NO_WINDOW = 0x08000000;
    public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    public const uint CREATE_NEW_CONSOLE = 0x00000010;
    public const uint CREATE_SUSPENDED = 0x00000004;
    public const uint DETACHED_PROCESS = 0x00000008;

    // STARTUPINFO Flags
    public const int STARTF_USESHOWWINDOW = 0x00000001;
    public const int STARTF_USESTDHANDLES = 0x00000100;

    // ShowWindow Constants
    public const short SW_HIDE = 0;
    public const short SW_SHOW = 5;

    // Wait Constants
    public const uint WAIT_OBJECT_0 = 0x00000000;
    public const uint WAIT_TIMEOUT = 0x00000102;
    public const uint WAIT_FAILED = 0xFFFFFFFF;
    public const uint INFINITE = 0xFFFFFFFF;

    // Standard Handles
    public const int STD_INPUT_HANDLE = -10;
    public const int STD_OUTPUT_HANDLE = -11;
    public const int STD_ERROR_HANDLE = -12;

    // NTSTATUS Codes
    public const int STATUS_SUCCESS = 0x00000000;
    public const int STATUS_LOGON_FAILURE = unchecked((int)0xC000006D);
    public const int STATUS_NO_SUCH_USER = unchecked((int)0xC0000064);
    public const int STATUS_WRONG_PASSWORD = unchecked((int)0xC000006A);
    public const int STATUS_PASSWORD_EXPIRED = unchecked((int)0xC0000071);
    public const int STATUS_ACCOUNT_DISABLED = unchecked((int)0xC0000072);
    public const int STATUS_ACCOUNT_RESTRICTION = unchecked((int)0xC000006E);
    public const int STATUS_INVALID_LOGON_HOURS = unchecked((int)0xC000006F);
    public const int STATUS_INVALID_WORKSTATION = unchecked((int)0xC0000070);
    public const int STATUS_PASSWORD_MUST_CHANGE = unchecked((int)0xC0000224);
    public const int STATUS_ACCOUNT_LOCKED_OUT = unchecked((int)0xC0000234);
    public const int STATUS_PRIVILEGE_NOT_HELD = unchecked((int)0xC0000061);

    // Win32 Error Codes
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_LOGON_FAILURE = 1326;
    public const int ERROR_NO_SUCH_USER = 1317;
    public const int ERROR_ACCESS_DENIED = 5;
    public const int ERROR_NOT_SUPPORTED = 50;
    public const int ERROR_INVALID_PARAMETER = 87;
    public const int ERROR_INSUFFICIENT_BUFFER = 122;

    // S4U Logon Message Types
    public const int MsV1_0S4ULogon = 12;
    public const int KerbS4ULogon = 12;

    // S4U Logon Flags
    public const uint S4U_LOGON_FLAG_IDENTITY = 0x00000008;

    // Authentication Package Names
    public const string MSV1_0_PACKAGE_NAME = "MICROSOFT_AUTHENTICATION_PACKAGE_V1_0";
    public const string NEGOSSP_NAME = "Negotiate";
    public const string MICROSOFT_KERBEROS_NAME = "Kerberos";

    // Token Information Classes
    public const int TokenUser = 1;
    public const int TokenGroups = 2;
    public const int TokenPrivileges = 3;
    public const int TokenOwner = 4;
    public const int TokenPrimaryGroup = 5;
    public const int TokenDefaultDacl = 6;
    public const int TokenSource = 7;
    public const int TokenType = 8;
    public const int TokenImpersonationLevel = 9;
    public const int TokenStatistics = 10;
    public const int TokenRestrictedSids = 11;
    public const int TokenSessionId = 12;
    public const int TokenGroupsAndPrivileges = 13;
    public const int TokenSessionReference = 14;
    public const int TokenSandBoxInert = 15;
    public const int TokenElevationType = 18;
    public const int TokenLinkedToken = 19;
    public const int TokenElevation = 20;

    // Security Attributes
    public const int SECURITY_SQOS_PRESENT = 0x00100000;
    public const int SECURITY_ANONYMOUS = 0;
    public const int SECURITY_IDENTIFICATION = 1;
    public const int SECURITY_IMPERSONATION = 2;
    public const int SECURITY_DELEGATION = 3;

    // Privilege Constants
    public const string SE_TCB_NAME = "SeTcbPrivilege";
    public const int SE_PRIVILEGE_ENABLED = 0x00000002;

    #endregion

    #region Structures

    /// <summary>
    /// Privilege attributes
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    /// <summary>
    /// Token privileges structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
    }

    /// <summary>
    /// Contains information about a newly created process and its primary thread
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    /// <summary>
    /// Specifies the window station, desktop, standard handles, and appearance of the main window for a process at creation time
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    /// <summary>
    /// Security attributes structure for handle inheritance
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    /// <summary>
    /// Locally unique identifier (LUID)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    /// <summary>
    /// Quota limits for a user
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct QUOTA_LIMITS
    {
        public IntPtr PagedPoolLimit;
        public IntPtr NonPagedPoolLimit;
        public IntPtr MinimumWorkingSetSize;
        public IntPtr MaximumWorkingSetSize;
        public IntPtr PagefileLimit;
        public IntPtr TimeLimit;
    }

    /// <summary>
    /// S4U logon structure for Service for User authentication (MSV1_0)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct S4U_LOGON
    {
        public int MessageType;
        public uint Flags;
        public UNICODE_STRING UserPrincipalName;
        public UNICODE_STRING DomainName;
    }

    /// <summary>
    /// Kerberos S4U logon structure for Service for User authentication
    /// Used with Kerberos authentication package for S4U2Self and S4U2Proxy operations
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct KERB_S4U_LOGON
    {
        public int MessageType;        // Must be KerbS4ULogon (12)
        public uint Flags;              // 0 for S4U2Self, S4U_LOGON_FLAG_IDENTITY for S4U2Proxy
        public UNICODE_STRING ClientUpn;      // User Principal Name (e.g., user@domain.com)
        public UNICODE_STRING ClientRealm;    // Domain name (e.g., DOMAIN.COM)
    }

    /// <summary>
    /// Unicode string structure used by LSA APIs
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    /// <summary>
    /// LSA string structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LSA_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    /// <summary>
    /// Kerberos S4U logon additional information
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct KERB_S4U_LOGON_ADDITIONAL_INFO
    {
        public UNICODE_STRING TargetServerName;
    }

    /// <summary>
    /// Token user information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_USER
    {
        public SID_AND_ATTRIBUTES User;
    }

    /// <summary>
    /// SID and its attributes
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    /// <summary>
    /// Token statistics information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_STATISTICS
    {
        public LUID TokenId;
        public LUID AuthenticationId;
        public long ExpirationTime;
        public int TokenType;
        public int ImpersonationLevel;
        public uint DynamicCharged;
        public uint DynamicAvailable;
        public uint GroupCount;
        public uint PrivilegeCount;
        public LUID ModifiedId;
    }

    #endregion

    #region advapi32.dll - Authentication and Process APIs

    /// <summary>
    /// Attempts to log a user on to the local computer
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool LogonUser(
        string lpszUsername,
        string? lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out SafeAccessTokenHandle phToken);

    /// <summary>
    /// Authenticates a security principal's logon data
    /// </summary>
    [DllImport("secur32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int LsaLogonUser(
        IntPtr LsaHandle,
        ref LSA_STRING OriginName,
        int LogonType,
        uint AuthenticationPackage,
        IntPtr AuthenticationInformation,
        uint AuthenticationInformationLength,
        IntPtr LocalGroups,
        ref TOKEN_SOURCE SourceContext,
        out IntPtr ProfileBuffer,
        out uint ProfileBufferLength,
        out LUID LogonId,
        out SafeAccessTokenHandle Token,
        out QUOTA_LIMITS Quotas,
        out int SubStatus);

    /// <summary>
    /// Registers a logon process with the LSA (requires SeTcbPrivilege)
    /// </summary>
    [DllImport("secur32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int LsaRegisterLogonProcess(
        ref LSA_STRING LogonProcessName,
        out IntPtr LsaHandle,
        out ulong SecurityMode);

    /// <summary>
    /// Establishes an untrusted connection to the LSA (does not require special privileges)
    /// </summary>
    [DllImport("secur32.dll", SetLastError = true)]
    public static extern int LsaConnectUntrusted(
        out IntPtr LsaHandle);

    /// <summary>
    /// Looks up an authentication package
    /// </summary>
    [DllImport("secur32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int LsaLookupAuthenticationPackage(
        IntPtr LsaHandle,
        ref LSA_STRING PackageName,
        out uint AuthenticationPackage);

    /// <summary>
    /// Frees memory allocated by LSA
    /// </summary>
    [DllImport("secur32.dll")]
    public static extern int LsaFreeReturnBuffer(IntPtr Buffer);

    /// <summary>
    /// Closes an LSA handle
    /// </summary>
    [DllImport("secur32.dll")]
    public static extern int LsaDeregisterLogonProcess(IntPtr LsaHandle);

    /// <summary>
    /// Creates a new process and its primary thread running in the security context of the specified token.
    /// Requires SE_ASSIGNPRIMARYTOKEN_NAME or SE_INCREASE_QUOTA_NAME privilege.
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessAsUser(
        SafeAccessTokenHandle hToken,
        string? lpApplicationName,
        string? lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    /// <summary>
    /// Creates a process under a token. Accepts Identification-level tokens (S4U2Self).
    /// Requires SE_IMPERSONATE_NAME privilege (held by services by default).
    /// Does NOT take bInheritHandles — pipe handles must be set in STARTUPINFO.
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessWithTokenW(
        SafeAccessTokenHandle hToken,
        uint dwLogonFlags,
        string? lpApplicationName,
        string? lpCommandLine,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    /// <summary>
    /// Impersonates a logged-on user on the current thread
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool ImpersonateLoggedOnUser(SafeAccessTokenHandle hToken);

    /// <summary>
    /// Reverts the current thread to its primary token
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool RevertToSelf();

    /// <summary>
    /// Opens the access token associated with the current thread (impersonation token)
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenThreadToken(
        IntPtr ThreadHandle,
        uint DesiredAccess,
        bool OpenAsSelf,
        out SafeAccessTokenHandle TokenHandle);

    /// <summary>
    /// Returns a pseudo-handle for the current thread
    /// </summary>
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentThread();

    /// <summary>
    /// Retrieves information about the specified access token
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetTokenInformation(
        SafeAccessTokenHandle TokenHandle,
        int TokenInformationClass,
        IntPtr TokenInformation,
        int TokenInformationLength,
        out int ReturnLength);

    /// <summary>
    /// Opens the access token associated with a process
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(
        IntPtr ProcessHandle,
        uint DesiredAccess,
        out SafeAccessTokenHandle TokenHandle);

    /// <summary>
    /// Looks up the LUID for a privilege name
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool LookupPrivilegeValue(
        string? lpSystemName,
        string lpName,
        out LUID lpLuid);

    /// <summary>
    /// Enables or disables privileges in an access token
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool AdjustTokenPrivileges(
        SafeAccessTokenHandle TokenHandle,
        bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    /// <summary>
    /// Token source structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_SOURCE
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] SourceName;
        public LUID SourceIdentifier;
    }

    #endregion

    #region kernel32.dll - Process and Handle Management

    /// <summary>
    /// Creates an anonymous pipe
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreatePipe(
        out SafeFileHandle hReadPipe,
        out SafeFileHandle hWritePipe,
        ref SECURITY_ATTRIBUTES lpPipeAttributes,
        uint nSize);

    /// <summary>
    /// Waits until the specified object is in the signaled state or the time-out interval elapses
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(
        IntPtr hHandle,
        uint dwMilliseconds);

    /// <summary>
    /// Retrieves the termination status of the specified process
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeProcess(
        IntPtr hProcess,
        out int lpExitCode);

    /// <summary>
    /// Closes an open object handle
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Terminates the specified process and all of its threads
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(
        IntPtr hProcess,
        uint uExitCode);

    /// <summary>
    /// Retrieves a handle to the specified standard device
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);

    /// <summary>
    /// Gets a pseudo handle to the current process
    /// </summary>
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    /// <summary>
    /// Reads data from the specified file or input/output (I/O) device
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    /// <summary>
    /// Determines whether there is data in the pipe
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool PeekNamedPipe(
        SafeFileHandle hNamedPipe,
        IntPtr lpBuffer,
        uint nBufferSize,
        IntPtr lpBytesRead,
        out uint lpTotalBytesAvail,
        IntPtr lpBytesLeftThisMessage);

    #endregion

    #region secur32.dll - Security Support Provider Interface

    /// <summary>
    /// Converts an NTSTATUS code to a Windows error code
    /// </summary>
    [DllImport("advapi32.dll")]
    public static extern int LsaNtStatusToWinError(int Status);

    // SSPI constants
    public const int  SECPKG_CRED_OUTBOUND      = 2;
    public const int  SECURITY_NATIVE_DREP       = 0x10;
    public const int  SECBUFFER_TOKEN            = 2;
    public const uint ISC_REQ_MUTUAL_AUTH        = 0x00000002;
    public const uint ISC_REQ_DELEGATE           = 0x00000001;
    public const uint ISC_REQ_ALLOCATE_MEMORY    = 0x00000100;
    public const int  SEC_E_OK                   = 0;
    public const int  SEC_I_CONTINUE_NEEDED      = 0x00090312;
    public const int  TOKEN_ALL_ACCESS            = 0x000F01FF;
    public const int  TokenPrimary               = 1;
    public const int  TokenImpersonation         = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct SecHandle
    {
        public IntPtr dwLower;
        public IntPtr dwUpper;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_INTEGER
    {
        public uint LowPart;
        public int  HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SecBuffer
    {
        public int    cbBuffer;
        public int    BufferType;
        public IntPtr pvBuffer;
    }

    /// <summary>
    /// Wrapper around SecBufferDesc that manages a single token buffer
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SecBufferDesc
    {
        public int    ulVersion;
        public int    cBuffers;
        public IntPtr pBuffers;   // pointer to SecBuffer array (unmanaged)

        public void Init(int bufferType, int size)
        {
            ulVersion = 0;
            cBuffers  = 1;
            // Allocate unmanaged SecBuffer + data
            int secBufSize = Marshal.SizeOf<SecBuffer>();
            pBuffers = Marshal.AllocHGlobal(secBufSize);
            var buf = new SecBuffer
            {
                cbBuffer   = size,
                BufferType = bufferType,
                pvBuffer   = Marshal.AllocHGlobal(size)
            };
            Marshal.StructureToPtr(buf, pBuffers, false);
        }

        public void Free()
        {
            if (pBuffers != IntPtr.Zero)
            {
                var buf = Marshal.PtrToStructure<SecBuffer>(pBuffers);
                if (buf.pvBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(buf.pvBuffer);
                Marshal.FreeHGlobal(pBuffers);
                pBuffers = IntPtr.Zero;
            }
        }
    }

    [DllImport("secur32.dll", CharSet = CharSet.Unicode)]
    public static extern int AcquireCredentialsHandle(
        string?                  pszPrincipal,
        string                   pszPackage,
        int                      fCredentialUse,
        IntPtr                   pvLogonId,
        IntPtr                   pAuthData,
        IntPtr                   pGetKeyFn,
        IntPtr                   pvGetKeyArgument,
        ref SecHandle            phCredential,
        ref SECURITY_INTEGER     ptsExpiry);

    [DllImport("secur32.dll", CharSet = CharSet.Unicode)]
    public static extern int InitializeSecurityContext(
        ref SecHandle            phCredential,
        IntPtr                   phContext,
        string                   pszTargetName,
        uint                     fContextReq,
        int                      Reserved1,
        int                      TargetDataRep,
        IntPtr                   pInput,
        int                      Reserved2,
        ref SecHandle            phNewContext,
        ref SecBufferDesc        pOutput,
        ref ulong                pfContextAttr,
        ref SECURITY_INTEGER     ptsExpiry);

    [DllImport("secur32.dll")]
    public static extern int QuerySecurityContextToken(
        ref SecHandle            phContext,
        out SafeAccessTokenHandle Token);

    [DllImport("secur32.dll")]
    public static extern int DeleteSecurityContext(ref SecHandle phContext);

    [DllImport("secur32.dll")]
    public static extern int FreeCredentialsHandle(ref SecHandle phCredential);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool DuplicateTokenEx(
        SafeAccessTokenHandle    hExistingToken,
        int                      dwDesiredAccess,
        IntPtr                   lpTokenAttributes,
        int                      ImpersonationLevel,
        int                      TokenType,
        out SafeAccessTokenHandle phNewToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DuplicateHandle(
        IntPtr hSourceProcessHandle,
        IntPtr hSourceHandle,
        IntPtr hTargetProcessHandle,
        out IntPtr lpTargetHandle,
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwOptions);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a UNICODE_STRING from a managed string
    /// </summary>
    public static UNICODE_STRING CreateUnicodeString(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return new UNICODE_STRING
            {
                Length = 0,
                MaximumLength = 0,
                Buffer = IntPtr.Zero
            };
        }

        var bytes = System.Text.Encoding.Unicode.GetByteCount(str);
        return new UNICODE_STRING
        {
            Length = (ushort)bytes,
            MaximumLength = (ushort)(bytes + 2), // +2 for null terminator
            Buffer = Marshal.StringToHGlobalUni(str)
        };
    }

    /// <summary>
    /// Creates an LSA_STRING from a managed string
    /// </summary>
    public static LSA_STRING CreateLsaString(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return new LSA_STRING
            {
                Length = 0,
                MaximumLength = 0,
                Buffer = IntPtr.Zero
            };
        }

        var bytes = System.Text.Encoding.ASCII.GetByteCount(str);
        return new LSA_STRING
        {
            Length = (ushort)bytes,
            MaximumLength = (ushort)(bytes + 1), // +1 for null terminator
            Buffer = Marshal.StringToHGlobalAnsi(str)
        };
    }

    /// <summary>
    /// Frees a UNICODE_STRING buffer
    /// </summary>
    public static void FreeUnicodeString(ref UNICODE_STRING str)
    {
        if (str.Buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(str.Buffer);
            str.Buffer = IntPtr.Zero;
            str.Length = 0;
            str.MaximumLength = 0;
        }
    }

    /// <summary>
    /// Frees an LSA_STRING buffer
    /// </summary>
    public static void FreeLsaString(ref LSA_STRING str)
    {
        if (str.Buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(str.Buffer);
            str.Buffer = IntPtr.Zero;
            str.Length = 0;
            str.MaximumLength = 0;
        }
    }

    #endregion
}