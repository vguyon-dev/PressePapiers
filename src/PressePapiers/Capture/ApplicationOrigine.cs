using PressePapiers.Natif;

namespace PressePapiers.Capture;

/// <summary>Nom du processus d'où vient une copie : une indication au mieux, jamais une certitude.</summary>
public static class ApplicationOrigine
{
    private const string HoteApplicationsUwp = "ApplicationFrameHost";

    // Un nom relevé reste valable une minute : assez pour les rafales de copies, trop court pour qu'un identifiant de
    // processus réutilisé par Windows donne un nom faux.
    private static readonly TimeSpan _dureeCache = TimeSpan.FromMinutes(1);
    private const int TailleMaxCache = 64;

    private static readonly object _verrou = new();
    private static readonly Dictionary<uint, (string? Nom, long Expiration)> _cache = [];

    /// <summary>
    /// Nom du processus de la fenêtre, sans extension. Pour une application UWP, la fenêtre appartient à
    /// ApplicationFrameHost : on cherche alors, parmi ses fenêtres filles, celle d'un autre processus, qui est l'application.
    /// </summary>
    /// <param name="fenetre">Fenêtre au premier plan relevée à la copie, ou 0.</param>
    /// <returns>Le nom du processus (par exemple « Code », « WindowsTerminal »), ou null si rien n'est déterminé.</returns>
    public static string? Determiner(nint fenetre)
    {
        if (fenetre == 0)
        {
            return null;
        }

        var idHote = IdProcessus(fenetre);
        var nom = NomProcessus(idHote);
        if (!string.Equals(nom, HoteApplicationsUwp, StringComparison.OrdinalIgnoreCase))
        {
            return nom;
        }

        // Le nom ApplicationFrameHost n'apprend rien : sans fenêtre fille d'un autre processus, l'origine reste inconnue.
        nint fille = 0;
        while ((fille = NatifPressePapiers.FindWindowEx(fenetre, fille, null, null)) != 0)
        {
            var idFille = IdProcessus(fille);
            if (idFille != 0 && idFille != idHote)
            {
                return NomProcessus(idFille);
            }
        }

        return null;
    }

    /// <summary>
    /// Origine de la copie qui vient d'être signalée : processus de la fenêtre au premier plan ; seulement à défaut,
    /// processus propriétaire du presse-papiers (il peut être un service sans rapport avec la fenêtre que l'utilisateur voit).
    /// À appeler dès la réception de la notification, avant que l'utilisateur ne change de fenêtre.
    /// </summary>
    internal static string? DeterminerPourCopie() =>
        Determiner(NatifPressePapiers.GetForegroundWindow()) ?? Determiner(NatifPressePapiers.GetClipboardOwner());

    private static uint IdProcessus(nint fenetre)
    {
        NatifPressePapiers.GetWindowThreadProcessId(fenetre, out var id);
        return id;
    }

    // Appelé sur le fil d'interface à chaque copie : un petit cache évite même l'ouverture du processus pour les copies
    // en rafale depuis la même application.
    private static string? NomProcessus(uint idProcessus)
    {
        if (idProcessus == 0)
        {
            return null;
        }

        var maintenant = Environment.TickCount64;
        lock (_verrou)
        {
            if (_cache.TryGetValue(idProcessus, out var connu) && connu.Expiration > maintenant)
            {
                return connu.Nom;
            }
        }

        var nom = LireNomProcessus(idProcessus);
        lock (_verrou)
        {
            if (_cache.Count >= TailleMaxCache)
            {
                _cache.Clear();
            }

            _cache[idProcessus] = (nom, maintenant + (long)_dureeCache.TotalMilliseconds);
        }

        return nom;
    }

    // OpenProcess avec le droit minimal puis QueryFullProcessImageName : quelques microsecondes, là où
    // Process.GetProcessById énumère tous les processus de la machine. Processus disparu ou accès refusé : origine inconnue.
    private static unsafe string? LireNomProcessus(uint idProcessus)
    {
        var processus = NatifProcessus.OpenProcess(NatifProcessus.PROCESS_QUERY_LIMITED_INFORMATION, false, idProcessus);
        if (processus == 0)
        {
            return null;
        }

        try
        {
            const int Capacite = 1024;
            var tampon = stackalloc char[Capacite];
            var longueur = (uint)Capacite;
            if (!NatifProcessus.QueryFullProcessImageName(processus, 0, tampon, ref longueur) || longueur == 0)
            {
                return null;
            }

            var nom = Path.GetFileNameWithoutExtension(new ReadOnlySpan<char>(tampon, (int)longueur));
            return nom.IsEmpty ? null : nom.ToString();
        }
        finally
        {
            NatifProcessus.CloseHandle(processus);
        }
    }
}
