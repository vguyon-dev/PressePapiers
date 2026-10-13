namespace PressePapiers.Coeur.Session;

/// <summary>Situation de la session Windows au démarrage de l'outil.</summary>
public enum EtatSession
{
    /// <summary>Aucun repère enregistré : rien n'est supprimé.</summary>
    PremierLancement,

    /// <summary>Même repère qu'au lancement précédent : rien n'est supprimé.</summary>
    MemeSession,

    /// <summary>Repère différent : Temporaire est purgé.</summary>
    NouvelleSession,

    /// <summary>Repère illisible ou nul : aucune purge.</summary>
    Indetermine,
}
