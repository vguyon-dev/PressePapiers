using System.Runtime.InteropServices;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Niveau d'intégrité des processus (spec, section 10.3). Windows bloque sans rien dire un Ctrl+V envoyé vers une
/// fenêtre de niveau supérieur : le collage compare donc le niveau de la cible à celui de l'outil.
/// </summary>
public static class NiveauIntegrite
{
    // Le niveau d'un processus ne change pas pendant sa vie : une seule lecture suffit.
    private static readonly Lazy<int> _niveauCourant = new(LireNiveauCourant);

    /// <summary>RID du niveau d'intégrité du processus d'une fenêtre ; null si une étape de la lecture échoue.</summary>
    public static int? DeFenetre(nint hwnd)
    {
        if (hwnd == 0) return null;
        if (NatifIntegrite.GetWindowThreadProcessId(hwnd, out var idProcessus) == 0 || idProcessus == 0) return null;

        var processus = NatifIntegrite.OpenProcess(NatifIntegrite.PROCESS_QUERY_LIMITED_INFORMATION, false, idProcessus);
        if (processus == 0) return null;
        try
        {
            return LireDuProcessus(processus);
        }
        finally
        {
            NatifIntegrite.CloseHandle(processus);
        }
    }

    /// <summary>
    /// RID du niveau d'intégrité du processus courant. Si la lecture échoue, renvoie 0 (le plus bas) : toute cible
    /// passe alors pour plus élevée et l'outil copie sans coller, au lieu de coller à tort.
    /// </summary>
    public static int DuProcessusCourant() => _niveauCourant.Value;

    private static int LireNiveauCourant() => LireDuProcessus(NatifIntegrite.GetCurrentProcess()) ?? 0;

    // Jeton du processus, TokenIntegrityLevel, puis dernier sous-identifiant du SID : c'est le RID du niveau
    // (0x1000 bas, 0x2000 moyen, 0x3000 élevé, 0x4000 système).
    private static int? LireDuProcessus(nint processus)
    {
        if (!NatifIntegrite.OpenProcessToken(processus, NatifIntegrite.TOKEN_QUERY, out var jeton)) return null;
        try
        {
            NatifIntegrite.GetTokenInformation(jeton, NatifIntegrite.TokenIntegrityLevel, 0, 0, out var taille);
            if (taille == 0) return null;

            var tampon = Marshal.AllocHGlobal((int)taille);
            try
            {
                if (!NatifIntegrite.GetTokenInformation(jeton, NatifIntegrite.TokenIntegrityLevel, tampon, taille, out _))
                    return null;

                // TOKEN_MANDATORY_LABEL commence par le pointeur vers le SID.
                var sid = Marshal.ReadIntPtr(tampon);
                if (sid == 0) return null;

                var pointeurNombre = NatifIntegrite.GetSidSubAuthorityCount(sid);
                if (pointeurNombre == 0) return null;
                var nombre = Marshal.ReadByte(pointeurNombre);
                if (nombre == 0) return null;

                var pointeurRid = NatifIntegrite.GetSidSubAuthority(sid, (uint)(nombre - 1));
                return pointeurRid == 0 ? null : Marshal.ReadInt32(pointeurRid);
            }
            finally
            {
                Marshal.FreeHGlobal(tampon);
            }
        }
        finally
        {
            NatifIntegrite.CloseHandle(jeton);
        }
    }
}
