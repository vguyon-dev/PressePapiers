using Microsoft.Win32;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Systeme;

namespace PressePapiers.Interface;

/// <summary>
/// Décide quand la clé Run (lancement avec Windows) est réellement écrite. Une version Debug ne l'écrit jamais : son
/// exécutable, dans le dossier de génération, n'a rien à faire dans le démarrage de Windows et y remplacerait la version
/// installée. Une version Release ne la réécrit au démarrage que si le chemin de l'exécutable a changé.
/// </summary>
internal static class GestionDemarrage
{
    /// <summary>
    /// Aligne la clé Run sur le réglage, au démarrage de l'application.
    /// </summary>
    /// <param name="activer">Valeur du réglage « Démarrage avec Windows ».</param>
    /// <param name="chemin">Chemin de l'exécutable en cours.</param>
    /// <param name="journal">Journal de l'application.</param>
    public static void Synchroniser(bool activer, string chemin, IJournal journal)
    {
#if DEBUG
        journal.Info("Version Debug : la clé Run du démarrage avec Windows n'est pas modifiée.");
#else
        var actuel = LireValeur();
        if (activer)
        {
            if (string.Equals(actuel, ValeurAttendue(chemin), StringComparison.OrdinalIgnoreCase))
            {
                journal.Info("Démarrage avec Windows : la clé Run est déjà à jour.");
                return;
            }

            DemarrageWindows.Appliquer(true, chemin);
            journal.Info("Démarrage avec Windows : clé Run écrite (chemin nouveau ou absent).");
        }
        else if (actuel is not null)
        {
            DemarrageWindows.Appliquer(false, chemin);
            journal.Info("Démarrage avec Windows : clé Run retirée.");
        }
#endif
    }

    /// <summary>
    /// Mode administrateur : le lancement est confié à une tâche planifiée, que le script d'installation crée et retire. L'application
    /// ne gère plus la clé Run, mais en retire la valeur si elle pointe vers cet exécutable : elle ferait un second lancement,
    /// non élevé, à chaque ouverture de session. Une valeur qui pointe ailleurs (autre installation) n'est pas touchée.
    /// </summary>
    /// <param name="chemin">Chemin de l'exécutable en cours.</param>
    /// <param name="journal">Journal de l'application.</param>
    public static void RetirerCleRunSiElleMePointe(string chemin, IJournal journal)
    {
#if DEBUG
        journal.Info("Version Debug : la clé Run du démarrage avec Windows n'est pas modifiée.");
#else
        if (string.Equals(LireValeur(), ValeurAttendue(chemin), StringComparison.OrdinalIgnoreCase))
        {
            DemarrageWindows.Appliquer(false, chemin);
            journal.Info("Mode administrateur : clé Run retirée, le lancement est confié à la tâche planifiée.");
        }
        else
        {
            journal.Info("Mode administrateur : le démarrage est géré par la tâche planifiée, la clé Run n'est pas utilisée.");
        }
#endif
    }

    /// <summary>
    /// Applique un choix explicite de l'utilisateur (interrupteur des Paramètres) : la clé est écrite ou retirée sans
    /// condition de chemin, sauf en version Debug où elle n'est jamais touchée.
    /// </summary>
    /// <param name="activer">Nouvelle valeur du réglage.</param>
    /// <param name="journal">Journal de l'application.</param>
    public static void Modifier(bool activer, IJournal journal)
    {
#if DEBUG
        journal.Info($"Version Debug : le démarrage avec Windows ({(activer ? "activé" : "désactivé")}) n'est pas écrit dans la clé Run.");
#else
        DemarrageWindows.Appliquer(activer, Environment.ProcessPath ?? string.Empty);
#endif
    }

#if !DEBUG
    // Même forme que DemarrageWindows.Appliquer : le chemin entre guillemets.
    private static string ValeurAttendue(string chemin) => $"\"{chemin}\"";

    private static string? LireValeur()
    {
        using var cle = Registry.CurrentUser.OpenSubKey(DemarrageWindows.CleRun);
        return cle?.GetValue(DemarrageWindows.NomValeur) as string;
    }
#endif
}
