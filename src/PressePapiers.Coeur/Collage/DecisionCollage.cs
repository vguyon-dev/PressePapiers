namespace PressePapiers.Coeur.Collage;

/// <summary>Choix entre coller et seulement copier (spec, sections 10.2 et 10.3).</summary>
public static class DecisionCollage
{
    /// <summary>
    /// Décide de l'issue d'un collage. <paramref name="niveauCible"/> null (lecture impossible) est traité
    /// comme un niveau plus élevé que celui de l'outil : en cas de doute on ne colle pas, car Windows bloque
    /// sans rien signaler un Ctrl+V envoyé vers une fenêtre de niveau supérieur.
    /// </summary>
    /// <remarks>
    /// L'ordre des tests est le sens des causes : la demande de l'utilisateur d'abord, puis l'existence d'une
    /// fenêtre d'origine, puis sa survie, puis son niveau (illisible pour une fenêtre disparue).
    /// </remarks>
    public static IssueCollage Decider(bool copieSeuleDemandee, bool fenetreOrigineConnue, bool fenetreExiste,
                                       int? niveauCible, int niveauOutil)
    {
        if (copieSeuleDemandee) return IssueCollage.CopieDemandee;
        if (!fenetreOrigineConnue) return IssueCollage.CopieSansFenetreOrigine;
        if (!fenetreExiste) return IssueCollage.CopieFenetreFermee;
        if (niveauCible is not int niveau || niveau > niveauOutil) return IssueCollage.CopieFenetreElevee;
        return IssueCollage.Colle;
    }
}
