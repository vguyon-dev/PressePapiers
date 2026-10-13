using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows de la lecture de la session (spec, section 6). Chaque brique native est autonome.</summary>
internal static partial class NatifSession
{
    /// <summary>WTS_CURRENT_SERVER_HANDLE : le serveur local.</summary>
    internal const nint ServeurCourant = 0;

    /// <summary>WTS_CURRENT_SESSION : la session du processus appelant.</summary>
    internal const uint SessionCourante = 0xFFFFFFFF;

    /// <summary>Valeur WTS_INFO_CLASS pour WTSSessionInfo (structure WTSINFOW).</summary>
    internal const int ClasseSessionInfo = 24;

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InterrogerSession(nint serveur, uint idSession, int classe, out nint tampon, out uint octets);

    /// <summary>Libère un tampon rendu par WTSQuerySessionInformationW.</summary>
    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSFreeMemory")]
    internal static partial void LibererMemoire(nint memoire);

    /// <summary>
    /// WTSINFOW : les huit compteurs, trois noms en UTF-16 de longueur fixe (nom de la station, domaine, utilisateur),
    /// puis cinq dates FILETIME. Seule LogonTime sert ; les autres champs fixent la disposition.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct InfoSession
    {
        internal int Etat;
        internal uint IdSession;
        internal uint OctetsEntrants;
        internal uint OctetsSortants;
        internal uint ImagesEntrantes;
        internal uint ImagesSortantes;
        internal uint OctetsEntrantsCompresses;
        internal uint OctetsSortantsCompresses;
        internal fixed char NomStation[32];
        internal fixed char Domaine[17];
        internal fixed char Utilisateur[21];
        internal long Connexion;
        internal long Deconnexion;
        internal long DerniereEntree;
        internal long OuvertureSession;
        internal long Maintenant;
    }
}
