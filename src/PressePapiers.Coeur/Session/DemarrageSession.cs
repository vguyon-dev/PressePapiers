using System.Globalization;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Coeur.Session;

/// <summary>
/// Détection d'une nouvelle session Windows au démarrage de l'outil, et purge de Temporaire (spec, section 6).
/// Le repère est l'heure d'ouverture de la session, gardée telle quelle et comparée en égalité exacte.
/// </summary>
public static class DemarrageSession
{
    /// <summary>Clé de <c>meta</c> qui garde l'heure d'ouverture de la session.</summary>
    public const string CleRepere = "session.heure_ouverture";

    /// <summary>
    /// Applique les règles de la spec : aucun repère enregistré (premier lancement) ou repère identique (même session),
    /// rien n'est supprimé ; repère différent (nouvelle session), Temporaire est purgé puis le repère mis à jour ;
    /// lecture impossible, nulle ou égale à 0, aucune purge, aucun changement du repère, et un avertissement au journal.
    /// La purge a lieu avant la mise à jour du repère : si elle échoue, le repère garde l'ancienne valeur et la purge
    /// est retentée au lancement suivant, au lieu d'être oubliée.
    /// </summary>
    /// <param name="source">Source de l'heure d'ouverture de la session.</param>
    /// <param name="depot">Dépôt qui porte Temporaire et la table <c>meta</c>.</param>
    /// <param name="journal">Journal des événements notables.</param>
    public static ResultatSession Appliquer(ISourceSession source, DepotHistorique depot, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(journal);

        long? heure;
        Exception? echec = null;
        try
        {
            heure = source.LireHeureOuverture();
        }
        catch (Exception exception)
        {
            // La source appelle Windows : sa panne ne doit pas empêcher le démarrage, seulement la purge.
            heure = null;
            echec = exception;
        }

        if (heure is null or 0)
        {
            journal.Avertissement(
                "Heure d'ouverture de la session Windows illisible : Temporaire n'est pas purgé, nouvel essai au prochain lancement.",
                echec);
            return new ResultatSession(EtatSession.Indetermine, 0);
        }

        var repere = heure.Value.ToString(CultureInfo.InvariantCulture);
        var precedent = depot.LireMeta(CleRepere);

        if (precedent is null)
        {
            depot.EcrireMeta(CleRepere, repere);
            journal.Info("Premier lancement : repère de session enregistré, Temporaire conservé.");
            return new ResultatSession(EtatSession.PremierLancement, 0);
        }

        if (precedent == repere)
        {
            return new ResultatSession(EtatSession.MemeSession, 0);
        }

        var purges = depot.PurgerTemporaire();
        depot.EcrireMeta(CleRepere, repere);
        journal.Info($"Nouvelle session Windows : {purges} élément(s) de Temporaire supprimé(s).");
        return new ResultatSession(EtatSession.NouvelleSession, purges);
    }
}
