using System.Runtime.InteropServices;
using PressePapiers.Coeur.Session;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Repère de session lu par WTSQuerySessionInformationW (spec, section 6) : l'heure d'ouverture de la session de terminal
/// en cours, champ LogonTime de WTSINFOW. Ce repère appartient à la session, pas au jeton du processus : il est le même
/// pour un lancement normal et pour un lancement en administrateur.
/// </summary>
public sealed class SourceSessionWts : ISourceSession
{
    /// <summary>FILETIME brut de LogonTime (gardé tel quel, comparé en égalité exacte), ou null si la lecture échoue.</summary>
    public long? LireHeureOuverture()
    {
        if (!NatifSession.InterrogerSession(NatifSession.ServeurCourant, NatifSession.SessionCourante,
                                            NatifSession.ClasseSessionInfo, out var tampon, out var octets))
        {
            return null;
        }

        try
        {
            // Une structure plus courte que prévu (version de Windows inattendue) ne se lit pas : mieux vaut « inconnu ».
            return octets >= Marshal.SizeOf<NatifSession.InfoSession>()
                ? Marshal.PtrToStructure<NatifSession.InfoSession>(tampon).OuvertureSession
                : null;
        }
        finally
        {
            NatifSession.LibererMemoire(tampon);
        }
    }
}
