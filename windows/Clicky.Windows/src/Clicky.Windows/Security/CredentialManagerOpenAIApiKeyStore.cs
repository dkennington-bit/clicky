using System.Runtime.InteropServices;
using System.Text;
using Clicky.Windows.Services;

namespace Clicky.Windows.Security;

public sealed class CredentialManagerOpenAIApiKeyStore : IOpenAIApiKeyStore
{
    private const string CredentialTargetName = "Clicky.OpenAI.ApiKey";
    private const int CredentialTypeGeneric = 1;
    private const int CredentialPersistenceLocalMachine = 2;

    public string? ReadApiKey()
    {
        if (!CredRead(CredentialTargetName, CredentialTypeGeneric, 0, out IntPtr credentialPointer))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
            {
                return null;
            }

            byte[] credentialBytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, credentialBytes, 0, credentialBytes.Length);
            return Encoding.Unicode.GetString(credentialBytes).TrimEnd('\0');
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void SaveApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be empty.", nameof(apiKey));
        }

        byte[] credentialBytes = Encoding.Unicode.GetBytes(apiKey);
        var credential = new NativeCredential
        {
            Type = CredentialTypeGeneric,
            TargetName = CredentialTargetName,
            CredentialBlobSize = credentialBytes.Length,
            Persist = CredentialPersistenceLocalMachine,
            UserName = Environment.UserName
        };

        IntPtr credentialBlobPointer = Marshal.AllocCoTaskMem(credentialBytes.Length);
        try
        {
            Marshal.Copy(credentialBytes, 0, credentialBlobPointer, credentialBytes.Length);
            credential.CredentialBlob = credentialBlobPointer;

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException($"Failed to save OpenAI API key to Windows Credential Manager. Win32 error: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(credentialBlobPointer);
        }
    }

    public void DeleteApiKey()
    {
        _ = CredDelete(CredentialTargetName, CredentialTypeGeneric, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential userCredential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPointer);
}
