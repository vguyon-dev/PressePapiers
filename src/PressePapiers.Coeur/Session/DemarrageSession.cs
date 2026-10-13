using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Coeur.Session;

/// <summary>Détection d'une nouvelle session Windows et purge de Temporaire. Coquille : remplacée par la tâche 8.</summary>
public static class DemarrageSession
{
    /// <summary>Clé de <c>meta</c> qui garde l'heure d'ouverture de la session.</summary>
    public const string CleRepere = "session.heure_ouverture";

    /// <summary>
    /// Applique les règles de la spec (section 6) : la purge a lieu avant la mise à jour du repère ;
    /// une valeur nulle ou 0 donne <see cref="EtatSession.Indetermine"/> sans purge.
    /// </summary>
    public static ResultatSession Appliquer(ISourceSession source, DepotHistorique depot, IJournal journal) =>
        throw new NotImplementedException("Tâche 8");
}
