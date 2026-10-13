using Microsoft.UI.Dispatching;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Interface;

/// <summary>
/// Point d'entrée protégé de tout rappel de l'interface. Une exception qui sort d'un rappel de <c>DispatcherQueue</c> ou
/// d'une minuterie n'atteint pas <c>UnhandledException</c> : elle arrête le processus sans rien écrire au journal. Tout
/// travail posté ou programmé passe donc ici, qui note l'exception et laisse l'application continuer.
/// </summary>
internal static class ExecutionProtegee
{
    /// <summary>Joue l'action en notant au journal l'exception qu'elle lève.</summary>
    /// <param name="journal">Journal de l'application.</param>
    /// <param name="action">Travail à faire.</param>
    /// <param name="contexte">Ce que l'action fait, pour le message du journal (« la recherche »).</param>
    public static void Executer(IJournal journal, Action action, string contexte)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            journal.Erreur($"Exception pendant {contexte}.", exception);
        }
    }

    /// <summary>Poste l'action sur le fil de la file, protégée. Renvoie faux si la file n'accepte plus de travail (arrêt).</summary>
    public static bool Poster(DispatcherQueue file, IJournal journal, Action action, string contexte,
                              DispatcherQueuePriority priorite = DispatcherQueuePriority.Normal) =>
        file.TryEnqueue(priorite, () => Executer(journal, action, contexte));

    /// <summary>Fait jouer l'action protégée à chaque tic de la minuterie.</summary>
    public static void Brancher(DispatcherQueueTimer minuteur, IJournal journal, Action action, string contexte) =>
        minuteur.Tick += (_, _) => Executer(journal, action, contexte);

    /// <summary>
    /// Lance une opération asynchrone sans l'attendre. Toute exception de son corps complet est notée : sans cela elle
    /// resterait dans une tâche que personne n'observe.
    /// </summary>
    public static void Lancer(IJournal journal, Func<Task> operation, string contexte) =>
        _ = ExecuterAsync(journal, operation, contexte);

    private static async Task ExecuterAsync(IJournal journal, Func<Task> operation, string contexte)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            journal.Erreur($"Exception pendant {contexte}.", exception);
        }
    }
}
