using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows de la lecture du niveau d'intégrité (spec, section 10.3). Chaque brique native est autonome.</summary>
internal static partial class NatifIntegrite
{
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const uint TOKEN_QUERY = 0x0008;

    /// <summary>Valeur TOKEN_INFORMATION_CLASS pour TokenIntegrityLevel.</summary>
    internal const int TokenIntegrityLevel = 25;

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hwnd, out uint idProcessus);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint OpenProcess(uint acces, [MarshalAs(UnmanagedType.Bool)] bool heriter, uint idProcessus);

    /// <summary>Pseudo-handle du processus courant : inutile de le fermer.</summary>
    [LibraryImport("kernel32.dll")]
    internal static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(nint processus, uint acces, out nint jeton);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetTokenInformation(nint jeton, int classe, nint tampon, uint tailleTampon, out uint tailleRequise);

    /// <summary>Renvoie un pointeur vers l'octet qui compte les sous-autorités du SID.</summary>
    [LibraryImport("advapi32.dll")]
    internal static partial nint GetSidSubAuthorityCount(nint sid);

    /// <summary>Renvoie un pointeur vers la sous-autorité demandée du SID.</summary>
    [LibraryImport("advapi32.dll")]
    internal static partial nint GetSidSubAuthority(nint sid, uint index);
}
