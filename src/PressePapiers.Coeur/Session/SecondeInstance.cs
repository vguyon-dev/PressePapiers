namespace PressePapiers.Coeur.Session;

/// <summary>
/// Conduite d'une instance qui démarre alors qu'une instance plus élevée (mode administrateur) tourne déjà. À l'ouverture de
/// session, Windows lance parfois les deux à quelques secondes d'écart (tâche planifiée, puis démarrage ordinaire) : la
/// seconde s'arrête alors sans rien dire. Lancée plus tard, c'est l'utilisateur qui la demande : il est prévenu.
/// </summary>
public static class SecondeInstance
{
    /// <summary>Écart en deçà duquel les deux lancements sont jugés venir de l'ouverture de session.</summary>
    public static readonly TimeSpan FenetreOuvertureSession = TimeSpan.FromMinutes(5);

    /// <summary>Dit s'il faut prévenir l'utilisateur que l'instance principale tourne déjà.</summary>
    /// <param name="debutPrincipale">Heure de démarrage de l'instance principale ; null si elle n'a pas pu être lue.</param>
    /// <param name="maintenant">Heure courante, dans le même fuseau.</param>
    /// <returns>Vrai si le lancement semble venir de l'utilisateur, ou si on ne sait pas.</returns>
    public static bool DoitPrevenir(DateTime? debutPrincipale, DateTime maintenant) =>
        debutPrincipale is not { } debut || maintenant - debut > FenetreOuvertureSession;
}
