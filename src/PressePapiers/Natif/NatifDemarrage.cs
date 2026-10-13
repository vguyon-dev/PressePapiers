using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows du démarrage : boîte de message avant toute fenêtre, attente non bloquante de la redirection.</summary>
internal static partial class NatifDemarrage
{
    internal const uint MB_OK = 0x0, MB_YESNO = 0x4, MB_ICONERROR = 0x10, MB_ICONWARNING = 0x30, MB_DEFBUTTON2 = 0x100, MB_SETFOREGROUND = 0x10000;
    internal const int IDYES = 6;

    /// <summary>Attente de CoWaitForMultipleObjects : sans drapeau particulier, les messages COM continuent d'être pompés.</summary>
    internal const uint CWMO_DEFAULT = 0;

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int AfficherMessage(nint proprietaire, string texte, string titre, uint style);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW", SetLastError = true)]
    internal static partial nint CreerEvenement(nint attributs, [MarshalAs(UnmanagedType.Bool)] bool manuel,
                                                [MarshalAs(UnmanagedType.Bool)] bool signale, nint nom);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetEvent(nint evenement);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    /// <summary>
    /// Attend un handle en laissant le fil STA pomper ses messages (CoWaitForMultipleObjects) ; renvoie le HRESULT,
    /// 0 quand le handle est signalé.
    /// </summary>
    internal static unsafe int AttendreSansBloquer(nint handle, uint delaiMs) =>
        CoWaitForMultipleObjects(CWMO_DEFAULT, delaiMs, 1, &handle, out _);

    [LibraryImport("ole32.dll")]
    private static unsafe partial int CoWaitForMultipleObjects(uint drapeaux, uint delai, uint nombre, nint* handles, out uint index);
}
