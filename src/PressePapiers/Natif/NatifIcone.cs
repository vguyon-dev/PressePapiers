using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>
/// Déclarations Win32 de l'icône de notification (Shell_NotifyIcon), de son menu natif et du chargement de ses icônes.
/// Une même fonction peut être déclarée ailleurs : chaque brique reste autonome.
/// </summary>
internal static partial class NatifIcone
{
    // Commandes de Shell_NotifyIcon.
    internal const uint NimAdd = 0;
    internal const uint NimModify = 1;
    internal const uint NimDelete = 2;
    internal const uint NimSetVersion = 4;

    // Champs renseignés dans NOTIFYICONDATA (NIF_*).
    internal const uint NifMessage = 0x01;
    internal const uint NifIcon = 0x02;
    internal const uint NifTip = 0x04;
    internal const uint NifInfo = 0x10;
    internal const uint NifShowTip = 0x80; // en version 4, l'infobulle standard n'apparaît qu'avec ce drapeau

    internal const uint NiifInfo = 0x01;
    internal const uint VersionIcone4 = 4;

    // Messages reçus en version 4 : LOWORD(lParam) de MessageIcone.
    internal const uint NinSelect = 0x0400;
    internal const uint NinKeySelect = 0x0401;
    internal const uint WmContextMenu = 0x007B;

    // Messages reçus en version 0 (repli si NOTIFYICON_VERSION_4 est refusée).
    internal const uint WmLButtonUp = 0x0202;
    internal const uint WmRButtonUp = 0x0205;

    internal const uint WmNull = 0x0000;

    // Menu contextuel.
    internal const uint MfString = 0x0000;
    internal const uint MfSeparator = 0x0800;
    internal const uint TpmRightButton = 0x0002;
    internal const uint TpmBottomAlign = 0x0020;
    internal const uint TpmReturnCmd = 0x0100;

    // Chargement des icônes.
    internal const uint ImageIcon = 1;
    internal const uint LrLoadFromFile = 0x0010;
    internal const int SmCxSmIcon = 49;
    internal const int IdiApplication = 32512;

    /// <summary>
    /// NOTIFYICONDATAW, dans l'ordre et avec l'alignement natifs (976 octets en x64). Les textes sont des tampons fixes :
    /// la structure reste copiable tel quel vers Windows, sans désactiver le marshaling du runtime pour tout le projet.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DonneesIcone
    {
        public uint Taille;
        public nint Hwnd;
        public uint Id;
        public uint Drapeaux;
        public uint MessageRappel;
        public nint Icone;
        public fixed char Infobulle[128];
        public uint Etat;
        public uint MasqueEtat;
        public fixed char Bulle[256];
        public uint DelaiOuVersion; // union uTimeout / uVersion
        public fixed char TitreBulle[64];
        public uint DrapeauxBulle;
        public Guid GuidIcone;
        public nint IconeBulle;
    }

    /// <summary>POINT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    /// <summary>Écrit l'infobulle (127 caractères au plus, le zéro final est gardé).</summary>
    internal static unsafe void DefinirInfobulle(ref DonneesIcone donnees, string texte)
    {
        fixed (char* tampon = donnees.Infobulle)
        {
            Copier(tampon, 128, texte);
        }
    }

    /// <summary>Écrit le message de la bulle (255 caractères au plus).</summary>
    internal static unsafe void DefinirBulle(ref DonneesIcone donnees, string texte)
    {
        fixed (char* tampon = donnees.Bulle)
        {
            Copier(tampon, 256, texte);
        }
    }

    /// <summary>Écrit le titre de la bulle (63 caractères au plus).</summary>
    internal static unsafe void DefinirTitreBulle(ref DonneesIcone donnees, string texte)
    {
        fixed (char* tampon = donnees.TitreBulle)
        {
            Copier(tampon, 64, texte);
        }
    }

    // Copie un texte dans un tampon de taille fixe, tronqué pour garder le zéro final.
    private static unsafe void Copier(char* tampon, int taille, string texte)
    {
        var n = Math.Min(texte.Length, taille - 1);
        var cible = new Span<char>(tampon, taille);
        texte.AsSpan(0, n).CopyTo(cible);
        cible[n..].Clear();
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool NotifierIcone(uint commande, ref DonneesIcone donnees);

    [LibraryImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    internal static partial nint CreerMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AjouterAuMenu(nint menu, uint drapeaux, nuint identifiant, string? texte);

    [LibraryImport("user32.dll", EntryPoint = "SetMenuDefaultItem", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DefinirCommandeParDefaut(nint menu, uint element, uint parPosition);

    [LibraryImport("user32.dll", EntryPoint = "TrackPopupMenuEx", SetLastError = true)]
    internal static partial int AfficherMenu(nint menu, uint drapeaux, int x, int y, nint hwnd, nint parametres);

    [LibraryImport("user32.dll", EntryPoint = "DestroyMenu", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DetruireMenu(nint menu);

    [LibraryImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AmenerAuPremierPlan(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PosterMessage(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool LirePositionCurseur(out Point position);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint ChargerImage(nint instance, string nom, uint type, int largeur, int hauteur, uint options);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW", SetLastError = true)]
    internal static partial nint ChargerIconeSysteme(nint instance, nint identifiant);

    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DetruireIcone(nint icone);

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForSystem")]
    internal static partial uint LireDpiSysteme();

    [LibraryImport("user32.dll", EntryPoint = "GetSystemMetricsForDpi")]
    internal static partial int LireMetrique(int indice, uint dpi);
}
