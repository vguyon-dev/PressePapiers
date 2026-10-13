using System.Runtime.InteropServices;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Amène une fenêtre au premier plan (spec, section 10.1). Windows interdit à un processus d'arrière-plan de
/// voler le premier plan : on applique, dans l'ordre, les deux recettes éprouvées par PowerToys, CopyQ et Ditto,
/// sans jamais toucher au réglage ForegroundLockTimeout de l'utilisateur.
/// </summary>
public static class PremierPlan
{
    /// <summary>Étape qui a fini par donner le premier plan, relevée pour le diagnostic.</summary>
    internal enum Etape
    {
        /// <summary>La fenêtre l'avait déjà (ou l'a obtenue dès la restauration).</summary>
        DejaAuPremierPlan,

        /// <summary>SendInput d'une entrée souris vide, puis SetForegroundWindow.</summary>
        SendInputVide,

        /// <summary>AttachThreadInput, puis SetForegroundWindow.</summary>
        AttachThreadInput,

        /// <summary>Windows a refusé : l'appelant doit le dire à l'utilisateur.</summary>
        Echec,
    }

    // Classes de la barre des tâches (principale et secondaires) et du bureau : jamais une fenêtre d'origine.
    private static readonly string[] _classesExclues = ["Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW"];

    /// <summary>Restaure la fenêtre si besoin puis la met au premier plan ; renvoie faux si Windows refuse.</summary>
    public static bool Amener(nint hwnd) => AmenerParEtapes(hwnd) != Etape.Echec;

    /// <summary>Comme <see cref="Amener"/>, mais indique l'étape qui a réussi.</summary>
    internal static Etape AmenerParEtapes(nint hwnd)
    {
        if (hwnd == 0 || !NatifPremierPlan.IsWindow(hwnd)) return Etape.Echec;

        // Étape 1 : une fenêtre réduite est restaurée, une fenêtre cachée est affichée.
        if (NatifPremierPlan.IsIconic(hwnd)) NatifPremierPlan.ShowWindow(hwnd, NatifPremierPlan.SW_RESTORE);
        else if (!NatifPremierPlan.IsWindowVisible(hwnd)) NatifPremierPlan.ShowWindow(hwnd, NatifPremierPlan.SW_SHOW);

        if (EstAuPremierPlan(hwnd)) return Etape.DejaAuPremierPlan;

        // Étape 2 : une entrée souris vide fait de l'outil le dernier fournisseur d'entrée, ce qui rend
        // SetForegroundWindow recevable. Technique non documentée, utilisée en production par PowerToys.
        EnvoyerEntreeVide();
        NatifPremierPlan.SetForegroundWindow(hwnd);
        if (EstAuPremierPlan(hwnd)) return Etape.SendInputVide;

        // Étape 3 : partage de la file d'entrée avec les fils concernés pendant l'appel.
        AmenerParAttachement(hwnd);
        return EstAuPremierPlan(hwnd) ? Etape.AttachThreadInput : Etape.Echec;
    }

    /// <summary>Fenêtre actuellement au premier plan.</summary>
    public static nint FenetreActive() => NatifPremierPlan.GetForegroundWindow();

    /// <summary>Vrai si la fenêtre peut servir de fenêtre d'origine : ni l'outil, ni le bureau, ni la barre des tâches.</summary>
    public static bool EstOrigineValide(nint hwnd, nint hwndOutil)
    {
        if (hwnd == 0 || hwnd == hwndOutil) return false;
        if (hwnd == NatifPremierPlan.GetDesktopWindow() || hwnd == NatifPremierPlan.GetShellWindow()) return false;

        // Une fenêtre fille de l'outil (boîte de dialogue, fenêtre flottante) vaut l'outil lui-même.
        if (hwndOutil != 0 && NatifPremierPlan.GetAncestor(hwnd, NatifPremierPlan.GA_ROOTOWNER) == hwndOutil) return false;

        return Array.IndexOf(_classesExclues, LireClasse(hwnd)) < 0;
    }

    private static bool EstAuPremierPlan(nint hwnd) => NatifPremierPlan.GetForegroundWindow() == hwnd;

    private static void EnvoyerEntreeVide()
    {
        // Entrée entièrement à zéro : aucun déplacement, aucun bouton, aucun effet visible.
        var entree = new NatifPremierPlan.Entree { Type = NatifPremierPlan.INPUT_MOUSE };
        NatifPremierPlan.SendInput(1, in entree, Marshal.SizeOf<NatifPremierPlan.Entree>());
    }

    private static void AmenerParAttachement(nint hwnd)
    {
        var courant = NatifPremierPlan.GetCurrentThreadId();
        var filPremierPlan = NatifPremierPlan.GetWindowThreadProcessId(NatifPremierPlan.GetForegroundWindow(), out _);
        var filCible = NatifPremierPlan.GetWindowThreadProcessId(hwnd, out _);

        // Le fil courant se lie à chaque autre fil concerné (premier plan, cible), une seule fois chacun.
        Span<uint> candidats = [filPremierPlan, filCible];
        Span<uint> attaches = stackalloc uint[2];
        var nombre = 0;

        foreach (var fil in candidats)
        {
            if (fil == 0 || fil == courant || attaches[..nombre].Contains(fil)) continue;
            if (NatifPremierPlan.AttachThreadInput(courant, fil, true)) attaches[nombre++] = fil;
        }

        try
        {
            NatifPremierPlan.BringWindowToTop(hwnd);
            NatifPremierPlan.SetForegroundWindow(hwnd);
        }
        finally
        {
            // Toujours détacher : un attachement oublié lierait durablement les files d'entrée.
            for (var i = 0; i < nombre; i++) NatifPremierPlan.AttachThreadInput(courant, attaches[i], false);
        }
    }

    private static unsafe string LireClasse(nint hwnd)
    {
        var tampon = stackalloc char[64];
        var longueur = NatifPremierPlan.GetClassName(hwnd, tampon, 64);
        return longueur > 0 ? new string(tampon, 0, longueur) : string.Empty;
    }
}
