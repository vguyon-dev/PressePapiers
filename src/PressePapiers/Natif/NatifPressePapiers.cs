using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>
/// Appels Windows de la lecture du presse-papiers et de la recherche de l'application d'origine
/// (user32 et kernel32). Chaque brique du projet déclare ses propres appels : une même fonction peut figurer ailleurs.
/// </summary>
internal static partial class NatifPressePapiers
{
    /// <summary>Texte Unicode (CF_UNICODETEXT).</summary>
    public const uint CfUnicodeText = 13;

    /// <summary>Image DIB (CF_DIB).</summary>
    public const uint CfDib = 8;

    /// <summary>Image DIB version 5 (CF_DIBV5), seule à porter un canal alpha.</summary>
    public const uint CfDibV5 = 17;

    /// <summary>Liste de fichiers (CF_HDROP).</summary>
    public const uint CfHDrop = 15;

    /// <summary>Ouvre le presse-papiers ; <paramref name="hwndProprietaire"/> à 0 l'associe à la tâche courante.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenClipboard(nint hwndProprietaire);

    /// <summary>Referme le presse-papiers.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseClipboard();

    /// <summary>Vrai si un format est présent (le presse-papiers n'a pas besoin d'être ouvert).</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsClipboardFormatAvailable(uint format);

    /// <summary>Poignée mémoire d'un format ; le presse-papiers doit être ouvert, la mémoire reste à lui.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint GetClipboardData(uint format);

    /// <summary>Enregistre (ou retrouve) un format nommé ; 0 en cas d'échec.</summary>
    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial uint RegisterClipboardFormat(string nom);

    /// <summary>Fenêtre actuellement au premier plan.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    /// <summary>Fenêtre propriétaire du contenu du presse-papiers (repli pour l'application d'origine).</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetClipboardOwner();

    /// <summary>Fil et processus d'une fenêtre ; renvoie l'identifiant du fil.</summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hwnd, out uint idProcessus);

    /// <summary>Énumère les fenêtres filles directes : passer 0 pour <paramref name="apres"/> la première fois.</summary>
    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindowEx(nint parent, nint apres, string? classe, string? titre);

    /// <summary>Verrouille un bloc mémoire globale et renvoie son adresse.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalLock(nint memoire);

    /// <summary>Déverrouille un bloc mémoire globale.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint memoire);

    /// <summary>Taille en octets d'un bloc mémoire globale (peut dépasser la taille demandée).</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nuint GlobalSize(nint memoire);
}
